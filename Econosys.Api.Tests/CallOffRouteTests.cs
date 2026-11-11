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

// All addresses, coordinates and the API key below are synthetic; Google is replaced by FakeRoutingClient.
public class CallOffRouteTests
{
    private static readonly GeoPoint Inventory = new(59.0, 18.0);
    private static readonly GeoPoint AddressA = new(59.5, 18.5);
    private static readonly GeoPoint AddressB = new(60.0, 17.0);
    private static readonly GeoPoint Midway = new(59.2, 18.2);

    [Fact]
    public void FerrySplitKeepsRoadAndFerryPartsAndDropsShortPortApproach()
    {
        var a = new GeoPoint(1, 1);
        var b = new GeoPoint(2, 2);
        var c = new GeoPoint(3, 3);
        var d = new GeoPoint(4, 4);
        var e = new GeoPoint(5, 5);
        var leg = new RouteLeg(a, e, 75300, new[]
        {
            new RouteStep(a, b, 300, null, "DRIVE"),
            new RouteStep(b, c, 50000, "FERRY", "DRIVE"),
            new RouteStep(c, d, 20000, null, "DRIVE"),
            new RouteStep(d, e, 5000, null, "DRIVE"),
        });

        var steps = CallOffRouteService.SplitFerrySteps(leg);

        Assert.Collection(steps,
            ferry => Assert.Equal(("FERRY", 50000, b, c), (ferry.TravelMode, ferry.DistanceMeters, ferry.Start, ferry.End)),
            road => Assert.Equal(("VEHICLE", 25000, c, e), (road.TravelMode, road.DistanceMeters, road.Start, road.End)));

        var withoutFerry = Assert.Single(CallOffRouteService.SplitFerrySteps(new RouteLeg(a, e, 1234, new[] { new RouteStep(a, e, 1234, null, "DRIVE") })));
        Assert.Equal(("VEHICLE", 1234, a, e), (withoutFerry.TravelMode, withoutFerry.DistanceMeters, withoutFerry.Start, withoutFerry.End));
    }

    [Fact]
    public async Task CreatingCallOffStoresLegsForSingleRoute()
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        var client = SingleLegClient(AddressA);
        var controller = CreateController(db, client);

        var result = await controller.CreateCallOff(new SaveCallOffAggregateRequest
        {
            CallOffDeliveryList = new() { new SaveCallOffDeliveryDto { DeliveryFromStockId = 20 } },
        });

        var aggregate = OkValue<CallOffAggregateDto>(result.Result);
        Assert.Equal(CallOffRouteStatus.Calculated, aggregate.RouteCalculation?.Status);
        Assert.False(aggregate.IsRouteMissing);
        Assert.Single(aggregate.DeliveryLegs.DeliveryLegGroupedList);

