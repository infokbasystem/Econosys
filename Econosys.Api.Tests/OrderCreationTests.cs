using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Econosys.Api.Controllers;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Econosys.Api.Tests;

public class OrderCreationTests
{
    [Theory]
    [InlineData(22, true, "Customer salesperson")]
    [InlineData(22, false, "Customer salesperson")]
    [InlineData(null, true, null)]
    public async Task CustomerDetailsIncludeAssignedSalespersonForNewOrderReference(
        int? responsibleUserId, bool active, string? expectedName)
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        context.LegacyUsers.AddRange(
            new LegacyUser { Id = 22, Name = "Customer salesperson", Active = active },
            new LegacyUser { Id = 42, Name = "Order creator", Active = true });
        context.Customers.Add(new Customer { Id = 1, ResponsibleUserId = responsibleUserId });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var controller = new CustomersController(context,
            NullLogger<CustomersController>.Instance,
            new UserResolution(new LegacyUser { Id = 42, CompanyId = 1 }));

        var result = await controller.GetById(1);

        var details = Assert.IsType<CustomerDetailsDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(responsibleUserId, details.Customer.ResponsibleUserId);
        Assert.Equal(expectedName, details.Customer.ResponsibleUserName);
    }

    [Theory]
    [InlineData("ApplyCreateRequestToEntity", null, null)]
    [InlineData("ApplyCreateRequestToEntity", 12, 175.5)]
    [InlineData("ApplyUpdateRequestToEntity", null, null)]
    [InlineData("ApplyUpdateRequestToEntity", 12, 175.5)]
    public void CustomerOrderPackagingQuantityAndPriceRoundTrip(string method, int? quantity, double? price)
    {
        var order = new CustomerOrder { NrOfEurPallet = 3, EurPalletValue = 99 };
        CreateCustomerOrderRequest request = method == "ApplyCreateRequestToEntity"
            ? new CreateCustomerOrderRequest()
            : new UpdateCustomerOrderRequest();
        request.PalletFormatId = 7;
        request.EurPallet = true;
        request.NrOfEurPallet = (short?)quantity;
        request.EurPalletValue = price;
        typeof(CustomerOrdersController).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [order, request]);
        var response = Assert.IsType<CustomerOrderDto>(typeof(CustomerOrdersController)
            .GetMethod("MapToDto", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [order]));
        Assert.Equal(request.NrOfEurPallet, response.NrOfEurPallet);
        Assert.Equal(price, response.EurPalletValue);
        Assert.Equal(7, response.PalletFormatId);
        Assert.True(response.EurPallet);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CustomerOrderCreateLinksExistingSupplierCostsWithoutDuplicatingThem(bool includeCostEdits)
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        var supplierOrder = new SupplierOrder { Id = 10, CompanyId = 1, SupplierOrderNr = "ABC-10" };
        var original = new OrderCost
        {
            Id = 20, SupplierOrderId = 10, CostId = 3, InPrice = 100,
            InPriceCurrencyId = 7, CalculationRowId = 4, QuotationId = 5,
            CreatedById = 42, Note = "Supplier cost",
        };
        var otherCost = new OrderCost { Id = 21, SupplierOrderId = 11, CostId = 3 };
        context.SupplierOrders.Add(supplierOrder);
        context.OrderCosts.AddRange(original, otherCost);
        await context.SaveChangesAsync();
        var controller = new CustomerOrdersController(context,
            NullLogger<CustomerOrdersController>.Instance,
            new UserResolution(new LegacyUser { Id = 42, CompanyId = 1 }));
        var request = new CreateCustomerOrderRequest { SupplierOrderId = 10 };
        if (includeCostEdits)
        {
            request.OrderCosts.Add(new UpsertCustomerOrderOrderCostRequest
            {
                Id = 20, SupplierOrderId = 10, CustomerOrderId = 999,
                CostId = 3, InPrice = 125, Note = "Edited cost",
            });
        }
        request.OrderCosts.Add(new UpsertCustomerOrderOrderCostRequest { CostId = 8, InPrice = 50 });

        var result = await controller.Create(request);

        var created = Assert.IsType<CustomerOrderDto>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.True(created.Id > 0);
        Assert.Equal(2, created.OrderCosts.Count);
        Assert.Equal(3, await context.OrderCosts.CountAsync());
        Assert.Equal(created.Id, original.CustomerOrderId);
        Assert.Equal(10, original.SupplierOrderId);
        Assert.Equal(includeCostEdits ? 125m : 100m, original.InPrice);
        Assert.Equal(7, original.InPriceCurrencyId);
        Assert.Equal(4, original.CalculationRowId);
        Assert.Equal(5, original.QuotationId);
        Assert.Equal(42, original.CreatedById);
        Assert.Null(otherCost.CustomerOrderId);
        var added = await context.OrderCosts.SingleAsync(x => x.CostId == 8);
        Assert.Equal(created.Id, added.CustomerOrderId);
        Assert.Equal(10, added.SupplierOrderId);

        var update = new UpdateCustomerOrderRequest
        {
            Id = created.Id,
            SupplierOrderId = 10,
            OrderCosts = created.OrderCosts.Select(cost => new UpsertCustomerOrderOrderCostRequest
            {
                Id = cost.Id, CustomerOrderId = null, SupplierOrderId = cost.SupplierOrderId,
                CostId = cost.CostId, InPrice = cost.InPrice,
            }).ToList(),
        };
        await controller.Update(created.Id, update);
        context.ChangeTracker.Clear();
        Assert.Equal(2, await context.OrderCosts.CountAsync(x => x.CustomerOrderId == created.Id));
        Assert.Equal(3, await context.OrderCosts.CountAsync());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CustomerOrderResponseUsesLinkedSupplierOrderPurchaseValues(
        bool hasSupplierOrder, bool hasValues)
    {
        var supplierOrder = hasSupplierOrder ? new SupplierOrder
        {
            ProducedEdition = hasValues ? 0 : null,
            PurchasePrice = hasValues ? 125.5 : null,
            PurchaseCurrencyId = hasValues ? 2 : null,
            PurchaseCurrency = hasValues ? new Currency { Name = "EUR", RateToSek = 99 } : null,
            PurchaseCurrencyRate = hasValues ? 11.25 : null,
            Unit = hasValues ? new Unit { Multiplicator = 100 } : null,
        } : null;
        var order = new CustomerOrder
        {
            Edition = 30000,
            SalesPrice = 3127,
            SalesCurrency = new Currency { Name = "SEK" },
            SalesCurrencyRate = 1,
            Unit = new Unit { Multiplicator = 1000 },
            SupplierOrder = supplierOrder,
        };
        var mapToDto = typeof(CustomerOrdersController).GetMethod(
            "MapToDto", BindingFlags.NonPublic | BindingFlags.Static)!;

        var response = Assert.IsType<CustomerOrderDto>(mapToDto.Invoke(null, [order]));

        Assert.Equal(supplierOrder?.ProducedEdition, response.SupplierOrderProducedEdition);
        Assert.Equal(supplierOrder?.Unit?.Multiplicator, response.SupplierOrderUnitMultiplicator);
        Assert.Equal(supplierOrder?.PurchasePrice, response.SupplierOrderPurchasePrice);
        Assert.Equal(supplierOrder?.PurchaseCurrencyId, response.SupplierOrderPurchaseCurrencyId);
        Assert.Equal(supplierOrder?.PurchaseCurrency?.Name, response.SupplierOrderPurchaseCurrencyName);
        Assert.Equal(supplierOrder?.PurchaseCurrencyRate, response.SupplierOrderPurchaseCurrencyRate);
        Assert.Equal(order.Edition, response.Edition);
        Assert.Equal(order.SalesPrice, response.SalesPrice);
        Assert.Equal(order.SalesCurrencyRate, response.SalesCurrencyRate);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    [InlineData(null)]
    public void CustomerOrderResponseIncludesUnitMultiplicator(int? multiplicator)
    {
        var order = new CustomerOrder
        {
            Unit = multiplicator.HasValue
                ? new Unit { Name = "Unit", Multiplicator = (short)multiplicator.Value }
                : null,
        };
        var mapToDto = typeof(CustomerOrdersController).GetMethod(
            "MapToDto", BindingFlags.NonPublic | BindingFlags.Static)!;

        var response = Assert.IsType<CustomerOrderDto>(mapToDto.Invoke(null, [order]));

        Assert.Equal(order.Unit?.Multiplicator, response.UnitMultiplicator);
    }

    [Fact]
    public void OrdersMapTheExistingRequiredCompanyColumn()
    {
        using var context = CreateContext();
        foreach (var entityType in new[] { typeof(CustomerOrder), typeof(SupplierOrder) })
        {
            var entity = context.Model.FindEntityType(entityType)!;
            var property = entity.FindProperty("CompanyId");

            Assert.NotNull(property);
            Assert.False(property.IsNullable);
            Assert.Equal("CompanyId", property.GetColumnName());
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public async Task CustomerOrderCreateAssignsTheAuthenticatedUsersCompanyBeforeSaving(int companyId)
    {
        var interceptor = new StopBeforeSaveInterceptor();
        using var context = CreateContext(interceptor);
        var controller = new CustomerOrdersController(
            context,
            NullLogger<CustomerOrdersController>.Instance,
            new UserResolution(new LegacyUser { Id = 42, CompanyId = companyId }));

        await Assert.ThrowsAsync<SaveInterceptedException>(
            () => controller.Create(new CreateCustomerOrderRequest()));

        var order = Assert.Single(context.ChangeTracker.Entries<CustomerOrder>()).Entity;
        Assert.Equal(companyId, order.CompanyId);
        Assert.Equal(42, order.CreatedBy);
        Assert.Equal(42, order.EditedBy);
    }

    [Theory]
    [InlineData("ApplyCreateRequestToEntity", false, false)]
    [InlineData("ApplyCreateRequestToEntity", true, false)]
    [InlineData("ApplyCreateRequestToEntity", false, true)]
    [InlineData("ApplyUpdateRequestToEntity", false, false)]
    [InlineData("ApplyUpdateRequestToEntity", true, false)]
    [InlineData("ApplyUpdateRequestToEntity", false, true)]
    public void CustomerOrderRequestsRoundTripDeliveryDateAndWeekMode(
        string mappingMethod, bool weekMode, bool clearDate)
    {
        var order = new CustomerOrder
        {
            DeliveryDate = new DateTime(2026, 5, 22),
            DeliveryDateWeekMode = true,
        };
        CreateCustomerOrderRequest request = mappingMethod == "ApplyCreateRequestToEntity"
            ? new CreateCustomerOrderRequest()
            : new UpdateCustomerOrderRequest();
        request.DeliveryDate = clearDate ? null : new DateTime(2026, 10, 19);
        request.DeliveryDateWeekMode = weekMode;

        var applyRequest = typeof(CustomerOrdersController).GetMethod(
            mappingMethod, BindingFlags.NonPublic | BindingFlags.Static)!;
        applyRequest.Invoke(null, [order, request]);

        Assert.Equal(request.DeliveryDate, order.DeliveryDate);
        Assert.Equal(weekMode, order.DeliveryDateWeekMode);

        var mapToDto = typeof(CustomerOrdersController).GetMethod(
            "MapToDto", BindingFlags.NonPublic | BindingFlags.Static)!;
        var response = Assert.IsType<CustomerOrderDto>(mapToDto.Invoke(null, [order]));
        Assert.Equal(request.DeliveryDate, response.DeliveryDate);
        Assert.Equal(weekMode, response.DeliveryDateWeekMode);
    }

    [Theory]
    [InlineData("ApplyCreateRequestToEntity", "Logistics\nSecond line", "Invoice\nSecond line")]
    [InlineData("ApplyCreateRequestToEntity", null, null)]
    [InlineData("ApplyUpdateRequestToEntity", "Logistics\nSecond line", "Invoice\nSecond line")]
    [InlineData("ApplyUpdateRequestToEntity", null, null)]
    public void CustomerOrderRequestsRoundTripLogisticsAndInvoicingInfo(
        string mappingMethod, string? logisticsInfo, string? invoicingInfo)
    {
        var order = new CustomerOrder
        {
            LogisticsInfoInternal = "Previous logistics info",
            InvoicingInfo = "Previous invoicing info",
        };
        CreateCustomerOrderRequest request = mappingMethod == "ApplyCreateRequestToEntity"
            ? new CreateCustomerOrderRequest()
            : new UpdateCustomerOrderRequest();
        request.LogisticsInfoInternal = logisticsInfo;
        request.InvoicingInfo = invoicingInfo;

        var applyRequest = typeof(CustomerOrdersController).GetMethod(
            mappingMethod, BindingFlags.NonPublic | BindingFlags.Static)!;
        applyRequest.Invoke(null, [order, request]);

        Assert.Equal(logisticsInfo, order.LogisticsInfoInternal);
        Assert.Equal(invoicingInfo, order.InvoicingInfo);

        var mapToDto = typeof(CustomerOrdersController).GetMethod(
            "MapToDto", BindingFlags.NonPublic | BindingFlags.Static)!;
        var response = Assert.IsType<CustomerOrderDto>(mapToDto.Invoke(null, [order]));
        Assert.Equal(logisticsInfo, response.LogisticsInfoInternal);
        Assert.Equal(invoicingInfo, response.InvoicingInfo);
    }

    [Theory]
    [InlineData("ApplyCreateRequestToEntity", false, false)]
    [InlineData("ApplyCreateRequestToEntity", true, false)]
    [InlineData("ApplyCreateRequestToEntity", false, true)]
    [InlineData("ApplyCreateRequestToEntity", true, true)]
    [InlineData("ApplyUpdateRequestToEntity", false, false)]
    [InlineData("ApplyUpdateRequestToEntity", true, false)]
    [InlineData("ApplyUpdateRequestToEntity", false, true)]
    [InlineData("ApplyUpdateRequestToEntity", true, true)]
    public void CustomerOrderRequestsRoundTripCompletionStatusAndDate(
        string mappingMethod, bool completed, bool clearDate)
    {
        var order = new CustomerOrder
        {
            Completed = !completed,
            CompletedDate = new DateTime(2026, 5, 22),
        };
        CreateCustomerOrderRequest request = mappingMethod == "ApplyCreateRequestToEntity"
            ? new CreateCustomerOrderRequest()
            : new UpdateCustomerOrderRequest();
        request.Completed = completed;
        request.CompletedDate = clearDate ? null : new DateTime(2026, 10, 15);

        var applyRequest = typeof(CustomerOrdersController).GetMethod(
            mappingMethod, BindingFlags.NonPublic | BindingFlags.Static)!;
        applyRequest.Invoke(null, [order, request]);

        Assert.Equal(completed, order.Completed);
        Assert.Equal(request.CompletedDate, order.CompletedDate);

        var mapToDto = typeof(CustomerOrdersController).GetMethod(
            "MapToDto", BindingFlags.NonPublic | BindingFlags.Static)!;
        var response = Assert.IsType<CustomerOrderDto>(mapToDto.Invoke(null, [order]));
        Assert.Equal(completed, response.Completed);
        Assert.Equal(request.CompletedDate, response.CompletedDate);
    }

    [Theory]
    [InlineData(1, null)]
    [InlineData(7, 123)]
    public async Task SupplierOrderCreateAssignsTheAuthenticatedUsersCompanyBeforeSaving(
        int companyId, int? basedOnSupplierOrderId)
    {
        var interceptor = new StopBeforeSaveInterceptor();
        var databaseName = Guid.NewGuid().ToString();
        var databaseRoot = new InMemoryDatabaseRoot();
        using (var seedContext = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName, databaseRoot).Options))
        {
            seedContext.Customers.Add(new Customer { Id = 1 });
            await seedContext.SaveChangesAsync();
        }
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName, databaseRoot)
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .AddInterceptors(interceptor).Options);
        var controller = new SupplierOrdersController(
            context,
            NullLogger<SupplierOrdersController>.Instance,
            new UserResolution(new LegacyUser { Id = 42, CompanyId = companyId }),
            new PalletFormatOptionsService(context));

        await Assert.ThrowsAsync<SaveInterceptedException>(
            () => controller.Create(new CreateSupplierOrderRequest
            {
                CustomerId = 1,
                BasedOnSupplierOrderId = basedOnSupplierOrderId,
            }));

        var order = Assert.Single(context.ChangeTracker.Entries<SupplierOrder>()).Entity;
        Assert.Equal(companyId, order.CompanyId);
        Assert.Equal(basedOnSupplierOrderId, order.BasedOnSupplierOrderId);
        Assert.Equal(42, order.CreatedBy);
        Assert.Equal(42, order.EditedBy);
    }

    [Theory]
    [InlineData("ABC-", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("ABC-", 123)]
    public async Task SupplierOrderCreatePersistsCustomerPrefixAndGeneratedId(
        string? prefix, int? basedOnSupplierOrderId)
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        context.Customers.Add(new Customer { Id = 1, OrderNrPrefix = prefix });
        context.SupplierOrders.Add(new SupplierOrder { Id = 123, SupplierOrderNr = "ORIGINAL" });
        await context.SaveChangesAsync();
        var controller = new SupplierOrdersController(context,
            NullLogger<SupplierOrdersController>.Instance,
            new UserResolution(new LegacyUser { Id = 42, CompanyId = 7 }),
            new PalletFormatOptionsService(context));

        var result = await controller.Create(new CreateSupplierOrderRequest
        {
            CustomerId = 1,
            BasedOnSupplierOrderId = basedOnSupplierOrderId,
            SupplierOrderNr = "Must not copy the source number",
        });

        var created = Assert.IsType<SupplierOrderDto>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.True(created.Id > 123);
        Assert.Equal(prefix + created.Id, created.SupplierOrderNr);
        context.ChangeTracker.Clear();
        var persisted = await context.SupplierOrders.SingleAsync(x => x.Id == created.Id);
        Assert.Equal(7, persisted.CompanyId);
        Assert.Equal(created.SupplierOrderNr, persisted.SupplierOrderNr);
        Assert.Equal("ORIGINAL", (await context.SupplierOrders.SingleAsync(x => x.Id == 123)).SupplierOrderNr);
    }

    [Theory]
    [InlineData(null, "ABC-50")]
    [InlineData("", "ABC-50")]
    [InlineData("OLD-50", "OLD-50")]
    public async Task SupplierOrderUpdateFillsMissingNumberAndPreservesExistingNumber(
        string? existingNumber, string expectedNumber)
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        context.Customers.Add(new Customer { Id = 1, OrderNrPrefix = "ABC-" });
        context.SupplierOrders.Add(new SupplierOrder { Id = 50, CustomerId = 1, SupplierOrderNr = existingNumber });
        await context.SaveChangesAsync();
        var controller = new SupplierOrdersController(context,
            NullLogger<SupplierOrdersController>.Instance,
            new UserResolution(new LegacyUser { Id = 42, CompanyId = 7 }),
            new PalletFormatOptionsService(context));

        var result = await controller.Update(50, new UpdateSupplierOrderRequest { Id = 50 });

        var updated = Assert.IsType<SupplierOrderDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(expectedNumber, updated.SupplierOrderNr);
        context.ChangeTracker.Clear();
        Assert.Equal(expectedNumber, (await context.SupplierOrders.SingleAsync()).SupplierOrderNr);
    }

    [Fact]
    public async Task SupplierOrderUpdatePersistsAndReturnsOrderCostCurrency()
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        context.SupplierOrders.Add(new SupplierOrder { Id = 50, SupplierOrderNr = "TEST-50" });
        context.OrderCosts.Add(new OrderCost
        {
            Id = 10,
            SupplierOrderId = 50,
            CostId = 3,
            InPriceCurrencyId = 3,
        });
        await context.SaveChangesAsync();
        var controller = new SupplierOrdersController(context,
            NullLogger<SupplierOrdersController>.Instance,
            new UserResolution(new LegacyUser { Id = 42, CompanyId = 7 }),
            new PalletFormatOptionsService(context));
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var updateRequest = JsonSerializer.Deserialize<UpdateSupplierOrderRequest>(
            """{"id":50,"orderCosts":[{"id":10,"costId":3,"inPriceCurrencyId":7}]}""",
            options);
        Assert.NotNull(updateRequest);

        var updateResult = await controller.Update(50, updateRequest);

        var updated = Assert.IsType<SupplierOrderDto>(Assert.IsType<OkObjectResult>(updateResult.Result).Value);
        Assert.Equal(7, Assert.Single(updated.OrderCosts).InPriceCurrencyId);
        context.ChangeTracker.Clear();
        Assert.Equal(7, (await context.OrderCosts.SingleAsync()).InPriceCurrencyId);

        var clearRequest = JsonSerializer.Deserialize<UpdateSupplierOrderRequest>(
            """{"id":50,"orderCosts":[{"id":10,"costId":3,"inPriceCurrencyId":null}]}""",
            options);
        Assert.NotNull(clearRequest);

        var clearResult = await controller.Update(50, clearRequest);

        var cleared = Assert.IsType<SupplierOrderDto>(Assert.IsType<OkObjectResult>(clearResult.Result).Value);
        Assert.Null(Assert.Single(cleared.OrderCosts).InPriceCurrencyId);
        context.ChangeTracker.Clear();
        Assert.Null((await context.OrderCosts.SingleAsync()).InPriceCurrencyId);
    }

    [Fact]
    public async Task SupplierOrderCreatePersistsOrderCostCurrency()
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        context.Customers.Add(new Customer { Id = 1, OrderNrPrefix = "TEST-" });
        await context.SaveChangesAsync();
        var controller = new SupplierOrdersController(context,
            NullLogger<SupplierOrdersController>.Instance,
            new UserResolution(new LegacyUser { Id = 42, CompanyId = 7 }),
            new PalletFormatOptionsService(context));

        var result = await controller.Create(new CreateSupplierOrderRequest
        {
            CustomerId = 1,
            OrderCosts =
            [
                new UpsertSupplierOrderOrderCostRequest
                {
                    CostId = 3,
                    InPriceCurrencyId = 7,
                },
            ],
        });

        var created = Assert.IsType<SupplierOrderDto>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        var createdOrderCost = Assert.Single(created.OrderCosts);
        Assert.Equal(7, createdOrderCost.InPriceCurrencyId);
        Assert.Equal(created.Id, createdOrderCost.SupplierOrderId);
        context.ChangeTracker.Clear();
        var persistedOrderCost = Assert.Single(await context.OrderCosts.ToListAsync());
        Assert.Equal(7, persistedOrderCost.InPriceCurrencyId);
        Assert.Equal(created.Id, persistedOrderCost.SupplierOrderId);
    }

    [Fact]
    public async Task SupplierOrderUpdatePreservesOrderCostCurrencyWhenOmitted()
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        context.SupplierOrders.Add(new SupplierOrder { Id = 50, SupplierOrderNr = "TEST-50" });
        context.OrderCosts.Add(new OrderCost
        {
            Id = 10,
            SupplierOrderId = 50,
            CostId = 3,
            InPriceCurrencyId = 7,
        });
        await context.SaveChangesAsync();
        var controller = new SupplierOrdersController(context,
            NullLogger<SupplierOrdersController>.Instance,
            new UserResolution(new LegacyUser { Id = 42, CompanyId = 7 }),
            new PalletFormatOptionsService(context));
        var request = JsonSerializer.Deserialize<UpdateSupplierOrderRequest>(
            """{"id":50,"orderCosts":[{"id":10,"costId":3}]}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(request);

        var result = await controller.Update(50, request);

        var updated = Assert.IsType<SupplierOrderDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(7, Assert.Single(updated.OrderCosts).InPriceCurrencyId);
        context.ChangeTracker.Clear();
        Assert.Equal(7, (await context.OrderCosts.SingleAsync()).InPriceCurrencyId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(999)]
    public async Task SupplierOrderCreateRejectsMissingCustomerWithoutSaving(int? customerId)
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var controller = new SupplierOrdersController(context,
            NullLogger<SupplierOrdersController>.Instance,
            new UserResolution(new LegacyUser { Id = 42, CompanyId = 7 }),
            new PalletFormatOptionsService(context));

        var result = await controller.Create(new CreateSupplierOrderRequest { CustomerId = customerId });

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(await context.SupplierOrders.ToListAsync());
    }

    [Theory]
    [InlineData(8)]
    [InlineData(0)]
    [InlineData(null)]
    public async Task SupplierOrderCreateSavesAndReturnsPalletCount(int? nrOfPallets)
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);
        context.Customers.Add(new Customer { Id = 1 });
        await context.SaveChangesAsync();
        var controller = new SupplierOrdersController(context,
            NullLogger<SupplierOrdersController>.Instance,
            new UserResolution(new LegacyUser { Id = 42, CompanyId = 7 }),
            new PalletFormatOptionsService(context));

        var result = await controller.Create(new CreateSupplierOrderRequest
        {
            CustomerId = 1,
            NrOfPallets = nrOfPallets,
        });

        var created = Assert.IsType<SupplierOrderDto>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.Equal(nrOfPallets, created.NrOfPallets);
        context.ChangeTracker.Clear();
        var loaded = await controller.GetById(created.Id);
        var response = Assert.IsType<SupplierOrderDto>(Assert.IsType<OkObjectResult>(loaded.Result).Value);
        Assert.Equal(nrOfPallets, response.NrOfPallets);
        Assert.Equal(nrOfPallets, (await context.SupplierOrders.SingleAsync()).NrOfPallets);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(0)]
    [InlineData(null)]
    public async Task SupplierOrderUpdateSavesAndReturnsPalletCount(int? nrOfPallets)
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        context.SupplierOrders.Add(new SupplierOrder
        {
            Id = 50, SupplierOrderNr = "TEST-50", ProducedEdition = 2700, NrOfPallets = 4,
        });
        await context.SaveChangesAsync();
        var controller = new SupplierOrdersController(context,
            NullLogger<SupplierOrdersController>.Instance,
            new UserResolution(new LegacyUser { Id = 42, CompanyId = 7 }),
            new PalletFormatOptionsService(context));
        var json = JsonSerializer.Serialize(new { id = 50, producedEdition = 2700, nrOfPallets });
        var request = JsonSerializer.Deserialize<UpdateSupplierOrderRequest>(
            json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(request);

        var result = await controller.Update(50, request);

        var updated = Assert.IsType<SupplierOrderDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(nrOfPallets, updated.NrOfPallets);
        Assert.Equal(2700, updated.ProducedEdition);
        context.ChangeTracker.Clear();
        var loaded = await controller.GetById(50);
        var response = Assert.IsType<SupplierOrderDto>(Assert.IsType<OkObjectResult>(loaded.Result).Value);
        Assert.Equal(nrOfPallets, response.NrOfPallets);
        Assert.Equal(nrOfPallets, (await context.SupplierOrders.SingleAsync()).NrOfPallets);
    }

    [Fact]
    public async Task SupplierOrderUpdatePreservesPalletCountWhenOlderClientOmitsIt()
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        context.SupplierOrders.Add(new SupplierOrder { Id = 50, SupplierOrderNr = "TEST-50", NrOfPallets = 4 });
        await context.SaveChangesAsync();
        var controller = new SupplierOrdersController(context,
            NullLogger<SupplierOrdersController>.Instance,
            new UserResolution(new LegacyUser { Id = 42, CompanyId = 7 }),
            new PalletFormatOptionsService(context));

        var result = await controller.Update(50, new UpdateSupplierOrderRequest { Id = 50 });

        var updated = Assert.IsType<SupplierOrderDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(4, updated.NrOfPallets);
        context.ChangeTracker.Clear();
        Assert.Equal(4, (await context.SupplierOrders.SingleAsync()).NrOfPallets);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SupplierOrderRejectsNegativePalletCountWithoutSaving(bool create)
    {
        using var context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        context.SupplierOrders.Add(new SupplierOrder { Id = 50, SupplierOrderNr = "TEST-50", NrOfPallets = 4 });
        await context.SaveChangesAsync();
        var controller = new SupplierOrdersController(context,
            NullLogger<SupplierOrdersController>.Instance,
            new UserResolution(new LegacyUser { Id = 42, CompanyId = 7 }),
            new PalletFormatOptionsService(context));

        var result = create
            ? await controller.Create(new CreateSupplierOrderRequest { NrOfPallets = -1 })
            : await controller.Update(50, new UpdateSupplierOrderRequest { Id = 50, NrOfPallets = -1 });

        Assert.IsType<BadRequestObjectResult>(result.Result);
        context.ChangeTracker.Clear();
        Assert.Equal(4, (await context.SupplierOrders.SingleAsync()).NrOfPallets);
    }

    private static ApplicationDbContext CreateContext(params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelMappingOnly;Integrated Security=true")
            .AddInterceptors(interceptors)
            .Options);

    private sealed class UserResolution(LegacyUser user) : ILegacyUserResolutionService
    {
        public Task<LegacyUser?> ResolveCurrentUserAsync(ClaimsPrincipal principal) =>
            Task.FromResult<LegacyUser?>(user);
    }

    private sealed class SaveInterceptedException : Exception { }

    private sealed class StopBeforeSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            throw new SaveInterceptedException();
    }
}
