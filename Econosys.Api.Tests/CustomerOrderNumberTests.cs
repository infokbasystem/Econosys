using System.Security.Claims;
using Econosys.Api.Controllers;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Econosys.Api.Tests;

public class CustomerOrderNumberTests
{
    [Theory]
    [InlineData("CUSTOMER-PO")]
    [InlineData(null)]
    public async Task CustomerPurchaseOrderIsSavedSeparatelyFromGeneratedOrderNumber(string? purchaseOrder)
    {
        using var db = CreateContext();
        db.SupplierOrders.Add(new SupplierOrder { Id = 50, SupplierOrderNr = "ABC-50" });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.Create(new CreateCustomerOrderRequest
        {
            SupplierOrderId = 50, YourOrderNr = purchaseOrder,
        });
        var created = Assert.IsType<CustomerOrderDto>(
            Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.Equal("ABC-50", created.CustomerOrderNr);
        Assert.Equal(purchaseOrder, created.YourOrderNr);
        db.ChangeTracker.Clear();
        var saved = await db.CustomerOrders.SingleAsync();
        Assert.Equal(purchaseOrder, saved.YourOrderNr);

        foreach (var updatedNumber in new[] { "CHANGED-PO", null })
        {
            var updated = await controller.Update(created.Id, new UpdateCustomerOrderRequest
            {
                Id = created.Id, CustomerOrderNr = created.CustomerOrderNr, YourOrderNr = updatedNumber,
            });
            var dto = Assert.IsType<CustomerOrderDto>(Assert.IsType<OkObjectResult>(updated.Result).Value);
            Assert.Equal(updatedNumber, dto.YourOrderNr);
            Assert.Equal("ABC-50", dto.CustomerOrderNr);
            db.ChangeTracker.Clear();
            saved = await db.CustomerOrders.SingleAsync();
            Assert.Equal(updatedNumber, saved.YourOrderNr);
            Assert.Equal("ABC-50", saved.CustomerOrderNr);
        }
    }

    [Fact]
    public void CustomerPurchaseOrderMapsToTheLegacyColumn()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=ModelMappingOnly;Integrated Security=true").Options);

        Assert.Equal("strYourOrderNr", db.Model.FindEntityType(typeof(CustomerOrder))!
            .FindProperty(nameof(CustomerOrder.YourOrderNr))!.GetColumnName());
    }

