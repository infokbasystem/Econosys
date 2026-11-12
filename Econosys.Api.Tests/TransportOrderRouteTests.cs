using System.Security.Claims;
using Econosys.Api.Controllers;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Econosys.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Econosys.Api.Tests;

public class TransportOrderRouteTests
{
    private static readonly GeoPoint Factory = new(59, 18);
    private static readonly GeoPoint Stock = new(59.2, 18.2);
    private static readonly GeoPoint Customer = new(59.5, 18.5);

    [Fact]
    public async Task FactoryToCustomerStoresLegsAndPreviewDoesNotOverwriteThem()
    {
        await using var db = CreateDb();
        await Seed(db);
        var client = new FakeClient();
        var controller = Controller(db, client);
        var saved = Ok<CallOffRouteResponseDto>((await controller.RecalculateRoute(1, default)).Result);
        Assert.Equal(CallOffRouteStatus.Calculated, saved.RouteCalculation.Status);
        var leg = Assert.Single(await db.DeliveryLegs.AsNoTracking().ToListAsync());
        Assert.Equal((1, 21, 12m, "VEHICLE"), (leg.TransportOrderId, leg.DeliveryToCustomerId, leg.DistanceKm, leg.TypeOfTransport));
        Assert.Equal((Factory, Customer), (Assert.Single(client.Requests).Origin, client.Requests[0].Destination));

        var preview = Ok<CallOffRouteResponseDto>((await controller.PreviewRoute(1, new TransportOrderRoutePreviewRequest(), default)).Result);
        Assert.Equal(CallOffRouteStatus.Empty, preview.RouteCalculation.Status);
        Assert.Single(await db.DeliveryLegs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task FactoryToStockAndStockToCustomerUseCorrectEndpoints()
    {
        await using var db = CreateDb();
        await Seed(db);
        db.DeliveryToStocks.Add(new DeliveryToStock { Id = 22, SupplierOrderId = 10, InventoryId = 30 });
        db.DeliveryFromStocks.Add(new DeliveryFromStock { Id = 23, InventoryId = 30, CustomerOrderId = 11 });
        await db.SaveChangesAsync();
        var service = Service(db, new FakeClient());
        var factoryStock = await service.PreviewTransportOrderAsync(new[] { new TransportOrderRouteStop(1, 1, null, null, 22, 10, null, null) }, default);
        Assert.Equal(CallOffRouteStatus.Calculated, factoryStock.Status);
        var stockCustomer = await service.PreviewTransportOrderAsync(new[] { new TransportOrderRouteStop(2, 1, 23, null, null, 10, null, null) }, default);
        Assert.Equal(CallOffRouteStatus.Calculated, stockCustomer.Status);
        Assert.Equal((Factory.Latitude, Stock.Latitude), ((double)factoryStock.DeliveryLegs.DeliveryLegGroupedList[0].LatitudeStart!, (double)factoryStock.DeliveryLegs.DeliveryLegGroupedList[0].LatitudeEnd!));
        Assert.Equal((Stock.Latitude, Customer.Latitude), ((double)stockCustomer.DeliveryLegs.DeliveryLegGroupedList[0].LatitudeStart!, (double)stockCustomer.DeliveryLegs.DeliveryLegGroupedList[0].LatitudeEnd!));
    }

    [Fact]
    public async Task StockDeliveryRoutesDirectlyToCustomerLikeCallOff()
    {
        await using var db = CreateDb();
        await Seed(db);
        db.DeliveryFromStocks.Add(new DeliveryFromStock { Id = 23, InventoryId = 30, CustomerOrderId = 11 });
        db.CustomerDeliveryAddresses.Add(Address(41, "Factory", 40));
        await db.SaveChangesAsync();
        var client = new FakeClient();
        var result = await Service(db, client).PreviewTransportOrderAsync(
            new[] { new TransportOrderRouteStop(1, 1, 23, null, null, 10, null, null) }, default);
        Assert.Equal(CallOffRouteStatus.Calculated, result.Status);
        Assert.Empty(result.DeliveryLegs.DistributionDeliveryLegGroupedList);
        Assert.Equal((Stock, Customer), (Assert.Single(client.Requests).Origin, client.Requests[0].Destination));
    }

    [Fact]
    public async Task ForcedTransshipmentCreatesDistributionLeg()
    {
        await using var db = CreateDb();
        await Seed(db);
        var row = await db.TransportOrderDeliveries.SingleAsync();
        row.ForcedOmlastStatus = 1;
        row.OmlastInventoryId = 30;
        await db.SaveChangesAsync();
        var client = new FakeClient();
        var result = await Service(db, client).RecalculateTransportOrderAsync(1, default);
        Assert.Equal(CallOffRouteStatus.Calculated, result.Status);
        Assert.Equal(2, client.Requests.Count);
        Assert.Equal((Factory, Stock), (client.Requests[0].Origin, client.Requests[0].Destination));
        Assert.Equal((Stock, Customer), (client.Requests[1].Origin, client.Requests[1].Destination));
        var legs = await db.DeliveryLegs.AsNoTracking().OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(2, legs.Count);
        Assert.Equal("SPEDITION_VEHICLE", legs[1].TypeOfTransport);
        Assert.Equal(21, legs[1].DeliveryToCustomerId);
        Assert.Single(result.DeliveryLegs.DistributionDeliveryLegGroupedList);
    }

    [Fact]
    public async Task PreviewUsesDraftTransshipmentWithoutPersistingTheOverride()
    {
        await using var db = CreateDb();
        await Seed(db);
        (await db.Inventories.FindAsync(30))!.IsOmlast = true;
        await db.SaveChangesAsync();
        var client = new FakeClient();
        var response = Ok<CallOffRouteResponseDto>((await Controller(db, client).PreviewRoute(1,
            new TransportOrderRoutePreviewRequest
            {
                Deliveries = new() { new(50, 1) { ForcedOmlastStatus = 1, OmlastInventoryId = 30 } },
            }, default)).Result);
        Assert.Equal(CallOffRouteStatus.Calculated, response.RouteCalculation.Status);
        Assert.Equal((Factory, Stock), (client.Requests[0].Origin, client.Requests[0].Destination));
        Assert.Equal((Stock, Customer), (client.Requests[1].Origin, client.Requests[1].Destination));
        Assert.Null((await db.TransportOrderDeliveries.FindAsync(50))!.ForcedOmlastStatus);
        Assert.Empty(await db.DeliveryLegs.ToListAsync());
    }

    [Fact]
    public async Task DraftDirectModeSuppressesAutomaticTransshipment()
    {
        await using var db = CreateDb();
        await Seed(db);
        db.CustomerDeliveryAddresses.Add(Address(41, "Stock", 40));
        await db.SaveChangesAsync();
        var client = new FakeClient();
        var response = Ok<CallOffRouteResponseDto>((await Controller(db, client).PreviewRoute(1,
            new TransportOrderRoutePreviewRequest { Deliveries = new() { new(50, 1) { ForcedOmlastStatus = 2 } } }, default)).Result);
        Assert.Equal(CallOffRouteStatus.Calculated, response.RouteCalculation.Status);
        Assert.Equal((Factory, Customer), (Assert.Single(client.Requests).Origin, client.Requests[0].Destination));
        Assert.Empty(response.DeliveryLegs.DistributionDeliveryLegGroupedList);
    }

    [Fact]
    public async Task DraftTransshipmentRequiresAnEligibleWarehouse()
    {
        await using var db = CreateDb();
        await Seed(db);
        var response = await Controller(db, new FakeClient()).PreviewRoute(1,
            new TransportOrderRoutePreviewRequest { Deliveries = new() { new(50, 1) { ForcedOmlastStatus = 1, OmlastInventoryId = 30 } } }, default);
        Assert.IsType<BadRequestObjectResult>(response.Result);
    }

    [Fact]
    public async Task OmlastContextProvidesLegacyAddressesWeightAndEligibleWarehouses()
    {
        await using var db = CreateDb();
        await Seed(db);
        db.CustomerDeliveryAddresses.Add(new CustomerDeliveryAddress { Id = 41, Name = "Transfer", Address = "Stock", NextTransportDeliveryAddressId = 40, InventoryId = 30 });
        (await db.Inventories.FindAsync(30))!.IsOmlast = true;
        db.Products.Add(new Product { Id = 70, NetWeightPer1000 = 525 });
        db.Calculations.Add(new Calculation { Id = 71, ProductId = 70 });
        db.CalculationRows.Add(new CalculationRow { Id = 72, CalculationId = 71 });
        (await db.CustomerOrders.FindAsync(11))!.SelectedCalculationRowId = 72;
        var delivery = (await db.DeliveryToCustomers.FindAsync(21))!;
        delivery.NrOfItems = 220;
        delivery.NrOfPallets = 1;
        delivery.PalletIsStackable = true;
        delivery.PalletLength = 1200;
        delivery.PalletWidth = 800;
        await db.SaveChangesAsync();
        var controller = Controller(db, new FakeClient());
        var info = Ok<TransportOrderOmlastInfoDto>((await controller.GetOmlastInfo(1, 50, new DeliveryEditingService(db), default)).Result);
        Assert.True(info.IsOmlast);
        Assert.Equal(30, info.DefaultInventoryId);
        Assert.Contains("Transfer", info.DefaultCustomerAddress);
        Assert.Contains("Customer", info.DirectCustomerAddress);
        Assert.Equal(136m, info.DefaultTotalWeight);
        Assert.Equal(220, info.NrOfItems);
        Assert.Equal(1200, info.PalletLength);
        Assert.Equal(30, Assert.Single(info.Inventories).Id);
        Assert.IsType<NotFoundResult>((await controller.GetOmlastInfo(2, 50, new DeliveryEditingService(db), default)).Result);
    }

    [Theory]
    [InlineData(0, "Transfer", "Omlast / direkt")]
    [InlineData(1, "Stock", "Tvingad omlast")]
    [InlineData(2, "Customer", "Tvingad direkt")]
    public async Task SavedOmlastModeUpdatesCardRouteAndFreight(int mode, string addressText, string caption)
    {
        await using var db = CreateDb();
        await Seed(db);
        (await db.Inventories.FindAsync(30))!.IsOmlast = true;
        db.CustomerDeliveryAddresses.Add(new CustomerDeliveryAddress { Id = 41, Name = "Transfer", Address = "Stock", NextTransportDeliveryAddressId = 40 });
        await db.SaveChangesAsync();
        var costs = new TestCosts();
        var controller = new TransportOrderController(db, new TestUser(), costs, Service(db, new FakeClient()))
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var aggregate = Ok<TransportOrderAggregateDto>((await controller.GetAggregateById(1)).Result);
        var row = Assert.Single(aggregate.DeliveryOverview.TransportOrderDeliveryList).TransportOrderDelivery;
        row.ForcedOmlastStatus = mode;
        row.OmlastInventoryId = mode == 1 ? 30 : null;
        row.OmlastPalletPlaces = 1.5m;
        row.TotalWeight = 136m;
        row.OmlastNote = "Transfer note";
        row.OmlastInDate = new DateTime(2026, 11, 12);
        row.OmlastDeliveryDate = new DateTime(2026, 11, 13);
        var saved = Ok<TransportOrderAggregateDto>((await controller.SaveAggregateById(1, new SaveTransportOrderAggregateRequest
        {
            Version = aggregate.Version, DeliveryStatus = 0, TransportOrderDeliveryList = new() { row },
        })).Result);
        var card = Assert.Single(saved.DeliveryOverview.TransportOrderDeliveryList);
        Assert.Contains(addressText, card.CustomerNameAddress);
        Assert.Equal(caption, card.ForcedOmlastCaption);
        Assert.Equal(mode, card.TransportOrderDelivery.ForcedOmlastStatus);
        Assert.Equal(1.5m, card.TransportOrderDelivery.OmlastPalletPlaces);
        Assert.Equal(136m, card.TransportOrderDelivery.TotalWeight);
        Assert.Equal("Transfer note", card.TransportOrderDelivery.OmlastNote);
        Assert.Equal(1, costs.Calls);
        Assert.NotEqual(aggregate.Version, saved.Version);
    }

    private sealed class TestCosts : ITransportOrderCostCalculationService
    {
        public int Calls { get; private set; }
        public Task<TransportOrderCostCalculationResult> RecalculateAsync(int transportOrderId, int deliveryStatus)
        {
            Calls++;
            return Task.FromResult(TransportOrderCostCalculationResult.Ok(new TransportOrderCostCalc()));
        }
    }

    [Theory]
    [InlineData(3, 1, 136)]
    [InlineData(0, -1, 136)]
    [InlineData(0, 1000000, 136)]
    [InlineData(0, 1, -1)]
    [InlineData(0, 1, 1000000)]
    public async Task InvalidOmlastPayloadIsRejectedBeforeUpdatingTheTransportOrder(int mode, int places, int weight)
    {
        await using var db = CreateDb();
        await Seed(db);
        var result = await Controller(db, new FakeClient()).SaveAggregateById(1, new SaveTransportOrderAggregateRequest
        {
            Version = "validation-test",
            TransportOrderDeliveryList = new()
            {
                new() { Id = 50, ForcedOmlastStatus = mode, OmlastPalletPlaces = places, TotalWeight = weight },
            },
        });
        Assert.IsType<BadRequestObjectResult>(result.Result);
        var row = (await db.TransportOrderDeliveries.FindAsync(50))!;
        Assert.Null(row.ForcedOmlastStatus);
        Assert.Null(row.OmlastPalletPlaces);
        Assert.Null(row.TotalWeight);
    }

    [Fact]
    public async Task ToStockDeliveryCanBeExplicitlyRoutedDirectlyToCustomer()
    {
        await using var db = CreateDb();
        await Seed(db);
        (await db.SupplierOrders.FindAsync(10))!.CustomerDeliveryAddressId = 40;
        db.DeliveryToStocks.Add(new DeliveryToStock { Id = 22, SupplierOrderId = 10, InventoryId = 30 });
        await db.SaveChangesAsync();
        var client = new FakeClient();
        var result = await Service(db, client).PreviewTransportOrderAsync(
            new[] { new TransportOrderRouteStop(1, 1, null, null, 22, 10, 2, null) }, default);
        Assert.Equal(CallOffRouteStatus.Calculated, result.Status);
        Assert.Equal((Factory, Customer), (Assert.Single(client.Requests).Origin, client.Requests[0].Destination));
        Assert.Equal(30, (await db.DeliveryToStocks.FindAsync(22))!.InventoryId);
    }

    [Fact]
    public async Task ViaAddressCreatesDistributionLeg()
    {
        await using var db = CreateDb();
        await Seed(db);
        db.CustomerDeliveryAddresses.Add(Address(41, "Stock", 40));
        await db.SaveChangesAsync();
        var client = new FakeClient();
        var result = await Service(db, client).RecalculateTransportOrderAsync(1, default);
        Assert.Equal(CallOffRouteStatus.Calculated, result.Status);
        Assert.Single(result.DeliveryLegs.DistributionDeliveryLegGroupedList);
        Assert.Equal((Factory, Stock), (client.Requests[0].Origin, client.Requests[0].Destination));
        Assert.Equal((Stock, Customer), (client.Requests[1].Origin, client.Requests[1].Destination));
    }

    [Fact]
    public async Task FactoryViaInventoryCanAlsoBeTheUnloadingAddress()
    {
        await using var db = CreateDb();
        await Seed(db);
        db.CustomerDeliveryAddresses.Add(Address(41, "Stock", 40));
        (await db.SupplierFactories.SingleAsync()).ViaInventoryId = 30;
        await db.SaveChangesAsync();
        var client = new FakeClient();
        var result = await Service(db, client).RecalculateTransportOrderAsync(1, default);
        Assert.Equal(CallOffRouteStatus.Calculated, result.Status);
        Assert.Single(result.DeliveryLegs.DistributionDeliveryLegGroupedList);
    }

    [Fact]
    public async Task FactoryViaInventoryIsIncludedAsWaypoint()
    {
        await using var db = CreateDb();
        await Seed(db);
        (await db.SupplierFactories.SingleAsync()).ViaInventoryId = 30;
        await db.SaveChangesAsync();
        var client = new FakeClient();
        var result = await Service(db, client).RecalculateTransportOrderAsync(1, default);
        Assert.Equal(CallOffRouteStatus.Calculated, result.Status);
        var request = Assert.Single(client.Requests);
        Assert.Contains(Stock, new[] { request.Origin }.Concat(request.Intermediates));
    }

    [Fact]
    public async Task ChangedDeliveriesDuringCalculationDoNotSaveStaleLegs()
    {
        var name = Guid.NewGuid().ToString();
        await using var db = CreateDb(name);
        await Seed(db);
        var client = new FakeClient
        {
            BeforeRoute = async () =>
            {
                await using var other = CreateDb(name);
                (await other.TransportOrderDeliveries.SingleAsync()).SortOrder = 9;
                await other.SaveChangesAsync();
            },
        };
        var result = await Service(db, client).RecalculateTransportOrderAsync(1, default);
        Assert.Equal(CallOffRouteStatus.Stale, result.Status);
        Assert.Empty(await db.DeliveryLegs.ToListAsync());
    }

    [Fact]
    public async Task SavingChangedOrderClearsOldLegsAndReportsRoutingFailure()
    {
        await using var db = CreateDb();
        await Seed(db);
        db.DeliveryLegs.Add(new DeliveryLeg { TransportOrderId = 1, DeliveryToCustomerId = 21, DistanceKm = 999 });
        await db.SaveChangesAsync();
        var controller = Controller(db, new FakeClient { Fail = true });
        var aggregate = Ok<TransportOrderAggregateDto>((await controller.GetAggregateById(1)).Result);
        var row = Assert.Single(aggregate.DeliveryOverview.TransportOrderDeliveryList).TransportOrderDelivery;
        row.SortOrder = 2;
        var response = Ok<TransportOrderAggregateDto>((await controller.SaveAggregateById(1, new SaveTransportOrderAggregateRequest
        {
            Version = aggregate.Version,
            DeliveryStatus = aggregate.TransportOrder.DeliveryStatus,
            TransportOrderDeliveryList = new() { row },
        })).Result);
        Assert.Equal(CallOffRouteStatus.Failed, response.RouteCalculation?.Status);
        Assert.True(response.IsRouteMissing);
        Assert.Empty(await db.DeliveryLegs.AsNoTracking().ToListAsync());
        Assert.Equal(2, (await db.TransportOrderDeliveries.AsNoTracking().SingleAsync()).SortOrder);
    }

    [Fact]
    public async Task SavingReorderedDeliveryReplacesExistingLegs()
    {
        await using var db = CreateDb();
        await Seed(db);
        db.DeliveryLegs.Add(new DeliveryLeg { TransportOrderId = 1, DeliveryToCustomerId = 21, DistanceKm = 999 });
        await db.SaveChangesAsync();
        var client = new FakeClient();
        var controller = Controller(db, client);
        var aggregate = Ok<TransportOrderAggregateDto>((await controller.GetAggregateById(1)).Result);
        var row = Assert.Single(aggregate.DeliveryOverview.TransportOrderDeliveryList).TransportOrderDelivery;
        row.SortOrder = 2;
        var response = Ok<TransportOrderAggregateDto>((await controller.SaveAggregateById(1, new SaveTransportOrderAggregateRequest
        {
            Version = aggregate.Version,
            DeliveryStatus = aggregate.TransportOrder.DeliveryStatus,
            TransportOrderDeliveryList = new() { row },
        })).Result);
        Assert.Equal(CallOffRouteStatus.Calculated, response.RouteCalculation?.Status);
        Assert.False(response.IsRouteMissing);
        Assert.Equal(12m, Assert.Single(await db.DeliveryLegs.AsNoTracking().ToListAsync()).DistanceKm);
        Assert.Single(client.Requests);
    }

    [Fact]
    public async Task MultiStopRouteIncludesEveryLoadingAndUnloadingPoint()
    {
        await using var db = CreateDb();
        await Seed(db);
        db.DeliveryToStocks.Add(new DeliveryToStock { Id = 22, SupplierOrderId = 10, InventoryId = 30 });
        db.TransportOrderDeliveries.Add(new TransportOrderDelivery
        {
            Id = 51, TransportOrderId = 1, SupplierOrderId = 10, DeliveryToStockId = 22, SortOrder = 2,
        });
        await db.SaveChangesAsync();
        var client = new FakeClient();
        var result = await Service(db, client).RecalculateTransportOrderAsync(1, default);
        var request = Assert.Single(client.Requests);
        Assert.Equal(Factory, request.Origin);
        Assert.Equal(Customer, request.Destination);
        Assert.Equal(new[] { Stock }, request.Intermediates);
        Assert.Equal(CallOffRouteStatus.Calculated, result.Status);
        var legs = await db.DeliveryLegs.AsNoTracking().ToListAsync();
        Assert.Equal(2, legs.Count(x => x.DeliveryToCustomerId == 21));
        Assert.Single(legs.Where(x => x.DeliveryToStockId == 22));
    }

    [Fact]
    public async Task PreviewRejectsRowsFromAnotherOrder()
    {
        await using var db = CreateDb();
        await Seed(db);
        var result = await Controller(db, new FakeClient()).PreviewRoute(1,
            new TransportOrderRoutePreviewRequest { Deliveries = new() { new(999, 1) } }, default);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task PlanningDeliveryRecalculatesAndReportsFailureWithoutUndoingDelivery()
    {
        await using var db = CreateDb();
        await Seed(db);
        db.TransportOrderDeliveries.Remove(await db.TransportOrderDeliveries.SingleAsync());
        db.DeliveryLegs.Add(new DeliveryLeg { TransportOrderId = 1, DistanceKm = 999 });
        await db.SaveChangesAsync();
        var service = Service(db, new FakeClient { Fail = true });
        var controller = new LogisticsController(db, new TestUser(), service)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var response = Ok<PlanTransportOrderResponseDto>((await controller.PlanCustomerOrder(new PlanTransportOrderRequestDto
        {
            CustomerOrderId = 11, DeliveryId = 21, DeliveryType = "deliverytocustomer", TransportOrderId = 1,
        })).Result);
        Assert.Equal(CallOffRouteStatus.Failed, response.RouteCalculation?.Status);
        Assert.Single(await db.TransportOrderDeliveries.AsNoTracking().ToListAsync());
        Assert.Empty(await db.DeliveryLegs.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task RecalculationDoesNotTreatSlattPalletsAsRouteDeliveries()
    {
        await using var db = CreateDb();
        await Seed(db);
        (await db.DeliveryToCustomers.SingleAsync()).IsSlattPallet = true;
        await db.SaveChangesAsync();
        var controller = Controller(db, new FakeClient());
        var response = Ok<CallOffRouteResponseDto>((await controller.RecalculateRoute(1, default)).Result);
        Assert.Equal(CallOffRouteStatus.Empty, response.RouteCalculation.Status);
        var aggregate = Ok<TransportOrderAggregateDto>((await controller.GetAggregateById(1)).Result);
        Assert.False(aggregate.IsRouteMissing);
    }

    private static ApplicationDbContext CreateDb(string? name = null) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options);

    private static CallOffRouteService Service(ApplicationDbContext db, IGoogleRoutingClient client) =>
        new(db, client, NullLogger<CallOffRouteService>.Instance);

    private static TransportOrderController Controller(ApplicationDbContext db, IGoogleRoutingClient client) =>
        new(db, new TestUser(), null!, Service(db, client))
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };

    private static T Ok<T>(IActionResult? result) => Assert.IsType<T>(Assert.IsType<OkObjectResult>(result).Value);

    private static CustomerDeliveryAddress Address(int id, string street, int? next = null) =>
        new() { Id = id, Name = street, Address = street, PostalNr = "123", PostalAddress = "City", Country = "SVERIGE", CountryCode = "SE", NextTransportDeliveryAddressId = next };

    private static async Task Seed(ApplicationDbContext db)
    {
        db.CompanyInfos.Add(new CompanyInfo { CompanyId = 1, GoogleApiKey = "dummy-key" });
        db.TransportOrders.Add(new TransportOrder { Id = 1, TransportOrderNr = 1, DeliveryStatus = 0 });
        db.SupplierFactories.Add(new SupplierFactory { Id = 2, Name = "Factory", Address = "Factory", PostalNr = "123", City = "City", Country = "SVERIGE", CountryCode = "SE" });
        db.SupplierOrders.Add(new SupplierOrder { Id = 10, SupplierFactoryId = 2 });
        db.CustomerDeliveryAddresses.Add(Address(40, "Customer"));
        db.CustomerOrders.Add(new CustomerOrder { Id = 11, CustomerDeliveryAddressId = 40, SupplierOrderId = 10 });
        db.DeliveryToCustomers.Add(new DeliveryToCustomer { Id = 21, SupplierOrderId = 10, CustomerOrderId = 11 });
        db.Inventories.Add(new Inventory { Id = 30, Name = "Stock", Address = "Stock", PostalNrText = "123", PostalAddress = "City", Country = "SVERIGE", CountryCode = "SE" });
        db.TransportOrderDeliveries.Add(new TransportOrderDelivery { Id = 50, TransportOrderId = 1, DeliveryToCustomerId = 21, SupplierOrderId = 10, SortOrder = 1 });
        await db.SaveChangesAsync();
    }

    private sealed class TestUser : ILegacyUserResolutionService
    {
        public Task<LegacyUser?> ResolveCurrentUserAsync(ClaimsPrincipal user) => Task.FromResult<LegacyUser?>(null);
    }

    private sealed class FakeClient : IGoogleRoutingClient
    {
        public List<RouteRequest> Requests { get; } = new();
        public bool Fail { get; init; }
        public Func<Task>? BeforeRoute { get; init; }
        public Task<GeoPoint?> GeocodeAsync(string apiKey, string address, CancellationToken cancellationToken) =>
            Task.FromResult<GeoPoint?>(address.StartsWith("Factory") ? Factory : address.StartsWith("Stock") ? Stock : Customer);
        public async Task<IReadOnlyList<RouteLeg>> ComputeRouteAsync(string apiKey, RouteRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (BeforeRoute is not null) await BeforeRoute();
            if (Fail) throw new HttpRequestException("Fake routing failure");
            var points = new[] { request.Origin }.Concat(request.Intermediates).Append(request.Destination).ToArray();
            return points.Zip(points.Skip(1), (a, b) => new RouteLeg(a, b, 12000, Array.Empty<RouteStep>())).ToArray();
        }
    }
}
