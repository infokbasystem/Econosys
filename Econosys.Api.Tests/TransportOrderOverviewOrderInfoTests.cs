using System.Text.Json;
using Econosys.Api.Controllers;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Migrations;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

namespace Econosys.Api.Tests;

public class TransportOrderOverviewOrderInfoTests
{
    private static ApplicationDbContext CreateDb() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static LogisticsController CreateController(ApplicationDbContext db) =>
        new(db, new LegacyUserResolutionService(db), null!);

    [Fact]
    public void MigrationAddsAndRemovesNullablePalletCount()
    {
        var migration = new AddSupplierOrderNrOfPallets();
        var addColumn = Assert.IsType<AddColumnOperation>(Assert.Single(migration.UpOperations));
        Assert.Equal("SupplierOrder", addColumn.Table);
        Assert.Equal("NrOfPallets", addColumn.Name);
        Assert.Equal(typeof(int), addColumn.ClrType);
        Assert.True(addColumn.IsNullable);

        var dropColumn = Assert.IsType<DropColumnOperation>(Assert.Single(migration.DownOperations));
        Assert.Equal("SupplierOrder", dropColumn.Table);
        Assert.Equal("NrOfPallets", dropColumn.Name);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(0)]
    [InlineData(null)]
    public async Task ReportedPalletsAreSavedOnSupplierOrderAndReturnedWhenReopened(int? nrOfPallets)
    {
        await using var db = CreateDb();
        db.SupplierOrders.Add(new SupplierOrder { Id = 1, Edition = 2700, ProducedEdition = 2600, NrOfPallets = 4 });
        db.CustomerOrders.Add(new CustomerOrder { Id = 2, SupplierOrderId = 1 });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var initial = await controller.GetOrderInfo(2);
        var initialInfo = Assert.IsType<TransportOrderOverviewOrderInfoDto>(Assert.IsType<OkObjectResult>(initial.Result).Value);
        Assert.Equal(4, initialInfo.NrOfPallets);

        var json = JsonSerializer.Serialize(new { producedEdition = 2672, nrOfPallets, isCompleted = true });
        var request = JsonSerializer.Deserialize<SaveTransportOrderOverviewOrderInfoDto>(
            json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(request);
        var result = await controller.SaveOrderInfo(2, request);

        Assert.IsType<NoContentResult>(result);
        db.ChangeTracker.Clear();
        var reopened = await controller.GetOrderInfo(2);
        var info = Assert.IsType<TransportOrderOverviewOrderInfoDto>(Assert.IsType<OkObjectResult>(reopened.Result).Value);
        Assert.Equal(2700, info.OrderedEdition);
        Assert.Equal(2672, info.ProducedEdition);
        Assert.Equal(nrOfPallets, info.NrOfPallets);
        Assert.True(info.IsCompleted);
        Assert.Equal(nrOfPallets, (await db.SupplierOrders.SingleAsync()).NrOfPallets);
    }

    [Fact]
    public async Task LegacyRequestWithoutPalletCountPreservesTheExistingValue()
    {
        await using var db = CreateDb();
        db.SupplierOrders.Add(new SupplierOrder { Id = 1, ProducedEdition = 100, NrOfPallets = 4 });
        db.CustomerOrders.Add(new CustomerOrder { Id = 2, SupplierOrderId = 1 });
        await db.SaveChangesAsync();
        var request = JsonSerializer.Deserialize<SaveTransportOrderOverviewOrderInfoDto>(
            """{"producedEdition":200,"isCompleted":true}""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(request);

        Assert.IsType<NoContentResult>(await CreateController(db).SaveOrderInfo(2, request));
        db.ChangeTracker.Clear();
        var supplierOrder = await db.SupplierOrders.SingleAsync();
        Assert.Equal(200, supplierOrder.ProducedEdition);
        Assert.Equal(4, supplierOrder.NrOfPallets);
        Assert.True((await db.CustomerOrders.SingleAsync()).Completed);
    }

    [Theory]
    [InlineData(8, 100, 8d)]
    [InlineData(0, 100, 0d)]
    [InlineData(8, null, 8d)]
    [InlineData(null, 100, 7.5d)]
    [InlineData(null, 0, null)]
    [InlineData(null, null, null)]
    public async Task TopTableUsesReportedPalletCountOrFallsBackToCalculatedBalance(
        int? nrOfPallets, int? editionPerPallet, double? expectedPallets)
    {
        await using var db = CreateDb();
        db.SupplierOrders.Add(new SupplierOrder
        {
            Id = 1, Edition = 1000, ProducedEdition = 900,
            EconopackTransportResponsible = true, NrOfPallets = nrOfPallets,
        });
        db.CalculationRows.Add(new CalculationRow { Id = 3, ArchivedEditionPerPallet = editionPerPallet });
        db.CustomerOrders.Add(new CustomerOrder { Id = 2, SupplierOrderId = 1, SelectedCalculationRowId = 3 });
        db.DeliveryToCustomers.Add(new DeliveryToCustomer { Id = 4, SupplierOrderId = 1, NrOfItems = 150 });
        await db.SaveChangesAsync();

        var result = await CreateController(db).GetTopTable();
        var rows = Assert.IsType<List<TransportOrderOverviewTopTableRowDto>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        var row = Assert.Single(rows);
        Assert.Equal(expectedPallets, row.Pallets);
        Assert.Equal(750, row.Rest);
    }

    [Fact]
    public async Task NegativePalletCountDoesNotChangeTheOrder()
    {
        await using var db = CreateDb();
        db.SupplierOrders.Add(new SupplierOrder { Id = 1, ProducedEdition = 100, NrOfPallets = 4 });
        db.CustomerOrders.Add(new CustomerOrder { Id = 2, SupplierOrderId = 1 });
        await db.SaveChangesAsync();

        var result = await CreateController(db).SaveOrderInfo(2, new SaveTransportOrderOverviewOrderInfoDto
        {
            ProducedEdition = 200,
            NrOfPallets = -1,
            IsCompleted = true,
        });

        Assert.IsType<BadRequestObjectResult>(result);
        db.ChangeTracker.Clear();
        var supplierOrder = await db.SupplierOrders.SingleAsync();
        Assert.Equal(100, supplierOrder.ProducedEdition);
        Assert.Equal(4, supplierOrder.NrOfPallets);
        Assert.False((await db.CustomerOrders.SingleAsync()).Completed);
    }

    [Fact]
    public async Task ReportingPalletsWithoutASupplierOrderReturnsAnError()
    {
        await using var db = CreateDb();
        db.CustomerOrders.Add(new CustomerOrder { Id = 2 });
        await db.SaveChangesAsync();

        var result = await CreateController(db).SaveOrderInfo(2, new SaveTransportOrderOverviewOrderInfoDto
        {
            NrOfPallets = 3,
            IsCompleted = true,
        });

        Assert.IsType<BadRequestObjectResult>(result);
        db.ChangeTracker.Clear();
        Assert.False((await db.CustomerOrders.SingleAsync()).Completed);
    }
}