    [Theory]
    [InlineData(0, null, "ABC-50")]
    [InlineData(1, "", "ABC-50-2")]
    [InlineData(2, "CLIENT-NUMBER", "ABC-50-3")]
    public async Task CreationPersistsLegacyNumberAndReturnsItInDetailsAndSearch(
        int previousOrders, string? requestedNumber, string expectedNumber)
    {
        using var db = CreateContext();
        db.SupplierOrders.Add(new SupplierOrder { Id = 50, SupplierOrderNr = "ABC-50" });
        db.CustomerOrders.Add(new CustomerOrder { Id = 10, SupplierOrderId = 51, CustomerOrderNr = "OTHER" });
        db.CustomerOrders.AddRange(Enumerable.Range(1, previousOrders).Select(id =>
            new CustomerOrder { Id = id, SupplierOrderId = 50, CustomerOrderNr = $"EXISTING-{id}" }));
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.Create(new CreateCustomerOrderRequest
        {
            SupplierOrderId = 50, CustomerOrderNr = requestedNumber,
        });

        var created = Assert.IsType<CustomerOrderDto>(
            Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        Assert.Equal(expectedNumber, created.CustomerOrderNr);
        Assert.Equal("ABC-50", created.SupplierOrderNr);
        db.ChangeTracker.Clear();
        Assert.Equal(expectedNumber, (await db.CustomerOrders.SingleAsync(x => x.Id == created.Id)).CustomerOrderNr);
        var details = await controller.GetById(created.Id);
        Assert.Equal(expectedNumber, Assert.IsType<CustomerOrderDto>(
            Assert.IsType<OkObjectResult>(details.Result).Value).CustomerOrderNr);
        var search = await controller.Search(new SearchCustomerOrdersRequest());
        var page = Assert.IsType<PagedResultDto<CustomerOrderDto>>(
            Assert.IsType<OkObjectResult>(search.Result).Value);
        Assert.Equal(expectedNumber, Assert.Single(page.Items, x => x.Id == created.Id).CustomerOrderNr);
        for (var id = 1; id <= previousOrders; id++)
        {
            Assert.Equal($"EXISTING-{id}", (await db.CustomerOrders.FindAsync(id))!.CustomerOrderNr);
        }

        await controller.Update(created.Id, new UpdateCustomerOrderRequest
        {
            Id = created.Id, CustomerOrderNr = expectedNumber, Product = "Edited product",
        });
        db.ChangeTracker.Clear();
        Assert.Equal(expectedNumber, (await db.CustomerOrders.FindAsync(created.Id))!.CustomerOrderNr);
    }

    [Fact]
    public async Task RepeatedCreationUsesTheNextSuffix()
    {
        using var db = CreateContext();
        db.SupplierOrders.Add(new SupplierOrder { Id = 50, SupplierOrderNr = "ABC-50" });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        foreach (var number in new[] { "ABC-50", "ABC-50-2", "ABC-50-3" })
        {
            var result = await controller.Create(new CreateCustomerOrderRequest { SupplierOrderId = 50 });
            Assert.Equal(number, Assert.IsType<CustomerOrderDto>(
                Assert.IsType<CreatedAtActionResult>(result.Result).Value).CustomerOrderNr);
        }
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(true, "")]
    [InlineData(true, " ")]
    public async Task MissingSupplierOrNumberIsRejectedWithoutSaving(bool hasSupplier, string? number)
    {
        using var db = CreateContext();
        if (hasSupplier)
        {
            db.SupplierOrders.Add(new SupplierOrder { Id = 50, SupplierOrderNr = number });
            await db.SaveChangesAsync();
        }

        var result = await CreateController(db).Create(new CreateCustomerOrderRequest { SupplierOrderId = 50 });

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(await db.CustomerOrders.ToListAsync());
    }

    [Theory]
    [InlineData(48, true)]
    [InlineData(49, false)]
    public async Task GeneratedNumberRespectsTheLegacyColumnLength(int supplierNumberLength, bool succeeds)
    {
        using var db = CreateContext();
        var number = new string('A', supplierNumberLength);
        db.SupplierOrders.Add(new SupplierOrder { Id = 50, SupplierOrderNr = number });
        db.CustomerOrders.Add(new CustomerOrder { Id = 1, SupplierOrderId = 50, CustomerOrderNr = number });
        await db.SaveChangesAsync();

        var result = await CreateController(db).Create(new CreateCustomerOrderRequest { SupplierOrderId = 50 });

        if (succeeds)
        {
            Assert.Equal(number + "-2", Assert.IsType<CustomerOrderDto>(
                Assert.IsType<CreatedAtActionResult>(result.Result).Value).CustomerOrderNr);
        }
        else
        {
            Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Single(await db.CustomerOrders.ToListAsync());
        }
    }

    [Fact]
    public async Task CreationWithoutSupplierKeepsExistingExplicitNumberBehavior()
    {
        using var db = CreateContext();

        var result = await CreateController(db).Create(new CreateCustomerOrderRequest { CustomerOrderNr = "DIRECT" });

        Assert.Equal("DIRECT", Assert.IsType<CustomerOrderDto>(
            Assert.IsType<CreatedAtActionResult>(result.Result).Value).CustomerOrderNr);
    }

    [Theory]
    [InlineData("ABC-50-2", "ABC-50-2")]
    [InlineData(null, "")]
    public async Task ReportsDisplayStoredNumberNotDatabaseId(string? number, string expected)
    {
        using var db = CreateContext();
        db.SupplierOrders.Add(new SupplierOrder
        {
            Id = 50, SupplierOrderNr = "ABC-50", Inventory = new Inventory { Id = 1, IsInventory = true },
        });
        db.CustomerOrders.Add(new CustomerOrder { Id = 123, SupplierOrderId = 50, CustomerOrderNr = number });
        await db.SaveChangesAsync();
        var controller = new ReportingController(db);

        var revenue = await controller.GetRevenuePerOrderReport(null);
        var revenuePage = Assert.IsType<PagedResultDto<RevenuePerOrderReportRowDto>>(
            Assert.IsType<OkObjectResult>(revenue.Result).Value);
        var revenueRow = Assert.Single(revenuePage.Items);
        Assert.Equal(123, revenueRow.OrderId);
        Assert.Equal(expected, revenueRow.OrderNumber);

        var warehouse = await controller.GetNonDeliveredWarehouseOrdersReport(null);
        var warehousePage = Assert.IsType<PagedResultDto<NonDeliveredWarehouseOrdersReportRowDto>>(
            Assert.IsType<OkObjectResult>(warehouse.Result).Value);
        var warehouseRow = Assert.Single(warehousePage.Items);
        Assert.Equal(123, warehouseRow.Id);
        Assert.Equal(expected, warehouseRow.OrderNumber);
    }

    private static ApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    private static CustomerOrdersController CreateController(ApplicationDbContext db) =>
        new(db, NullLogger<CustomerOrdersController>.Instance, new UserResolution());

    private sealed class UserResolution : ILegacyUserResolutionService
    {
        public Task<LegacyUser?> ResolveCurrentUserAsync(ClaimsPrincipal principal) =>
            Task.FromResult<LegacyUser?>(new LegacyUser { Id = 42, CompanyId = 1 });
    }
}
