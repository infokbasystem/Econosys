using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Econosys.Api.Tests;

public class DeliveryEditingTests
{
    private static ApplicationDbContext CreateDb() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Theory]
    [InlineData("DeliveryToStock", 100d)]
    [InlineData("DeliveryFromStock", 200d)]
    [InlineData("DeliveryToCustomer", 300d)]
    public async Task SameIdSelectsOnlyTheRequestedDeliveryType(string type, double quantity)
    {
        await using var db = CreateDb();
        db.DeliveryToStocks.Add(new DeliveryToStock { Id = 1, NrOfItems = 100 });
        db.DeliveryFromStocks.Add(new DeliveryFromStock { Id = 1, NrOfItems = 200 });
        db.DeliveryToCustomers.Add(new DeliveryToCustomer { Id = 1, NrOfItems = 300 });
        await db.SaveChangesAsync();

        var result = await new DeliveryEditingService(db).LoadAsync(type, 1);

        Assert.NotNull(result);
        Assert.Equal(type, result.Delivery.Type);
        Assert.Equal(quantity, result.Delivery.NrOfItems);
        Assert.NotEmpty(result.Version);
    }

    [Fact]
    public async Task MissingDeliveryDoesNotFallBackToAnotherType()
    {
        await using var db = CreateDb();
        db.DeliveryToStocks.Add(new DeliveryToStock { Id = 1 });
        await db.SaveChangesAsync();
        var service = new DeliveryEditingService(db);

        Assert.Null(await service.LoadAsync("DeliveryFromStock", 1));
        Assert.Null(await service.LoadAsync("Invalid", 1));
    }

    private static UpdateDeliveryRequest Request(DeliveryEditResponse response) => new()
    {
        Version = response.Version,
        DeliveryDate = response.Delivery.DeliveryDate ?? new DateTime(2026, 11, 12),
        NrOfItems = response.Delivery.NrOfItems,
        NrOfPallets = response.Delivery.NrOfPallets,
        NrOfBunt = response.Delivery.NrOfBunt,
        NrOfYtterforpackning = response.Delivery.NrOfYtterforpackning,
        InventoryId = response.Delivery.InventoryId,
        PalletLength = response.Delivery.PalletLength,
        PalletWidth = response.Delivery.PalletWidth,
        PalletHeight = response.Delivery.PalletHeight,
        PalletCalcFactor = response.Delivery.PalletCalcFactor,
        PalletIsStackable = response.Delivery.PalletIsStackable,
        IsDelivered = response.Delivery.DeliveryStatus == 2,
        CallOff = response.Delivery.CallOff,
        SpcsRefNr = response.Delivery.SpcsRefNr,
        SupplierInvoiceNr = response.Delivery.SupplierInvoiceNr,
    };

    [Fact]
    public async Task SavesWhitelistedFieldsAndPreservesStatusAndInvoiceFlags()
    {
        await using var db = CreateDb();
        db.DeliveryToCustomers.Add(new DeliveryToCustomer { Id = 1, OldDbId = 42, ToInvoice = true, DoNotInvoice = true, DeliveryStatus = 3 });
        await db.SaveChangesAsync();
        var service = new DeliveryEditingService(db);
        var loaded = (await service.LoadAsync("DeliveryToCustomer", 1))!;

        var saved = await service.SaveAsync("DeliveryToCustomer", 1, Request(loaded) with { NrOfItems = 123, SpcsRefNr = "Ref" });

        Assert.Null(saved.Error);
        Assert.Equal(123, saved.Response!.Delivery.NrOfItems);
        var entity = (await db.DeliveryToCustomers.FindAsync(1))!;
        Assert.Equal(42, entity.OldDbId);
        Assert.True(entity.ToInvoice);
        Assert.True(entity.DoNotInvoice);
        Assert.Equal(3, entity.DeliveryStatus);
        Assert.Equal("Ref", entity.SpcsRefNr);
        var stale = await service.SaveAsync("DeliveryToCustomer", 1, Request(loaded));
        Assert.Equal("StaleVersion", stale.Error!.Code);
    }

    [Fact]
    public async Task EditingStockDeliveryExcludesItselfAndIncludesOtherCustomerOrders()
    {
        await using var db = CreateDb();
        db.SupplierOrders.Add(new SupplierOrder { Id = 10 });
        db.CustomerOrders.AddRange(new CustomerOrder { Id = 20, SupplierOrderId = 10 }, new CustomerOrder { Id = 21, SupplierOrderId = 10 });
        db.DeliveryToStocks.Add(new DeliveryToStock { Id = 1, SupplierOrderId = 10, EditionPerPallet = 100, NrOfItems = 500, NrOfPallets = 5 });
        db.DeliveryFromStocks.AddRange(
            new DeliveryFromStock { Id = 2, CustomerOrderId = 20, EditionPerPallet = 100, NrOfItems = 300, NrOfPallets = 3 },
            new DeliveryFromStock { Id = 3, CustomerOrderId = 21, EditionPerPallet = 100, NrOfItems = 100, NrOfPallets = 1 });
        await db.SaveChangesAsync();
        var service = new DeliveryEditingService(db);
        var loaded = (await service.LoadAsync("DeliveryFromStock", 2))!;
        Assert.Equal(400, loaded.AvailableItems);
        Assert.Equal(4, loaded.AvailablePallets);
        Assert.Null((await service.SaveAsync("DeliveryFromStock", 2, Request(loaded))).Error);
        var latest = (await service.LoadAsync("DeliveryFromStock", 2))!;
        var rejected = await service.SaveAsync("DeliveryFromStock", 2, Request(latest) with { NrOfItems = 500, NrOfPallets = 5 });
        Assert.Equal("Validation", rejected.Error!.Code);
        Assert.Equal(300, (await db.DeliveryFromStocks.FindAsync(2))!.NrOfItems);
    }

    [Fact]
    public async Task ParentFormatChangesPropagateToAllSlattWithoutChangingTheirQuantity()
    {
        await using var db = CreateDb();
        db.DeliveryToStocks.AddRange(
            new DeliveryToStock { Id = 1, PalletLength = 1200, PalletWidth = 800, PalletHeight = 1000 },
            new DeliveryToStock { Id = 2, ParentDeliveryId = 1, IsSlattPallet = true, NrOfItems = 17, OldDbId = 72 },
            new DeliveryToStock { Id = 3, ParentDeliveryId = 1, IsSlattPallet = true, NrOfItems = 18 });
        await db.SaveChangesAsync();
        var service = new DeliveryEditingService(db);
        var loaded = (await service.LoadAsync("DeliveryToStock", 1))!;
        var saved = await service.SaveAsync("DeliveryToStock", 1, Request(loaded) with { PalletWidth = 900, PalletCalcFactor = 1.234m });

        Assert.Null(saved.Error);
        Assert.Equal(2, saved.Response!.RelatedDeliveryIds.Count);
        foreach (var child in await db.DeliveryToStocks.Where(item => item.ParentDeliveryId == 1).ToListAsync())
        {
            Assert.Equal(900, child.PalletWidth);
            Assert.Equal(1.234m, child.PalletCalcFactor);
        }
        Assert.Equal(17, (await db.DeliveryToStocks.FindAsync(2))!.NrOfItems);
        Assert.Equal(72, (await db.DeliveryToStocks.FindAsync(2))!.OldDbId);
    }

    [Fact]
    public async Task StockTakingRequiresExplicitConfirmationWithoutPersistingRejectedEdit()
    {
        await using var db = CreateDb();
        db.SupplierOrders.Add(new SupplierOrder { Id = 10 });
        db.DeliveryToCustomers.Add(new DeliveryToCustomer { Id = 1, SupplierOrderId = 10, NrOfItems = 100 });
        var snapshotDate = new DateTime(2026, 11, 14, 23, 59, 59);
        db.StockTakings.Add(new StockTaking { Id = 5, StockTakingDate = snapshotDate });
        db.StockTakingItems.Add(new StockTakingItem { Id = 6, SupplierOrderId = 10, StockTakingId = 5, NrOfItems = 100, NrOfPallets = 1 });
        await db.SaveChangesAsync();
        var service = new DeliveryEditingService(db);
        var loaded = (await service.LoadAsync("DeliveryToCustomer", 1))!;
        var request = Request(loaded) with { NrOfItems = 120 };
        var rejected = await service.SaveAsync("DeliveryToCustomer", 1, request);

        Assert.Equal("StockTakingConfirmationRequired", rejected.Error!.Code);
        Assert.Equal(100, (await db.DeliveryToCustomers.FindAsync(1))!.NrOfItems);
        Assert.Null((await service.SaveAsync("DeliveryToCustomer", 1, request with { ConfirmedStockTakingDate = snapshotDate })).Error);
    }

    [Fact]
    public async Task AutomaticFactorUsesCombinedSlattPalletsAndIgnoresClientFactor()
    {
        await using var db = CreateDb();
        db.CalculationRows.Add(new CalculationRow { Id = 8, ArchivedAutoFreightCalc = true });
        db.SupplierOrders.Add(new SupplierOrder { Id = 10, SelectedCalculationRowId = 8 });
        db.PalletFactors.Add(new PalletFactor { Id = 7, LengthFrom = 1100, LengthTo = 1200, WidthFrom = 700, WidthTo = 800, PalletFactors = "1;1.2;1.3;1.4" });
        db.DeliveryToStocks.AddRange(
            new DeliveryToStock { Id = 1, SupplierOrderId = 10, NrOfPallets = 4, PalletLength = 1200, PalletWidth = 800 },
            new DeliveryToStock { Id = 2, SupplierOrderId = 10, ParentDeliveryId = 1, IsSlattPallet = true, NrOfPallets = 1 });
        await db.SaveChangesAsync();
        var service = new DeliveryEditingService(db);
        var loaded = (await service.LoadAsync("DeliveryToStock", 1))!;
        var request = Request(loaded) with { PalletIsStackable = true, PalletCalcFactor = 99 };
        var preview = await service.PreviewAsync("DeliveryToStock", 1, request);
        Assert.Equal(1.3m, preview.Response!.Delivery.PalletCalcFactor);
        Assert.False((await db.DeliveryToStocks.FindAsync(1))!.PalletIsStackable);
        var saved = await service.SaveAsync("DeliveryToStock", 1, request);
        Assert.Equal(1.3m, saved.Response!.Delivery.PalletCalcFactor);
        Assert.Equal(1.3m, (await db.DeliveryToStocks.FindAsync(2))!.PalletCalcFactor);
    }

    [Fact]
    public async Task SnapshotCapsAvailabilityAndSameOrderDeliveriesAfterSnapshotAreCounted()
    {
        await using var db = CreateDb();
        db.SupplierOrders.Add(new SupplierOrder { Id = 10 });
        db.CustomerOrders.Add(new CustomerOrder { Id = 20, SupplierOrderId = 10 });
        db.DeliveryToStocks.Add(new DeliveryToStock { Id = 1, SupplierOrderId = 10, EditionPerPallet = 100, NrOfItems = 1000, NrOfPallets = 10, DeliveryStatus = 2, DeliveryDate = new DateTime(2026, 11, 1) });
        db.DeliveryFromStocks.AddRange(
            new DeliveryFromStock { Id = 2, CustomerOrderId = 20, EditionPerPallet = 100, NrOfItems = 200, NrOfPallets = 2, DeliveryDate = new DateTime(2026, 11, 15) },
            new DeliveryFromStock { Id = 3, CustomerOrderId = 20, EditionPerPallet = 100, NrOfItems = 100, NrOfPallets = 1, DeliveryDate = new DateTime(2026, 11, 16) });
        db.StockTakings.Add(new StockTaking { Id = 5, StockTakingDate = new DateTime(2026, 11, 10, 23, 59, 59) });
        db.StockTakingItems.Add(new StockTakingItem { Id = 6, SupplierOrderId = 10, StockTakingId = 5, NrOfItems = 300, NrOfPallets = 3 });
        await db.SaveChangesAsync();
        var loaded = (await new DeliveryEditingService(db).LoadAsync("DeliveryFromStock", 2))!;
        Assert.Equal(200, loaded.AvailableItems);
        Assert.Equal(2, loaded.AvailablePallets);
    }

    [Theory]
    [InlineData(-1d)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public async Task InvalidQuantityDoesNotMutateDeliveryOrChildren(double quantity)
    {
        await using var db = CreateDb();
        db.DeliveryToStocks.Add(new DeliveryToStock { Id = 1, NrOfItems = 100 });
        await db.SaveChangesAsync();
        var service = new DeliveryEditingService(db);
        var loaded = (await service.LoadAsync("DeliveryToStock", 1))!;
        var rejected = await service.SaveAsync("DeliveryToStock", 1, Request(loaded) with { NrOfItems = quantity });
        Assert.Equal("Validation", rejected.Error!.Code);
        Assert.Equal(100, (await db.DeliveryToStocks.FindAsync(1))!.NrOfItems);
    }

    [Fact]
    public async Task SlattDimensionsCannotBeEditedAndChildChangesInvalidateParentVersion()
    {
        await using var db = CreateDb();
        db.DeliveryToStocks.AddRange(new DeliveryToStock { Id = 1 }, new DeliveryToStock { Id = 2, ParentDeliveryId = 1, IsSlattPallet = true, PalletWidth = 800 });
        await db.SaveChangesAsync();
        var service = new DeliveryEditingService(db);
        var parent = (await service.LoadAsync("DeliveryToStock", 1))!;
        var child = (await service.LoadAsync("DeliveryToStock", 2))!;
        var rejected = await service.SaveAsync("DeliveryToStock", 2, Request(child) with { PalletWidth = 900 });
        Assert.Equal("Validation", rejected.Error!.Code);
        (await db.DeliveryToStocks.FindAsync(2))!.NrOfPallets = 2;
        await db.SaveChangesAsync();
        var stale = await service.SaveAsync("DeliveryToStock", 1, Request(parent));
        Assert.Equal("StaleVersion", stale.Error!.Code);
    }

    [Fact]
    public async Task HistoricStockDeliveryCanBeEditedWithoutReservingItsQuantityAgain()
    {
        await using var db = CreateDb();
        db.SupplierOrders.Add(new SupplierOrder { Id = 10 });
        db.CustomerOrders.Add(new CustomerOrder { Id = 20, SupplierOrderId = 10 });
        db.DeliveryToStocks.Add(new DeliveryToStock { Id = 1, SupplierOrderId = 10, EditionPerPallet = 100, NrOfItems = 1000, NrOfPallets = 10, DeliveryStatus = 2, DeliveryDate = new DateTime(2026, 11, 1) });
        db.DeliveryFromStocks.Add(new DeliveryFromStock { Id = 2, CustomerOrderId = 20, EditionPerPallet = 100, NrOfItems = 900, NrOfPallets = 9, DeliveryStatus = 2, DeliveryDate = new DateTime(2026, 11, 2) });
        var snapshotDate = new DateTime(2026, 11, 10, 23, 59, 59);
        db.StockTakings.Add(new StockTaking { Id = 5, StockTakingDate = snapshotDate });
        db.StockTakingItems.Add(new StockTakingItem { Id = 6, SupplierOrderId = 10, StockTakingId = 5, NrOfItems = 100, NrOfPallets = 1 });
        await db.SaveChangesAsync();
        var service = new DeliveryEditingService(db);
        var loaded = (await service.LoadAsync("DeliveryFromStock", 2))!;
        Assert.Equal(100, loaded.AvailableItems);
        var saved = await service.SaveAsync("DeliveryFromStock", 2, Request(loaded) with { CallOff = "New marking", ConfirmedStockTakingDate = snapshotDate });
        Assert.Null(saved.Error);
        Assert.Equal("New marking", saved.Response!.Delivery.CallOff);
        Assert.Equal(900, saved.Response.Delivery.NrOfItems);
    }

    [Fact]
    public async Task UnrelatedEditPreservesAutomaticFactorAndDistinctChildFactors()
    {
        await using var db = CreateDb();
        db.CalculationRows.Add(new CalculationRow { Id = 8, ArchivedAutoFreightCalc = true });
        db.SupplierOrders.Add(new SupplierOrder { Id = 10, SelectedCalculationRowId = 8 });
        db.DeliveryToStocks.AddRange(
            new DeliveryToStock { Id = 1, SupplierOrderId = 10, PalletCalcFactor = 1.2m },
            new DeliveryToStock { Id = 2, SupplierOrderId = 10, ParentDeliveryId = 1, IsSlattPallet = true, PalletCalcFactor = 1.3m });
        await db.SaveChangesAsync();
        var service = new DeliveryEditingService(db);
        var loaded = (await service.LoadAsync("DeliveryToStock", 1))!;
        var saved = await service.SaveAsync("DeliveryToStock", 1, Request(loaded) with { SpcsRefNr = "Ref", PalletCalcFactor = 99 });
        Assert.Null(saved.Error);
        Assert.Equal(1.2m, saved.Response!.Delivery.PalletCalcFactor);
        Assert.Equal(1.3m, (await db.DeliveryToStocks.FindAsync(2))!.PalletCalcFactor);
    }

    [Fact]
    public async Task BundleQuantityMustMatchItemsBeforeSaving()
    {
        await using var db = CreateDb();
        db.CalculationRows.Add(new CalculationRow { Id = 8, ArchivedNrOfPerBundle = 25 });
        db.SupplierOrders.Add(new SupplierOrder { Id = 10, SelectedCalculationRowId = 8, PackagingType = "BUNT" });
        db.CustomerOrders.Add(new CustomerOrder { Id = 20, SupplierOrderId = 10 });
        db.DeliveryToStocks.Add(new DeliveryToStock { Id = 1, SupplierOrderId = 10, EditionPerPallet = 100, NrOfItems = 500, NrOfPallets = 5 });
        db.DeliveryFromStocks.Add(new DeliveryFromStock { Id = 2, CustomerOrderId = 20, EditionPerPallet = 100, NrOfItems = 200, NrOfPallets = 2, NrOfBunt = 8, DeliveryDate = new DateTime(2026, 11, 12) });
        await db.SaveChangesAsync();
        var service = new DeliveryEditingService(db);
        var loaded = (await service.LoadAsync("DeliveryFromStock", 2))!;
        var request = Request(loaded) with { NrOfItems = 300, NrOfPallets = 3, NrOfBunt = 11 };
        var rejected = await service.SaveAsync("DeliveryFromStock", 2, request);
        Assert.Equal("Validation", rejected.Error!.Code);
        Assert.Equal(200, (await db.DeliveryFromStocks.FindAsync(2))!.NrOfItems);
        Assert.Null((await service.SaveAsync("DeliveryFromStock", 2, request with { NrOfBunt = 12 })).Error);
    }

    [Fact]
    public async Task InvalidWarehouseAndOutOfRangeFactorCannotBePersisted()
    {
        await using var db = CreateDb();
        db.DeliveryToStocks.Add(new DeliveryToStock { Id = 1, NrOfItems = 100 });
        await db.SaveChangesAsync();
        var service = new DeliveryEditingService(db);
        var loaded = (await service.LoadAsync("DeliveryToStock", 1))!;
        Assert.Equal("Validation", (await service.SaveAsync("DeliveryToStock", 1, Request(loaded) with { InventoryId = 999 })).Error!.Code);
        Assert.Equal("Validation", (await service.SaveAsync("DeliveryToStock", 1, Request(loaded) with { PalletCalcFactor = 99.9999m })).Error!.Code);
        Assert.Null((await db.DeliveryToStocks.FindAsync(1))!.InventoryId);
    }

    [Theory]
    [InlineData(1, true, 2)]
    [InlineData(2, false, 1)]
    public async Task ExplicitDeliveredToggleMapsToLegacyStatuses(int originalStatus, bool delivered, int expectedStatus)
    {
        await using var db = CreateDb();
        db.DeliveryToCustomers.Add(new DeliveryToCustomer { Id = 1, DeliveryStatus = originalStatus });
        await db.SaveChangesAsync();
        var service = new DeliveryEditingService(db);
        var loaded = (await service.LoadAsync("DeliveryToCustomer", 1))!;
        var saved = await service.SaveAsync("DeliveryToCustomer", 1, Request(loaded) with { IsDelivered = delivered });
        Assert.Equal(expectedStatus, saved.Response!.Delivery.DeliveryStatus);
    }
}