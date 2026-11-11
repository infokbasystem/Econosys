using System.Security.Claims;
using Econosys.Api.Controllers;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Econosys.Api.Tests;

public class CallOffPlanningTests
{
    private static ApplicationDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static CallOffController CreateController(ApplicationDbContext db) =>
        new(db, new TestUserResolution(), new CallOffRouteService(db, new CallOffRouteTests.FakeRoutingClient(), NullLogger<CallOffRouteService>.Instance))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

    private static AddCallOffDeliveryFromStockRequest Delivery(int orderId = 10) => new()
    {
        CustomerOrderId = orderId,
        EditionPerPallet = 100,
        NrOfItems = 200,
        NrOfPallets = 2,
        DeliveryDate = new DateTime(2026, 11, 12),
    };

    [Fact]
    public async Task ExistingCallOffReceivesDeliveryAndItsDate()
    {
        await using var db = CreateDb();
        db.CustomerOrders.Add(new CustomerOrder { Id = 10, CustomerOrderNr = "1234", CustomerDeliveryAddressId = 7 });
        db.CallOffs.Add(new CallOff { Id = 4, DeliveryStatus = 0, DeliveryDate = new DateTime(2026, 11, 18) });
        await db.SaveChangesAsync();

        var result = await CreateController(db).AddPlannedDelivery(4, Delivery(), CancellationToken.None);

        Assert.IsType<CreatedAtActionResult>(result);
        var link = Assert.Single(await db.CallOffDeliveries.Include(x => x.DeliveryFromStock).ToListAsync());
        Assert.Equal(4, link.CallOffId);
        Assert.Equal(new DateTime(2026, 11, 18), link.DeliveryFromStock!.DeliveryDate);
        Assert.Equal(200, link.DeliveryFromStock.NrOfItems);
        Assert.Equal(7, (await db.CallOffs.FindAsync(4))!.CustomerDeliveryAddressId);
    }

    [Fact]
    public async Task MissingOrCompletedCallOffDoesNotCreateStockDelivery()
    {
        await using var db = CreateDb();
        db.CustomerOrders.Add(new CustomerOrder { Id = 10 });
        db.CallOffs.Add(new CallOff { Id = 4, DeliveryStatus = 2 });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        Assert.IsType<NotFoundResult>(await controller.AddPlannedDelivery(99, Delivery(), CancellationToken.None));
        Assert.IsType<ConflictObjectResult>(await controller.AddPlannedDelivery(4, Delivery(), CancellationToken.None));
        Assert.Empty(await db.DeliveryFromStocks.ToListAsync());
    }

    [Fact]
    public async Task NewCallOffSavesDraftDeliveryAndLinkTogether()
    {
        await using var db = CreateDb();
        db.CustomerOrders.Add(new CustomerOrder { Id = 10, CustomerOrderNr = "1234" });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var result = await controller.CreateCallOff(new SaveCallOffAggregateRequest
        {
            DeliveryStatus = 0,
            CallOffDeliveryList = new() { new SaveCallOffDeliveryDto { NewDelivery = Delivery() } },
        });

        Assert.IsType<OkObjectResult>(result.Result);
        var callOff = Assert.Single(await db.CallOffs.ToListAsync());
        var link = Assert.Single(await db.CallOffDeliveries.Include(x => x.DeliveryFromStock).ToListAsync());
        Assert.Equal(callOff.Id, link.CallOffId);
        Assert.Equal(new DateTime(2026, 11, 12), link.DeliveryFromStock!.DeliveryDate);
        Assert.Equal(2, link.DeliveryFromStock.NrOfPallets);
    }

    [Fact]
    public async Task InvalidDraftDoesNotPersistEmptyCallOff()
    {
        await using var db = CreateDb();
        var result = await CreateController(db).CreateCallOff(new SaveCallOffAggregateRequest
        {
            CallOffDeliveryList = new() { new SaveCallOffDeliveryDto { NewDelivery = Delivery(99) } },
        });

        Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Empty(await db.CallOffs.AsNoTracking().ToListAsync());
        Assert.Empty(await db.DeliveryFromStocks.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CandidateLookupUsesOrderIdInsteadOfDuplicateOrderNumber()
    {
        await using var db = CreateDb();
        db.CustomerOrders.AddRange(
            new CustomerOrder { Id = 10, CustomerOrderNr = "1234", CustomerName = "First" },
            new CustomerOrder { Id = 11, CustomerOrderNr = "1234", CustomerName = "Second" });
        await db.SaveChangesAsync();

        var result = await CreateController(db).GetDeliveryCandidates("1234", 11);

        var candidate = Assert.IsType<CallOffDeliveryCandidateResponseDto>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(11, candidate.CustomerOrderId);
        Assert.Equal("Second", candidate.CustomerName);
    }

    [Fact]
    public async Task NewCallOffStillLinksPreviouslyCreatedStockDelivery()
    {
        await using var db = CreateDb();
        db.CustomerOrders.Add(new CustomerOrder { Id = 10 });
        db.DeliveryFromStocks.Add(new DeliveryFromStock { Id = 20, CustomerOrderId = 10 });
        await db.SaveChangesAsync();

        var result = await CreateController(db).CreateCallOff(new SaveCallOffAggregateRequest
        {
            CallOffDeliveryList = new() { new SaveCallOffDeliveryDto { DeliveryFromStockId = 20 } },
        });

        Assert.IsType<OkObjectResult>(result.Result);
        var link = Assert.Single(await db.CallOffDeliveries.ToListAsync());
        Assert.Equal(20, link.DeliveryFromStockId);
        Assert.NotNull(link.CallOffId);
    }

    private sealed class TestUserResolution : ILegacyUserResolutionService
    {
        public Task<LegacyUser?> ResolveCurrentUserAsync(ClaimsPrincipal user) => Task.FromResult<LegacyUser?>(null);
    }
}
