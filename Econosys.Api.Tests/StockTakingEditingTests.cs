using Econosys.Api.Controllers;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Econosys.Api.Tests;

public class StockTakingEditingTests
{
    private static ApplicationDbContext CreateDb() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    private static StockTakingsController CreateController(ApplicationDbContext db) =>
        new(db, NullLogger<StockTakingsController>.Instance);

    [Fact]
    public async Task UpdateItemKeepsOriginalCalculatedLevelWhenRecomputingDiff()
    {
        await using var db = CreateDb();
        db.StockTakings.Add(new StockTaking { Id = 1, StockTakingDate = new DateTime(2026, 11, 1) });
        db.StockTakingItems.Add(new StockTakingItem
        {
            Id = 10, StockTakingId = 1, NrOfItems = 90, NrOfPallets = 4, DiffNrOfItems = -10, DiffNrOfPallets = -1
        });
        await db.SaveChangesAsync();

        var result = await CreateController(db).UpdateItem(1, 10, new UpdateStockTakingItemRequestDto
        {
            NrOfItems = 120,
            NrOfPallets = null
        });

        Assert.IsType<OkObjectResult>(result.Result);
        var item = await db.StockTakingItems.AsNoTracking().SingleAsync(x => x.Id == 10);
        Assert.Equal(120, item.NrOfItems);
        Assert.Equal(20, item.DiffNrOfItems);
        Assert.Null(item.NrOfPallets);
        Assert.Null(item.DiffNrOfPallets);
    }

    [Fact]
    public async Task UpdateItemRejectsItemFromAnotherStockTaking()
    {
        await using var db = CreateDb();
        db.StockTakings.AddRange(new StockTaking { Id = 1 }, new StockTaking { Id = 2 });
        db.StockTakingItems.Add(new StockTakingItem { Id = 10, StockTakingId = 2, NrOfItems = 5, DiffNrOfItems = 0 });
        await db.SaveChangesAsync();

        var result = await CreateController(db).UpdateItem(1, 10, new UpdateStockTakingItemRequestDto { NrOfItems = 6 });

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task UpdateItemRejectsItemSupersededByLaterStockTaking()
    {
        await using var db = CreateDb();
        db.StockTakings.AddRange(
            new StockTaking { Id = 1, StockTakingDate = new DateTime(2026, 10, 1) },
            new StockTaking { Id = 2, StockTakingDate = new DateTime(2026, 11, 1) });
        db.StockTakingItems.AddRange(
            new StockTakingItem { Id = 10, StockTakingId = 1, SupplierOrderId = 5, NrOfItems = 5, DiffNrOfItems = 0 },
            new StockTakingItem { Id = 20, StockTakingId = 2, SupplierOrderId = 5, NrOfItems = 5, DiffNrOfItems = 0 });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var rejected = await controller.UpdateItem(1, 10, new UpdateStockTakingItemRequestDto { NrOfItems = 6 });
        var accepted = await controller.UpdateItem(2, 20, new UpdateStockTakingItemRequestDto { NrOfItems = 6 });

        Assert.IsType<BadRequestObjectResult>(rejected.Result);
        var aggregate = Assert.IsType<StockTakingAggregateDto>(Assert.IsType<OkObjectResult>(accepted.Result).Value);
        Assert.True(aggregate.CanDelete);
        Assert.True(Assert.Single(aggregate.Items).IsEditable);
    }

    [Fact]
    public async Task DeleteRemovesLatestStockTakingAndOnlyItsItems()
    {
        await using var db = CreateDb();
        db.StockTakings.AddRange(
            new StockTaking { Id = 1, StockTakingDate = new DateTime(2026, 10, 1) },
            new StockTaking { Id = 2, StockTakingDate = new DateTime(2026, 11, 1) });
        db.StockTakingItems.AddRange(
            new StockTakingItem { Id = 10, StockTakingId = 1 },
            new StockTakingItem { Id = 20, StockTakingId = 2 },
            new StockTakingItem { Id = 21, StockTakingId = 2 });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        Assert.IsType<BadRequestObjectResult>(await controller.Delete(1));
        Assert.IsType<NoContentResult>(await controller.Delete(2));

        Assert.Equal([1], await db.StockTakings.Select(x => x.Id).ToListAsync());
        Assert.Equal([10], await db.StockTakingItems.Select(x => x.Id).ToListAsync());
    }
}
