using System.Reflection;
using Econosys.Api.Controllers;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Econosys.Api.Tests;

public class CalculationRowEditingTests
{
    private static ApplicationDbContext Context() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public void EveryBusinessScalarHasAnExplicitRoundTripMappingAndIdentityIsPreserved()
    {
        var immutable = new[] { "Id", "CompanyId", "CalculationId", "OldDbId" };
        var entityFields = typeof(CalculationRow).GetProperties()
            .Where(property => property.PropertyType.IsValueType || property.PropertyType == typeof(string))
            .Where(property => !immutable.Contains(property.Name)).OrderBy(property => property.Name).ToArray();
        var editFields = typeof(CalculationRowEditValues).GetProperties().OrderBy(property => property.Name).ToArray();
        Assert.Equal(entityFields.Select(property => property.Name), editFields.Select(property => property.Name));
        var original = new CalculationRow { Id = 41, CalculationId = 42, CompanyId = 43, OldDbId = 44 };
        foreach (var property in entityFields)
        {
            var type = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
            object value = type == typeof(bool) ? true : type == typeof(int) ? 11
                : type == typeof(double) ? 12.123456789 : type == typeof(decimal) ? 13.123m : "Stored value";
            property.SetValue(original, value);
        }

        var target = new CalculationRow { Id = 51, CalculationId = 52, CompanyId = 53, OldDbId = 54 };
        CalculationRowEditMapper.Apply(target, CalculationRowEditMapper.Map(original).Values);
        foreach (var property in entityFields)
            Assert.Equal(property.GetValue(original), property.GetValue(target));
        Assert.Equal((51, 52, 53, 54), (target.Id, target.CalculationId, target.CompanyId, target.OldDbId));

        CalculationRowEditMapper.Apply(target, new CalculationRowEditValues());
        foreach (var property in entityFields)
            Assert.Equal(property.PropertyType == typeof(bool) ? false : null, property.GetValue(target));
    }

    [Fact]
    public async Task ArchivedSupplierRowCanBeEditedWithoutChangingOrdersOtherRowsOrChildCosts()
    {
        using var db = Context();
        var selected = new CalculationRow { Id = 1, CalculationId = 4, CompanyId = 5, OldDbId = 6, Archived = true, Freight = 10 };
        var customerRow = new CalculationRow { Id = 2, Freight = 99 };
        var supplier = new SupplierOrder { Id = 10, SelectedCalculationRow = selected, PurchasePrice = 100, Edition = 1000 };
        var customer = new CustomerOrder { Id = 20, SupplierOrder = supplier, SelectedCalculationRow = customerRow, SalesPrice = 150, Edition = 1000 };
        var cost = new CalculationRowCost { Id = 30, CalculationRow = selected, InPrice = 25 };
        db.AddRange(supplier, customer, cost);
        await db.SaveChangesAsync();
        var controller = new SupplierOrderCalculationRowsController(db);
        var read = Assert.IsType<CalculationRowEditDto>(Assert.IsType<OkObjectResult>((await controller.Get(10, default)).Result).Value);
        Assert.Equal(1, read.Id);
        Assert.True(read.Values.Archived);
        var saved = await controller.Update(10, new UpdateLinkedCalculationRowRequest
        {
            ExpectedRowId = read.Id,
            Values = read.Values with { Freight = 123.456789, ArchivedPriceListPriceFtl = 1234.12345m, Info = null },
        }, default);
        Assert.IsType<OkObjectResult>(saved.Result);
        db.ChangeTracker.Clear();
        var reloaded = await db.CalculationRows.SingleAsync(item => item.Id == 1);
        Assert.True(reloaded.Archived);
        Assert.Equal(123.456789, reloaded.Freight);
        Assert.Equal(1234.12345m, reloaded.ArchivedPriceListPriceFtl);
        Assert.Equal((4, 5, 6), (reloaded.CalculationId, reloaded.CompanyId, reloaded.OldDbId));
        Assert.Equal(99, (await db.CalculationRows.SingleAsync(item => item.Id == 2)).Freight);
        Assert.Equal(25m, (await db.CalculationRowCosts.SingleAsync()).InPrice);
        var savedSupplier = await db.SupplierOrders.SingleAsync();
        var savedCustomer = await db.CustomerOrders.SingleAsync();
        Assert.Equal((100d, 1000, 1), (savedSupplier.PurchasePrice, savedSupplier.Edition, savedSupplier.SelectedCalculationRowId));
        Assert.Equal((150d, 1000, 2), (savedCustomer.SalesPrice, savedCustomer.Edition, savedCustomer.SelectedCalculationRowId));
    }

    [Fact]
    public async Task MissingOrdersLinksAndRowsReturnNotFound()
    {
        using var db = Context();
        db.SupplierOrders.AddRange(new SupplierOrder { Id = 1 }, new SupplierOrder { Id = 2, SelectedCalculationRowId = 999 });
        await db.SaveChangesAsync();
        var controller = new SupplierOrderCalculationRowsController(db);
        foreach (var id in new[] { 1, 2, 999 })
        {
            Assert.IsType<NotFoundObjectResult>((await controller.Get(id, default)).Result);
            Assert.IsType<NotFoundObjectResult>((await controller.Update(id, new UpdateLinkedCalculationRowRequest
            { ExpectedRowId = 1, Values = new() }, default)).Result);
        }
        Assert.Empty(db.CalculationRows);
    }