        var leg = Assert.Single(await db.DeliveryLegs.AsNoTracking().ToListAsync());
        var inventoryPositionId = (await db.Inventories.AsNoTracking().SingleAsync(x => x.Id == 1)).PositionId;
        var addressPositionId = (await db.CustomerDeliveryAddresses.AsNoTracking().SingleAsync(x => x.Id == 7)).PositionId;
        Assert.Equal(aggregate.CallOff.Id, leg.CallOffId);
        Assert.Equal(20, leg.DeliveryFromStockId);
        Assert.Equal(45m, leg.DistanceKm);
        Assert.Equal("VEHICLE", leg.TypeOfTransport);
        Assert.NotNull(inventoryPositionId);
        Assert.Equal(inventoryPositionId, leg.FromPositionId);
        Assert.Equal(addressPositionId, leg.ToPositionId);
        Assert.Equal(inventoryPositionId, leg.FromPositionIdLeg);
        Assert.Equal(addressPositionId, leg.ToPositionIdLeg);
        Assert.Equal(2, await db.Positions.CountAsync());
        var request = Assert.Single(client.RouteRequests);
        Assert.Empty(request.Intermediates);
        Assert.Equal(Inventory, request.Origin);
    }

    [Fact]
    public async Task CallOffAddressOverridesOrderAddress()
    {
        await using var db = CreateDb();
        await SeedAsync(db, linkedDeliveryIds: new[] { 20 });
        var client = SingleLegClient(AddressB);
        var controller = CreateController(db, client);
        var current = OkValue<CallOffAggregateDto>((await controller.GetAggregateById(4)).Result);

        var result = await controller.SaveAggregateById(4, SaveRequest(current, customerDeliveryAddressId: 8));

        var aggregate = OkValue<CallOffAggregateDto>(result.Result);
        Assert.Equal(CallOffRouteStatus.Calculated, aggregate.RouteCalculation?.Status);
        Assert.Contains(client.GeocodedAddresses, x => x.StartsWith("Kundvägen 3", StringComparison.Ordinal));
        Assert.DoesNotContain(client.GeocodedAddresses, x => x.StartsWith("Kundvägen 2", StringComparison.Ordinal));
        var addressPositionId = (await db.CustomerDeliveryAddresses.AsNoTracking().SingleAsync(x => x.Id == 8)).PositionId;
        Assert.All(await db.DeliveryLegs.AsNoTracking().ToListAsync(), leg => Assert.Equal(addressPositionId, leg.ToPositionId));
    }

    [Fact]
    public async Task MultiStopRouteAssignsLegsToEachDelivery()
    {
        await using var db = CreateDb();
        await SeedAsync(db, linkedDeliveryIds: new[] { 20, 21 });
        var client = new FakeRoutingClient
        {
            Route = _ => new[]
            {
                new RouteLeg(Inventory, AddressA, 10000, new[] { new RouteStep(Inventory, AddressA, 10000, null, "DRIVE") }),
                new RouteLeg(AddressA, AddressB, 20000, new[] { new RouteStep(AddressA, AddressB, 20000, null, "DRIVE") }),
            },
        };
        var controller = CreateController(db, client);

        var result = OkValue<CallOffRouteResponseDto>((await controller.RecalculateRoute(4, CancellationToken.None)).Result);

        Assert.Equal(CallOffRouteStatus.Calculated, result.RouteCalculation.Status);
        var request = Assert.Single(client.RouteRequests);
        Assert.Equal(Inventory, request.Origin);
        Assert.Equal(AddressA, request.Destination);
        Assert.Equal(new[] { AddressB }, request.Intermediates);

        var legs = await db.DeliveryLegs.AsNoTracking().ToListAsync();
        Assert.Equal(new[] { 1 }, legs.Where(x => x.DeliveryFromStockId == 20).Select(x => x.SortOrder));
        Assert.Equal(new[] { 1, 2 }, legs.Where(x => x.DeliveryFromStockId == 21).Select(x => x.SortOrder).OrderBy(x => x));
        Assert.Equal(new[] { 10m, 20m }, result.DeliveryLegs.DeliveryLegGroupedList.Select(x => x.DistanceKm!.Value));
    }

    [Fact]
    public async Task IncompleteAddressSavesCallOffWithoutLegsAndWarns()
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        (await db.CustomerDeliveryAddresses.SingleAsync(x => x.Id == 7)).CountryCode = null;
        await db.SaveChangesAsync();
        var client = SingleLegClient(AddressA);

        var result = await CreateController(db, client).CreateCallOff(new SaveCallOffAggregateRequest
        {
            CallOffDeliveryList = new() { new SaveCallOffDeliveryDto { DeliveryFromStockId = 20 } },
        });

        var aggregate = OkValue<CallOffAggregateDto>(result.Result);
        Assert.Equal(CallOffRouteStatus.Failed, aggregate.RouteCalculation?.Status);
        Assert.Contains("Leveransadressen Kund A", aggregate.RouteCalculation?.Message);
        Assert.True(aggregate.IsRouteMissing);
        Assert.Empty(client.RouteRequests);
        Assert.Single(await db.CallOffDeliveries.Where(x => x.CallOffId == aggregate.CallOff.Id).ToListAsync());
        Assert.Empty(await db.DeliveryLegs.ToListAsync());
    }

    [Fact]
    public async Task RoutingFailureAfterDeliveryChangeClearsOutdatedLegs()
    {
        await using var db = CreateDb();
        await SeedAsync(db, linkedDeliveryIds: new[] { 20 }, withExistingLeg: true);
        var client = new FakeRoutingClient { RouteException = new HttpRequestException("network down") };
        var controller = CreateController(db, client);
        var current = OkValue<CallOffAggregateDto>((await controller.GetAggregateById(4)).Result);
        var request = SaveRequest(current);
        request.CallOffDeliveryList.Add(new SaveCallOffDeliveryDto { DeliveryFromStockId = 21 });

        var aggregate = OkValue<CallOffAggregateDto>((await controller.SaveAggregateById(4, request)).Result);

        Assert.Equal(CallOffRouteStatus.Failed, aggregate.RouteCalculation?.Status);
        Assert.True(aggregate.IsRouteMissing);
        Assert.Equal(2, aggregate.CallOffDeliveryList.Count);
        Assert.Empty(await db.DeliveryLegs.ToListAsync());
    }

    [Fact]
    public async Task GoogleErrorMessageIsShownInRouteWarning()
    {
        await using var db = CreateDb();
        await SeedAsync(db, linkedDeliveryIds: new[] { 20 });
        var client = new FakeRoutingClient
        {
            RouteException = new GoogleRoutingException("Routes request failed.", 400, "INVALID_ARGUMENT: Invalid field mask."),
        };

        var result = OkValue<CallOffRouteResponseDto>((await CreateController(db, client).RecalculateRoute(4, CancellationToken.None)).Result);

        Assert.Equal(CallOffRouteStatus.Failed, result.RouteCalculation.Status);
        Assert.Equal("Transportbenen kunde inte beräknas. Google svarade: INVALID_ARGUMENT: Invalid field mask.", result.RouteCalculation.Message);
    }

    [Fact]
    public async Task UnrelatedSaveKeepsLegsWithoutCallingGoogle()
    {
        await using var db = CreateDb();
        await SeedAsync(db, linkedDeliveryIds: new[] { 20 }, withExistingLeg: true);
        var client = SingleLegClient(AddressA);
        var controller = CreateController(db, client);
        var current = OkValue<CallOffAggregateDto>((await controller.GetAggregateById(4)).Result);
        var request = SaveRequest(current);
        request.Note = "Ny notering";

        var aggregate = OkValue<CallOffAggregateDto>((await controller.SaveAggregateById(4, request)).Result);

        Assert.Null(aggregate.RouteCalculation);
        Assert.Empty(client.GeocodedAddresses);
        Assert.Empty(client.RouteRequests);
        Assert.Single(await db.DeliveryLegs.ToListAsync());
    }

    [Fact]
    public async Task UnchangedSaveRetriesWhenLegsAreMissing()
    {
        await using var db = CreateDb();
        await SeedAsync(db, linkedDeliveryIds: new[] { 20 });
        var client = SingleLegClient(AddressA);
        var controller = CreateController(db, client);
        var current = OkValue<CallOffAggregateDto>((await controller.GetAggregateById(4)).Result);
        Assert.True(current.IsRouteMissing);

        var aggregate = OkValue<CallOffAggregateDto>((await controller.SaveAggregateById(4, SaveRequest(current))).Result);

        Assert.Equal(CallOffRouteStatus.Calculated, aggregate.RouteCalculation?.Status);
        Assert.False(aggregate.IsRouteMissing);
        Assert.Single(client.RouteRequests);
    }

    [Fact]
    public async Task ChangeDuringCalculationDoesNotStoreStaleLegs()
    {
        var databaseName = Guid.NewGuid().ToString();
        await using var db = CreateDb(databaseName);
        await SeedAsync(db, linkedDeliveryIds: new[] { 20, 21 });
        var client = new FakeRoutingClient
        {
            Route = _ => new[]
            {
                new RouteLeg(Inventory, AddressA, 10000, Array.Empty<RouteStep>()),
                new RouteLeg(AddressA, AddressB, 20000, Array.Empty<RouteStep>()),
            },
            BeforeRouteReturns = async () =>
            {
                await using var other = CreateDb(databaseName);
                other.CallOffDeliveries.Remove(await other.CallOffDeliveries.SingleAsync(x => x.DeliveryFromStockId == 21));
                await other.SaveChangesAsync();
            },
        };

        var result = OkValue<CallOffRouteResponseDto>((await CreateController(db, client).RecalculateRoute(4, CancellationToken.None)).Result);

        Assert.Equal(CallOffRouteStatus.Stale, result.RouteCalculation.Status);
        Assert.Empty(await db.DeliveryLegs.ToListAsync());
    }

    [Fact]
    public async Task PreviewOfPlannedDraftDoesNotCreateDeliveriesOrLegs()
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        var client = SingleLegClient(AddressA);
        var stockDeliveriesBefore = await db.DeliveryFromStocks.CountAsync();

        var result = OkValue<CallOffRouteResponseDto>((await CreateController(db, client).PreviewRoute(new CallOffRoutePreviewRequest
        {
            Deliveries = new()
            {
                new CallOffRoutePreviewDeliveryDto
                {
                    NewDelivery = new AddCallOffDeliveryFromStockRequest { CustomerOrderId = 10, EditionPerPallet = 100, InventoryId = 1 },
                },
            },
        }, CancellationToken.None)).Result);

        Assert.Equal(CallOffRouteStatus.Calculated, result.RouteCalculation.Status);
        var leg = Assert.Single(result.DeliveryLegs.DeliveryLegGroupedList);
        Assert.Null(leg.DeliveryFromStockId);
        Assert.Equal("Lagerstad", leg.HelppropLegFromPositionPostalAddress);
        Assert.Equal("Kundstad", leg.HelppropLegToPositionPostalAddress);
        Assert.Equal(stockDeliveriesBefore, await db.DeliveryFromStocks.CountAsync());
        Assert.Empty(await db.CallOffDeliveries.ToListAsync());
        Assert.Empty(await db.DeliveryLegs.ToListAsync());
    }

    [Fact]
    public async Task PreviewRejectsAmbiguousDeliveries()
    {
        await using var db = CreateDb();
        var result = await CreateController(db, new FakeRoutingClient()).PreviewRoute(new CallOffRoutePreviewRequest
        {
            Deliveries = new() { new CallOffRoutePreviewDeliveryDto() },
        }, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task DeletingDeliveryRecalculatesRemainingRoute()
    {
        await using var db = CreateDb();
        await SeedAsync(db, linkedDeliveryIds: new[] { 20, 21 }, withExistingLeg: true);
        var client = SingleLegClient(AddressA);
        var link = await db.CallOffDeliveries.AsNoTracking().SingleAsync(x => x.DeliveryFromStockId == 21);

        var result = OkValue<CallOffDeliveryDeletedResponseDto>((await CreateController(db, client).DeleteCallOffDelivery(4, link.Id)).Result);

        Assert.Equal(CallOffRouteStatus.Calculated, result.RouteCalculation?.Status);
        Assert.Single(result.DeliveryLegs.DeliveryLegGroupedList);
        Assert.Empty(Assert.Single(client.RouteRequests).Intermediates);
        Assert.All(await db.DeliveryLegs.AsNoTracking().ToListAsync(), leg => Assert.Equal(20, leg.DeliveryFromStockId));
        Assert.False(string.IsNullOrEmpty(result.Version));
    }

    [Fact]
    public async Task PlannedDeliveryRecalculatesExistingCallOff()
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        var client = SingleLegClient(AddressA);

        var result = await CreateController(db, client).AddPlannedDelivery(4, new AddCallOffDeliveryFromStockRequest
        {
            CustomerOrderId = 10,
            EditionPerPallet = 100,
            NrOfItems = 200,
            NrOfPallets = 2,
            InventoryId = 1,
        }, CancellationToken.None);

        var aggregate = Assert.IsType<CallOffAggregateDto>(Assert.IsType<CreatedAtActionResult>(result).Value);
        Assert.Equal(CallOffRouteStatus.Calculated, aggregate.RouteCalculation?.Status);
        var newDeliveryId = Assert.Single(aggregate.CallOffDeliveryList).DeliveryFromStockId;
        Assert.Equal(newDeliveryId, Assert.Single(await db.DeliveryLegs.AsNoTracking().ToListAsync()).DeliveryFromStockId);
    }

    [Fact]
    public async Task PolishInventoryToSwedishAddressRoutesThroughPort()
    {
        await using var db = CreateDb();
        await SeedAsync(db, linkedDeliveryIds: new[] { 20 });
        (await db.Inventories.SingleAsync(x => x.Id == 1)).CountryCode = "PL";
        await db.SaveChangesAsync();
        var port = new GeoPoint(53.8996846, 14.2595374);
        var client = new FakeRoutingClient
        {
            Route = _ => new[]
            {
                new RouteLeg(Inventory, port, 30000, Array.Empty<RouteStep>()),
                new RouteLeg(port, AddressA, 40000, Array.Empty<RouteStep>()),
            },
        };

        var result = OkValue<CallOffRouteResponseDto>((await CreateController(db, client).RecalculateRoute(4, CancellationToken.None)).Result);

        Assert.Equal(CallOffRouteStatus.Calculated, result.RouteCalculation.Status);
        var request = Assert.Single(client.RouteRequests);
        Assert.Equal(new[] { port }, request.Intermediates);
        Assert.Single(await db.Positions.Where(x => x.Name == "WaypointPort").ToListAsync());
        Assert.Equal(2, await db.DeliveryLegs.CountAsync());
    }

    [Fact]
    public async Task EmptyCallOffAndMissingKeyNeverCallGoogle()
    {
        await using var db = CreateDb();
        await SeedAsync(db);
        var client = SingleLegClient(AddressA);
        var controller = CreateController(db, client);

        var empty = OkValue<CallOffRouteResponseDto>((await controller.RecalculateRoute(4, CancellationToken.None)).Result);
        Assert.Equal(CallOffRouteStatus.Empty, empty.RouteCalculation.Status);

        (await db.CompanyInfos.SingleAsync()).GoogleApiKey = " ";
        await db.SaveChangesAsync();
        var preview = OkValue<CallOffRouteResponseDto>((await controller.PreviewRoute(new CallOffRoutePreviewRequest
        {
            Deliveries = new() { new CallOffRoutePreviewDeliveryDto { DeliveryFromStockId = 20 } },
        }, CancellationToken.None)).Result);

        Assert.Equal(CallOffRouteStatus.Failed, preview.RouteCalculation.Status);
        Assert.Contains("Google API-nyckel", preview.RouteCalculation.Message);
        Assert.Empty(client.GeocodedAddresses);
        Assert.Empty(client.RouteRequests);
        Assert.Equal(404, Assert.IsType<NotFoundResult>((await controller.RecalculateRoute(99, CancellationToken.None)).Result).StatusCode);
    }

    private static ApplicationDbContext CreateDb(string? name = null) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString()).Options);

    private static CallOffController CreateController(ApplicationDbContext db, FakeRoutingClient client) =>
        new(db, new TestUserResolution(), new CallOffRouteService(db, client, NullLogger<CallOffRouteService>.Instance))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

    private static FakeRoutingClient SingleLegClient(GeoPoint destination) => new()
    {
        Route = _ => new[]
        {
            new RouteLeg(Inventory, destination, 45000, new[]
            {
                new RouteStep(Inventory, Midway, 1000, null, "DRIVE"),
                new RouteStep(Midway, destination, 44000, null, "DRIVE"),
            }),
        },
    };

    private static T OkValue<T>(IActionResult? result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);

    private static SaveCallOffAggregateRequest SaveRequest(CallOffAggregateDto current, int? customerDeliveryAddressId = null) => new()
    {
        Version = current.Version,
        DeliveryStatus = current.CallOff.DeliveryStatus,
        CustomerDeliveryAddressId = customerDeliveryAddressId ?? current.CallOff.CustomerDeliveryAddressId,
        CallOffDeliveryList = current.CallOffDeliveryList.Select(x => new SaveCallOffDeliveryDto { Id = x.Id }).ToList(),
    };

    private static async Task SeedAsync(ApplicationDbContext db, int[]? linkedDeliveryIds = null, bool withExistingLeg = false)
    {
        db.CompanyInfos.Add(new CompanyInfo { CompanyId = 1, GoogleApiKey = "dummy-key" });
        db.Inventories.Add(new Inventory
        {
            Id = 1, Name = "Testlager", Address = "Lagergatan 1", PostalNrText = "111 11", PostalAddress = "Lagerstad",
            Country = "SVERIGE", CountryCode = "SE", IsInventory = true,
        });
        db.CustomerDeliveryAddresses.AddRange(
            new CustomerDeliveryAddress
            {
                Id = 7, Name = "Kund A", Address = "Kundvägen 2", PostalNr = "222 22", PostalAddress = "Kundstad",
                Country = "SVERIGE", CountryCode = "SE",
            },
            new CustomerDeliveryAddress
            {
                Id = 8, Name = "Kund B", Address = "Kundvägen 3", PostalNr = "333 33", PostalAddress = "Andrastad",
                Country = "SVERIGE", CountryCode = "SE",
            });
        db.CustomerOrders.AddRange(
            new CustomerOrder { Id = 10, CustomerOrderNr = "1001", CustomerDeliveryAddressId = 7 },
            new CustomerOrder { Id = 11, CustomerOrderNr = "1002", CustomerDeliveryAddressId = 8 });
        db.DeliveryFromStocks.AddRange(
            new DeliveryFromStock { Id = 20, CustomerOrderId = 10, InventoryId = 1, NrOfPallets = 2 },
            new DeliveryFromStock { Id = 21, CustomerOrderId = 11, InventoryId = 1, NrOfPallets = 3 });
        db.CallOffs.Add(new CallOff { Id = 4, DeliveryStatus = 0 });

        var sortOrder = 1;
        foreach (var deliveryId in linkedDeliveryIds ?? Array.Empty<int>())
        {
            db.CallOffDeliveries.Add(new CallOffDelivery { CallOffId = 4, DeliveryFromStockId = deliveryId, SortOrder = sortOrder++ });
        }

        if (withExistingLeg)
        {
            db.DeliveryLegs.Add(new DeliveryLeg { CallOffId = 4, DeliveryFromStockId = 20, SortOrder = 1, DistanceKm = 999, TypeOfTransport = "VEHICLE" });
        }

        await db.SaveChangesAsync();
    }

    public sealed class FakeRoutingClient : IGoogleRoutingClient
    {
        private static readonly Dictionary<string, GeoPoint> Geocodes = new(StringComparer.Ordinal)
        {
            ["Lagergatan 1"] = Inventory,
            ["Kundvägen 2"] = AddressA,
            ["Kundvägen 3"] = AddressB,
        };

        public List<string> GeocodedAddresses { get; } = new();
        public List<RouteRequest> RouteRequests { get; } = new();
        public Func<RouteRequest, IReadOnlyList<RouteLeg>> Route { get; init; } = _ => Array.Empty<RouteLeg>();
        public Exception? RouteException { get; init; }
        public Func<Task>? BeforeRouteReturns { get; init; }

        public Task<GeoPoint?> GeocodeAsync(string apiKey, string address, CancellationToken cancellationToken)
        {
            Assert.Equal("dummy-key", apiKey);
            GeocodedAddresses.Add(address);
            var match = Geocodes.FirstOrDefault(x => address.StartsWith(x.Key, StringComparison.Ordinal));
            return Task.FromResult<GeoPoint?>(match.Value);
        }

        public async Task<IReadOnlyList<RouteLeg>> ComputeRouteAsync(string apiKey, RouteRequest request, CancellationToken cancellationToken)
        {
            Assert.Equal("dummy-key", apiKey);
            RouteRequests.Add(request);
            if (BeforeRouteReturns is not null)
            {
                await BeforeRouteReturns();
            }

            if (RouteException is not null)
            {
                throw RouteException;
            }

            return Route(request);
        }
    }

    private sealed class TestUserResolution : ILegacyUserResolutionService
    {
        public Task<LegacyUser?> ResolveCurrentUserAsync(ClaimsPrincipal user) => Task.FromResult<LegacyUser?>(null);
    }
}
