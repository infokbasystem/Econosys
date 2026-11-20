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

public class CustomerOrderPriceAdjustmentTests
{
    [Fact]
    public async Task UpdatesPricesAndRecalculatesTheSelectedCalculationRow()
    {
        await using var db = CreateContext();
        await AddOrder(db);

        var result = await CreateController(db).UpdatePrices(70, new UpdateCustomerOrderPricesRequest
        {
            PurchasePrice = 12,
            SalesPrice = 18,
        });

        Assert.IsType<OkObjectResult>(result.Result);
        db.ChangeTracker.Clear();
        var customerOrder = await db.CustomerOrders.SingleAsync();
        var supplierOrder = await db.SupplierOrders.SingleAsync();
        var calculationRow = await db.CalculationRows.SingleAsync();
        Assert.Equal(18d, customerOrder.SalesPrice);
        Assert.Equal(12d, supplierOrder.PurchasePrice);
        Assert.Equal(12d, calculationRow.PurchasePrice);
        Assert.Equal(18d, calculationRow.SalesPrice);
        Assert.Equal(50d, calculationRow.PercentAddition);
    }

    [Fact]
    public async Task CopiesCalculationRowWhenAnotherCustomerOrderUsesIt()
    {
        await using var db = CreateContext();
        await AddOrder(db);
        db.CustomerOrders.Add(new CustomerOrder
        {
            Id = 71,
            SupplierOrderId = 50,
            SelectedCalculationRowId = 60,
            SalesPrice = 20,
        });
        await db.SaveChangesAsync();

        var result = await CreateController(db).UpdatePrices(70, new UpdateCustomerOrderPricesRequest
        {
            PurchasePrice = 12,
            SalesPrice = 18,
        });

        Assert.IsType<OkObjectResult>(result.Result);
        db.ChangeTracker.Clear();
        var editedOrder = await db.CustomerOrders.SingleAsync(x => x.Id == 70);
        var otherOrder = await db.CustomerOrders.SingleAsync(x => x.Id == 71);
        var originalRow = await db.CalculationRows.SingleAsync(x => x.Id == 60);
        var adjustedRow = await db.CalculationRows.SingleAsync(x => x.Id == editedOrder.SelectedCalculationRowId);
        Assert.NotEqual(originalRow.Id, adjustedRow.Id);
        Assert.Equal(60, otherOrder.SelectedCalculationRowId);
        Assert.Equal(10d, originalRow.PurchasePrice);
        Assert.Equal(20d, originalRow.SalesPrice);
        Assert.Equal(12d, adjustedRow.PurchasePrice);
        Assert.Equal(18d, adjustedRow.SalesPrice);
    }

    [Fact]
    public async Task RejectsPriceChangesForUsersWithoutPermission()
    {
        await using var db = CreateContext();
        var result = await CreateController(db, authorizationLevel: 64).UpdatePrices(
            70,
            new UpdateCustomerOrderPricesRequest { PurchasePrice = 12, SalesPrice = 18 });

        Assert.IsType<ForbidResult>(result.Result);
        Assert.Empty(await db.CustomerOrders.ToListAsync());
    }

    [Fact]
    public async Task RejectsPriceChangesWhenArchivedCurrencyRatesAreMissing()
    {
        await using var db = CreateContext();
        await AddOrder(db, purchaseRate: null);

        var result = await CreateController(db).UpdatePrices(70, new UpdateCustomerOrderPricesRequest
        {
            PurchasePrice = 12,
            SalesPrice = 18,
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(10d, (await db.CalculationRows.SingleAsync()).PurchasePrice);
        Assert.Equal(20d, (await db.CustomerOrders.SingleAsync()).SalesPrice);
    }

    private static async Task AddOrder(ApplicationDbContext db, decimal? purchaseRate = 2m)
    {
        db.SupplierOrders.Add(new SupplierOrder { Id = 50, PurchasePrice = 10 });
        db.CalculationRows.Add(new CalculationRow
        {
            Id = 60,
            ArchivedPurchaseCurrencyRate = purchaseRate,
            ArchivedSalesCurrencyRate = 4m,
            PurchasePrice = 10,
            SalesPrice = 20,
            Freight = 1,
            Storage = 2,
            Other = 3,
        });
        db.CustomerOrders.Add(new CustomerOrder
        {
            Id = 70,
            SupplierOrderId = 50,
            SelectedCalculationRowId = 60,
            SalesPrice = 20,
        });
        await db.SaveChangesAsync();
    }

    private static ApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    private static CustomerOrdersController CreateController(ApplicationDbContext db, int authorizationLevel = 0) =>
        new(db, NullLogger<CustomerOrdersController>.Instance, new UserResolution(authorizationLevel));

    private sealed class UserResolution(int authorizationLevel) : ILegacyUserResolutionService
    {
        public Task<LegacyUser?> ResolveCurrentUserAsync(ClaimsPrincipal principal) =>
            Task.FromResult<LegacyUser?>(new LegacyUser
            {
                Id = 42,
                CompanyId = 1,
                AuthorizationLevel = authorizationLevel,
            });
    }
}