    [Fact]
    public async Task ChangedOrderSelectionReturnsConflictWithoutUpdatingEitherRow()
    {
        using var db = Context();
        db.AddRange(new CalculationRow { Id = 1, Freight = 10 },
            new SupplierOrder { Id = 10, SelectedCalculationRow = new CalculationRow { Id = 2, Freight = 20 } });
        await db.SaveChangesAsync();
        var result = await new SupplierOrderCalculationRowsController(db).Update(10, new()
        { ExpectedRowId = 1, Values = new() { Freight = 999 } }, default);
        Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal(new double?[] { 10, 20 }, await db.CalculationRows.OrderBy(item => item.Id).Select(item => item.Freight).ToArrayAsync());
    }

    [Theory]
    [InlineData("decimal-scale")]
    [InlineData("decimal-range")]
    [InlineData("non-finite")]
    [InlineData("text-length")]
    [InlineData("edition")]
    [InlineData("address")]
    [InlineData("factory")]
    [InlineData("pallet")]
    [InlineData("currency")]
    public async Task InvalidInputDoesNotSave(string scenario)
    {
        using var db = Context();
        db.Add(new SupplierOrder { Id = 10, CustomerId = 50, SupplierId = 60,
            SelectedCalculationRow = new CalculationRow { Id = 1, Freight = 10 } });
        db.Add(new CustomerDeliveryAddress { Id = 99, CustomerId = 51 });
        await db.SaveChangesAsync();
        var values = new CalculationRowEditValues { Freight = 999 };
        values = scenario switch
        {
            "decimal-scale" => values with { ArchivedPalletCalcFactor = 1.1234m },
            "decimal-range" => values with { ArchivedPriceListPriceFtl = 100000000m },
            "non-finite" => values with { PurchasePrice = double.NaN },
            "text-length" => values with { Info = new string('x', 51) },
            "edition" => values with { Edition = 0 },
            "address" => values with { CustomerDeliveryAddressId = 99 },
            "factory" => values with { ArchivedSupplierFactoryId = 99 },
            "pallet" => values with { ArchivedPalletFormatId = 99 },
            "currency" => values with { ArchivedPriceListCurrencyId = 1.5m },
            _ => throw new InvalidOperationException(),
        };
        var controller = new SupplierOrderCalculationRowsController(db);
        Assert.IsType<ObjectResult>((await controller.Update(10, new() { ExpectedRowId = 1, Values = values }, default)).Result);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal(10, (await db.CalculationRows.SingleAsync()).Freight);
    }

    [Fact]
    public async Task HistoryUsesSupplierSelectionEvenIfCustomerSelectionDiffers()
    {
        using var db = Context();
        var calculation = new Calculation { Id = 1 };
        var quotation = new Quotation { Id = 2, Calculation = calculation };
        var supplier = new SupplierOrder { Id = 3, PurchasePrice = 100, PurchaseCurrencyRate = 1,
            SelectedCalculationRow = new CalculationRow { Id = 4, Freight = 12, Other = 13, Storage = 14, PackagingCost = 15m, PercentAddition = 16 } };
        db.Add(quotation);
        db.Add(new CustomerOrder { Id = 5, QuotationId = quotation.Id, SupplierOrder = supplier, SalesCurrencyRate = 1,
            SelectedCalculationRow = new CalculationRow { Id = 6, Freight = 999 } });
        db.Add(new Inquiry { Id = 8, Calculation = calculation });
        db.Add(new SupplierOrder { Id = 7, InquiryId = 8,
            SelectedCalculationRow = new CalculationRow { Id = 9 } });
        await db.SaveChangesAsync();
        var controller = new CalculationsController(db, new LegacyUserResolutionService(db), new CalculationFreightPriceService(db));
        var result = await controller.GetHistory(1, default);
        var rows = Assert.IsAssignableFrom<IReadOnlyList<CalculationHistoryRowDto>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        var customerHistory = rows.Single(item => item.CustomerOrderId == 5);
        Assert.Equal(4, customerHistory.SelectedCalculationRowId);
        Assert.Equal(12, customerHistory.FreightCost);
        Assert.Equal(13, customerHistory.OtherCost);
        Assert.Equal(14, customerHistory.StorageCost);
        Assert.Equal(15m, customerHistory.PackagingCost);
        Assert.Equal(16, customerHistory.PercentAddition);
        Assert.Equal(154, customerHistory.PurchaseCostTotal);
        Assert.Equal(9, rows.Single(item => item.SupplierOrderId == 7).SelectedCalculationRowId);
    }

    [Fact]
    public void LinkedRowEndpointsRequireAuthentication()
    {
        Assert.NotNull(typeof(SupplierOrderCalculationRowsController).GetCustomAttribute<AuthorizeAttribute>());
    }

    [Fact]
    public async Task AggregateRowEditorStillRejectsArchivedRows()
    {
        using var db = Context();
        var calculation = new Calculation { Id = 1 };
        db.Add(calculation);
        db.Add(new CalculationRow { Id = 2, Calculation = calculation, Archived = true, Freight = 10 });
        await db.SaveChangesAsync();
        var controller = new CalculationsController(db, new LegacyUserResolutionService(db), new CalculationFreightPriceService(db));
        var method = typeof(CalculationsController).GetMethod("ApplyCalculationRowsAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var result = await (Task<string?>)method.Invoke(controller,
            [1, calculation, new UpdateCalculationRowRequest[] { new() { Id = 2, Freight = 999 } }, CancellationToken.None])!;
        Assert.Equal("Archived calculation rows cannot be edited.", result);
        Assert.Equal(10, (await db.CalculationRows.SingleAsync()).Freight);
    }
}
