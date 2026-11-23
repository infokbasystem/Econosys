using Econosys.Api.Controllers;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Econosys.Api.Tests;

public class PartyPricesTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PricesCanBeAddedEditedAndRemovedWithoutChangingOtherPartyOrCostFields(bool isSupplier)
    {
        await using var db = CreateContext();
        await SeedAsync(db);
        db.PalletFormatPrices.Add(new PalletFormatPrice
        {
            Id = 101, CustomerId = isSupplier ? 1 : null, SupplierId = isSupplier ? null : 1,
            PalletFormatId = 1, PalletPrice = 700,
        });
        db.CostPrices.Add(new CostPrice
        {
            Id = 101, CustomerId = isSupplier ? 1 : null, SupplierId = isSupplier ? null : 1,
            CostId = 30, DefaultInPrice = 701, DefaultOutPrice = 702,
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var service = new PartyPricesService(db);
        var request = new UpdatePartyPricesRequest
        {
            PalletPrices = [new() { ItemId = 1, Price = 175.25m }, new() { ItemId = 2 }],
            CostPrices = [new() { ItemId = 30, Price = 0 }],
        };
        Assert.Equal((true, (string?)null), await service.SaveAsync(1, isSupplier, request, default));
        db.ChangeTracker.Clear();

        var initial = Assert.IsType<PartyPricesResponse>(await service.GetAsync(1, isSupplier, default));
        Assert.Equal(2, initial.PalletPrices.Count);
        Assert.Equal(175.25m, initial.PalletPrices[0].Price);
        Assert.Null(initial.PalletPrices[1].Price);
        Assert.Equal(0m, Assert.Single(initial.CostPrices).Price);
        Assert.Contains(initial.PalletFormats, x => x.Id == 2 && !x.IsActive);
        Assert.Contains(initial.Costs, x => x.Id == 30);

        var storedCost = await db.CostPrices.SingleAsync(x => isSupplier ? x.SupplierId == 1 : x.CustomerId == 1);
        storedCost.CalculationAutoAddSupplier = true;
        if (isSupplier)
            storedCost.DefaultOutPrice = 83;
        else
            storedCost.DefaultInPrice = 83;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var edit = new UpdatePartyPricesRequest
        {
            PalletPrices = [initial.PalletPrices[0] with { ItemId = 2, Price = 0 }],
            CostPrices = [initial.CostPrices[0] with { Price = null }],
        };
        Assert.Equal((true, (string?)null), await service.SaveAsync(1, isSupplier, edit, default));
        db.ChangeTracker.Clear();
        var edited = Assert.IsType<PartyPricesResponse>(await service.GetAsync(1, isSupplier, default));
        var pallet = Assert.Single(edited.PalletPrices);
        Assert.Equal(2, pallet.ItemId);
        Assert.Equal(0m, pallet.Price);
        Assert.Equal(initial.PalletPrices[0].Id, pallet.Id);
        Assert.Null(Assert.Single(edited.CostPrices).Price);

        storedCost = await db.CostPrices.SingleAsync(x => x.Id == storedCost.Id);
        Assert.True(storedCost.CalculationAutoAddSupplier);
        Assert.Equal(83m, isSupplier ? storedCost.DefaultOutPrice : storedCost.DefaultInPrice);

        var controller = new PartyPricesController(service);
        Assert.IsType<NoContentResult>(await controller.Update(isSupplier ? "supplier" : "customer", 1,
            new UpdatePartyPricesRequest { PalletPrices = [], CostPrices = [] }, default));
        db.ChangeTracker.Clear();
        var cleared = Assert.IsType<PartyPricesResponse>(await service.GetAsync(1, isSupplier, default));
        Assert.Empty(cleared.PalletPrices);
        Assert.Empty(cleared.CostPrices);
        Assert.Equal(900m, (await db.PalletFormatPrices.SingleAsync(x => x.Id == 100)).PalletPrice);
        Assert.Equal(901m, (await db.CostPrices.SingleAsync(x => x.Id == 100)).DefaultInPrice);
        Assert.Equal(700m, (await db.PalletFormatPrices.SingleAsync(x => x.Id == 101)).PalletPrice);
        Assert.Equal(702m, (await db.CostPrices.SingleAsync(x => x.Id == 101)).DefaultOutPrice);
    }

    [Theory]
    [InlineData("foreign-pallet")]
    [InlineData("foreign-cost")]
    [InlineData("unknown-item")]
    [InlineData("duplicate-row")]
    [InlineData("precision")]
    [InlineData("overflow")]
    public async Task InvalidRequestsDoNotPartiallyUpdateOrDeletePrices(string invalidCase)
    {
        await using var db = CreateContext();
        await SeedAsync(db);
        db.PalletFormatPrices.Add(new PalletFormatPrice { Id = 200, CustomerId = 1, PalletFormatId = 1, PalletPrice = 175 });
        db.CostPrices.Add(new CostPrice { Id = 200, CustomerId = 1, CostId = 30, DefaultOutPrice = 50 });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var request = new UpdatePartyPricesRequest
        {
            PalletPrices = invalidCase switch
            {
                "foreign-pallet" => [new() { Id = 100, ItemId = 1, Price = 10 }],
                "unknown-item" => [new() { ItemId = 9999, Price = 10 }],
                "duplicate-row" => [new() { Id = 200, ItemId = 1 }, new() { Id = 200, ItemId = 2 }],
                "precision" => [new() { ItemId = 1, Price = 1.234m }],
                "overflow" => [new() { ItemId = 1, Price = 100000000m }],
                _ => [],
            },
            CostPrices = invalidCase == "foreign-cost" ? [new() { Id = 100, ItemId = 30 }] : [],
        };
        var controller = new PartyPricesController(new PartyPricesService(db));
        var result = Assert.IsType<ObjectResult>(await controller.Update("customer", 1, request, default));
        Assert.Equal(400, result.StatusCode);
        Assert.NotEmpty(Assert.IsType<ProblemDetails>(result.Value).Detail!);
        Assert.False(db.ChangeTracker.HasChanges());
        db.ChangeTracker.Clear();
        Assert.Equal(175m, (await db.PalletFormatPrices.SingleAsync(x => x.Id == 200)).PalletPrice);
        Assert.Equal(50m, (await db.CostPrices.SingleAsync(x => x.Id == 200)).DefaultOutPrice);
        Assert.Equal(900m, (await db.PalletFormatPrices.SingleAsync(x => x.Id == 100)).PalletPrice);
    }

    [Theory]
    [InlineData("customer")]
    [InlineData("supplier")]
    public async Task MissingPartyReturnsNotFound(string partyType)
    {
        await using var db = CreateContext();
        var controller = new PartyPricesController(new PartyPricesService(db));
        Assert.IsType<NotFoundResult>((await controller.Get(partyType, 999, default)).Result);
        Assert.IsType<NotFoundResult>(await controller.Update(partyType, 999,
            new UpdatePartyPricesRequest { PalletPrices = [], CostPrices = [] }, default));
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task SeedAsync(ApplicationDbContext db)
    {
        db.Customers.Add(new Customer { Id = 1, Name = "Customer" });
        db.Suppliers.Add(new Supplier { Id = 1, Name = "Supplier" });
        db.PalletTypes.Add(new PalletType { Id = 1, Name = "Pallet", IsActive = true });
        db.PalletFormats.AddRange(
            new PalletFormat { Id = 1, Name = "EUR-pall", Active = true, PalletTypeId = 1 },
            new PalletFormat { Id = 2, Name = "Inactive pallet", Active = false });
        db.Costs.Add(new Cost { Id = 30, Name = "Printing plates", IsActive = true });
        db.PalletFormatPrices.Add(new PalletFormatPrice { Id = 100, CustomerId = 2, SupplierId = 2, PalletFormatId = 1, PalletPrice = 900 });
        db.CostPrices.Add(new CostPrice { Id = 100, CustomerId = 2, SupplierId = 2, CostId = 30, DefaultInPrice = 901, DefaultOutPrice = 902 });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }
}
