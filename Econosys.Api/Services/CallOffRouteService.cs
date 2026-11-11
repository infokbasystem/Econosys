using System.Data;
using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Services
{
    public static class CallOffRouteStatus
    {
        public const string Calculated = "Calculated";
        public const string Empty = "Empty";
        public const string Partial = "Partial";
        public const string Failed = "Failed";
        public const string Stale = "Stale";
    }

    /// <summary>A delivery on the call-off route; either an existing stock delivery or an unsaved draft (customer order + inventory).</summary>
    public sealed record CallOffRouteStop(int? DeliveryFromStockId, int? CustomerOrderId = null, int? InventoryId = null);

    public sealed record CallOffRouteResult(string Status, string? Message, TransportOrderDeliveryLegsResponseDto DeliveryLegs);

    public interface ICallOffRouteService
    {
        /// <summary>Calculates legs for unsaved changes without touching call-off, delivery or leg data.</summary>
        Task<CallOffRouteResult> PreviewAsync(int? customerDeliveryAddressId, IReadOnlyList<CallOffRouteStop> stops, CancellationToken cancellationToken);

        /// <summary>Recalculates and stores the legs of a saved call-off, unless it changed during the calculation.</summary>
        Task<CallOffRouteResult> RecalculateAsync(int callOffId, CancellationToken cancellationToken);
    }

    // Legacy parity: EconosysService.GetDeliveryLegsFromDeliveryList (call-off branch) and Utils.Google.GetFerrySpliitedSteps.
    public sealed partial class CallOffRouteService : ICallOffRouteService, ITransportOrderRouteService
    {
        private const double EarthRadiusMeters = 6376500;
        private const double LegMatchDistanceMeters = 200;
        private const string WaypointPortName = "WaypointPort";
        private static readonly GeoPoint DefaultPort = new(53.898982271453576, 14.258273430833828);
        private const string IncompleteAddressText = "saknar fullständig adress (gata, postnummer, ort, land och landskod)";

        private readonly ApplicationDbContext _dbContext;
        private readonly IGoogleRoutingClient _routingClient;
        private readonly ILogger<CallOffRouteService> _logger;

        public CallOffRouteService(ApplicationDbContext dbContext, IGoogleRoutingClient routingClient, ILogger<CallOffRouteService> logger)
        {
            _dbContext = dbContext;
            _routingClient = routingClient;
            _logger = logger;
        }

        public async Task<CallOffRouteResult> PreviewAsync(int? customerDeliveryAddressId, IReadOnlyList<CallOffRouteStop> stops, CancellationToken cancellationToken)
        {
            var calculation = await CalculateAsync(null, customerDeliveryAddressId, stops, cancellationToken);
            return ToResult(calculation);
        }

        public async Task<CallOffRouteResult> RecalculateAsync(int callOffId, CancellationToken cancellationToken)
        {
            var input = await LoadCallOffInputAsync(callOffId, cancellationToken);
            if (input is null)
            {
                return new CallOffRouteResult(CallOffRouteStatus.Failed, "Avropet kunde inte hittas.", GroupLegs(Array.Empty<TransportOrderDeliveryLegDto>()));
            }

            var calculation = input.Stops.Count == 0
                ? Calculation.EmptyRoute()
                : await CalculateAsync(callOffId, input.CustomerDeliveryAddressId, input.Stops, cancellationToken);

            if (calculation.Status == CallOffRouteStatus.Failed)
            {
                return ToResult(calculation);
            }

            var transaction = _dbContext.Database.IsRelational()
                ? await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;
            try
            {
                // Concurrency guard: never store legs calculated for deliveries/addresses that have changed since.
                var current = await LoadCallOffInputAsync(callOffId, cancellationToken);
                if (current is null || current.Key != input.Key)
                {
                    if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                    return new CallOffRouteResult(
                        CallOffRouteStatus.Stale,
                        "Avropet ändrades medan transportbenen beräknades. Försök igen.",
                        GroupLegs(Array.Empty<TransportOrderDeliveryLegDto>()));
                }

                var existingLegs = await _dbContext.DeliveryLegs.Where(x => x.CallOffId == callOffId).ToListAsync(cancellationToken);
                _dbContext.DeliveryLegs.RemoveRange(existingLegs);
                foreach (var leg in calculation.Legs)
                {
                    leg.CallOffId = callOffId;
                    _dbContext.DeliveryLegs.Add(leg);
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning("Saving delivery legs for call-off {CallOffId} failed ({ErrorType}).", callOffId, ex.GetType().Name);
                _dbContext.ChangeTracker.Clear();
                return new CallOffRouteResult(
                    CallOffRouteStatus.Failed,
                    "Transportbenen kunde inte sparas. Försök igen.",
                    GroupLegs(Array.Empty<TransportOrderDeliveryLegDto>()));
            }
            finally
            {
                if (transaction is not null) await transaction.DisposeAsync();
            }

            return ToResult(calculation);
        }

        public static TransportOrderDeliveryLegsResponseDto GroupLegs(IReadOnlyList<TransportOrderDeliveryLegDto> deliveryLegList)
        {
            var mainLegs = deliveryLegList.Where(x => !IsDistributionLeg(x)).ToList();
            var distributionLegs = deliveryLegList.Where(IsDistributionLeg).ToList();

            return new TransportOrderDeliveryLegsResponseDto
            {
                DeliveryLegGroupedList = mainLegs
                    .GroupBy(x => new { x.SortOrder, x.LatitudeStart, x.LongitudeStart })
                    .OrderBy(x => x.Key.SortOrder)
                    .Select(x => x.First())
                    .ToList(),
                DistributionDeliveryLegGroupedList = distributionLegs
                    .GroupBy(x => new { x.SortOrder, x.LatitudeStart, x.LongitudeStart })
                    .OrderBy(x => x.Key.SortOrder)
                    .Select(x => x.First())
                    .ToList(),
                DistributionLegList = distributionLegs
                    .GroupBy(x => new { x.DeliveryToCustomerId, x.DeliveryToStockId })
                    .Select(x => x.OrderBy(y => y.SortOrder).ToList())
                    .ToList(),
            };
        }

        private static bool IsDistributionLeg(TransportOrderDeliveryLegDto deliveryLeg) =>
            !string.IsNullOrWhiteSpace(deliveryLeg.TypeOfTransport)
            && deliveryLeg.TypeOfTransport.Contains("SPEDITION", StringComparison.OrdinalIgnoreCase);

        // Legacy parity: Utils.Google.GetFerrySpliitedSteps splits a route leg into road and ferry parts.
        public static List<RouteStep> SplitFerrySteps(RouteLeg leg)
        {
            var calculatedSteps = new List<RouteStep>();
            if (!leg.Steps.Any(IsFerry))
            {
                calculatedSteps.Add(new RouteStep(leg.Start, leg.End, leg.DistanceMeters, null, "VEHICLE"));
            }
            else
            {
                RouteStep? firstStepBeforeFerry = null;
                RouteStep? lastStepBeforeFerry = null;
                var metersInSteps = 0;
                foreach (var step in leg.Steps)
                {
                    if (IsFerry(step))
                    {
                        if (firstStepBeforeFerry is not null && lastStepBeforeFerry is not null)
                        {
                            calculatedSteps.Add(new RouteStep(firstStepBeforeFerry.Start, lastStepBeforeFerry.End, metersInSteps, null, "VEHICLE"));
                        }

                        calculatedSteps.Add(new RouteStep(step.Start, step.End, step.DistanceMeters, null, "FERRY"));
                        firstStepBeforeFerry = null;
                        lastStepBeforeFerry = null;
                        metersInSteps = 0;
                    }
                    else
                    {
                        metersInSteps += step.DistanceMeters;
                        firstStepBeforeFerry ??= step;
                        lastStepBeforeFerry = step;
                    }
                }

                if (metersInSteps != 0 && firstStepBeforeFerry is not null && lastStepBeforeFerry is not null)
                {
                    calculatedSteps.Add(new RouteStep(firstStepBeforeFerry.Start, lastStepBeforeFerry.End, metersInSteps, null, "VEHICLE"));
                }
            }

            // A short road part followed by more of the route is only the forced port waypoint, so legacy drops it.
            var shortRoadIndex = calculatedSteps.FindIndex(x => x.TravelMode.StartsWith("VEHICLE", StringComparison.Ordinal) && x.DistanceMeters < 500);
            if (shortRoadIndex >= 0 && shortRoadIndex < calculatedSteps.Count - 1)
            {
                calculatedSteps.RemoveAt(shortRoadIndex);
            }

            return calculatedSteps;
        }

        private static bool IsFerry(RouteStep step) => step.Maneuver == "FERRY";

        private async Task<Calculation> CalculateAsync(int? callOffId, int? customerDeliveryAddressId, IReadOnlyList<CallOffRouteStop> stops, CancellationToken cancellationToken)
        {
            if (stops.Count == 0)
            {
                return Calculation.EmptyRoute();
            }

            try
            {
                var apiKey = (await _dbContext.CompanyInfos.AsNoTracking().Select(x => x.GoogleApiKey).FirstOrDefaultAsync(cancellationToken))?.Trim();
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    return Calculation.Failure("Google API-nyckel saknas i Företagsinfo.");
                }

                var resolved = await ResolveStopsAsync(customerDeliveryAddressId, stops, apiKey, cancellationToken);
                if (resolved.Errors.Count > 0)
                {
                    return Calculation.Failure(string.Join(" ", resolved.Errors.Distinct()));
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                return await CalculateRouteAsync(resolved.Stops, apiKey, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                var googleError = ex as GoogleRoutingException;
                _logger.LogWarning(
                    "Route calculation for call-off {CallOffId} failed ({ErrorType}, status {StatusCode}, Google: {GoogleMessage}).",
                    callOffId,
                    ex.GetType().Name,
                    googleError?.StatusCode,
                    googleError?.GoogleMessage);
                return Calculation.Failure(string.IsNullOrWhiteSpace(googleError?.GoogleMessage)
                    ? "Transportbenen kunde inte beräknas eftersom Google inte svarade korrekt. Försök igen."
                    : $"Transportbenen kunde inte beräknas. Google svarade: {googleError.GoogleMessage}");
            }
        }

        private async Task<(List<ResolvedStop> Stops, List<string> Errors)> ResolveStopsAsync(
            int? customerDeliveryAddressId, IReadOnlyList<CallOffRouteStop> stops, string apiKey, CancellationToken cancellationToken)
        {
            var errors = new List<string>();
            var deliveryIds = stops.Where(x => x.DeliveryFromStockId.HasValue).Select(x => x.DeliveryFromStockId!.Value).Distinct().ToList();
            var deliveries = await _dbContext.DeliveryFromStocks
                .AsNoTracking()
                .Where(x => deliveryIds.Contains(x.Id))
                .Select(x => new { x.Id, x.InventoryId, x.CustomerOrderId })
                .ToDictionaryAsync(x => x.Id, cancellationToken);

            var orderIds = stops.Where(x => x.CustomerOrderId.HasValue).Select(x => x.CustomerOrderId!.Value)
                .Concat(deliveries.Values.Where(x => x.CustomerOrderId.HasValue).Select(x => x.CustomerOrderId!.Value))
                .Distinct()
                .ToList();
            var orders = await _dbContext.CustomerOrders
                .AsNoTracking()
                .Where(x => orderIds.Contains(x.Id))
                .Select(x => new
                {
                    x.Id,
                    x.CustomerOrderNr,
                    x.CustomerDeliveryAddressId,
                    SupplierInventoryId = x.SupplierOrder != null ? x.SupplierOrder.InventoryId : null,
                })
                .ToDictionaryAsync(x => x.Id, cancellationToken);

            var plans = new List<(CallOffRouteStop Stop, string OrderLabel, int? InventoryId, int? AddressId)>();
            foreach (var stop in stops)
            {
                var delivery = stop.DeliveryFromStockId.HasValue && deliveries.TryGetValue(stop.DeliveryFromStockId.Value, out var foundDelivery)
                    ? foundDelivery
                    : null;
                if (stop.DeliveryFromStockId.HasValue && delivery is null)
                {
                    errors.Add("En leverans på avropet kunde inte hittas.");
                    continue;
                }

                var orderId = stop.CustomerOrderId ?? delivery?.CustomerOrderId;
                var order = orderId.HasValue && orders.TryGetValue(orderId.Value, out var foundOrder) ? foundOrder : null;
                var orderLabel = string.IsNullOrWhiteSpace(order?.CustomerOrderNr) ? "leveransen" : $"order {order!.CustomerOrderNr}";
                plans.Add((stop, orderLabel, stop.InventoryId ?? delivery?.InventoryId ?? order?.SupplierInventoryId, customerDeliveryAddressId ?? order?.CustomerDeliveryAddressId));
            }

            var inventoryIds = plans.Where(x => x.InventoryId.HasValue).Select(x => x.InventoryId!.Value).Distinct().ToList();
            var inventories = await _dbContext.Inventories.Where(x => inventoryIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
            var addressIds = plans.Where(x => x.AddressId.HasValue).Select(x => x.AddressId!.Value).Distinct().ToList();
            var addresses = await _dbContext.CustomerDeliveryAddresses.Where(x => addressIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);

            var positionCache = new Dictionary<string, Position>(StringComparer.Ordinal);
            var resolvedStops = new List<ResolvedStop>();
            foreach (var plan in plans)
            {
                Position? from = null;
                if (plan.InventoryId is not int inventoryId || !inventories.TryGetValue(inventoryId, out var inventory))
                {
                    errors.Add($"Lager saknas för {plan.OrderLabel}.");
                }
                else
                {
                    var postalNr = !string.IsNullOrWhiteSpace(inventory.PostalNrText)
                        ? inventory.PostalNrText
                        : GoogleRoutingClient.Invariant(inventory.PostalNr);
                    var (position, error) = await ResolvePositionAsync(
                        inventory.Name, inventory.Address, postalNr, inventory.PostalAddress, inventory.Country, inventory.CountryCode,
                        $"Lagret {inventory.Name}", positionCache, apiKey, cancellationToken);
                    if (position is null)
                    {
                        errors.Add(error!);
                    }
                    else
                    {
                        inventory.PositionId ??= position.Id;
                        from = position;
                    }
                }

                Position? to = null;
                if (plan.AddressId is not int addressId || !addresses.TryGetValue(addressId, out var address))
                {
                    errors.Add($"Leveransadress saknas för {plan.OrderLabel}.");
                }
                else
                {
                    var postalNr = address.PostalNrValue.HasValue ? GoogleRoutingClient.Invariant(address.PostalNrValue) : address.PostalNr;
                    var (position, error) = await ResolvePositionAsync(
                        address.Name, address.Address, postalNr, address.PostalAddress, address.Country, address.CountryCode,
                        $"Leveransadressen {address.Name}", positionCache, apiKey, cancellationToken);
                    if (position is null)
                    {
                        errors.Add(error!);
                    }
                    else
                    {
                        address.PositionId ??= position.Id;
                        to = position;
                    }
                }

                if (from is not null && to is not null)
                {
                    resolvedStops.Add(new ResolvedStop(plan.Stop, from, to));
                }
            }

            return (resolvedStops, errors);
        }

        private async Task<(Position? Position, string? Error)> ResolvePositionAsync(
            string? name, string? street, string? postalNr, string? city, string? country, string? countryCode,
            string label, Dictionary<string, Position> cache, string apiKey, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(street) || string.IsNullOrWhiteSpace(postalNr) || string.IsNullOrWhiteSpace(city)
                || string.IsNullOrWhiteSpace(country) || string.IsNullOrWhiteSpace(countryCode))
            {
                return (null, $"{label} {IncompleteAddressText}.");
            }

            street = Truncate(street.Trim(), 200);
            postalNr = Truncate(postalNr.Trim(), 50);
            city = Truncate(city.Trim(), 100);
            countryCode = Truncate(countryCode.Trim(), 10);
            var cacheKey = string.Join("|", street, postalNr, city, countryCode);
            if (cache.TryGetValue(cacheKey, out var cached))
            {
                return (cached, null);
            }

            var position = _dbContext.Positions.Local.FirstOrDefault(x => x.Street == street && x.PostalNr == postalNr && x.PostalAddress == city && x.CountryCode == countryCode)
                ?? await _dbContext.Positions.FirstOrDefaultAsync(
                    x => x.Street == street && x.PostalNr == postalNr && x.PostalAddress == city && x.CountryCode == countryCode,
                    cancellationToken);

            if (position?.Latitude is null || position.Longitude is null)
            {
                var point = await _routingClient.GeocodeAsync(apiKey, $"{street}, {postalNr} {city}, {country.Trim()}", cancellationToken);
                if (point is null)
                {
                    return (null, $"{label} kunde inte hittas på kartan.");
                }

                if (position is null)
                {
                    position = new Position
                    {
                        Name = Truncate(name?.Trim(), 100),
                        Street = street,
                        PostalNr = postalNr,
                        PostalAddress = city,
                        Country = Truncate(country.Trim(), 100),
                        CountryCode = countryCode,
                    };
                    _dbContext.Positions.Add(position);
                }

                position.Latitude = RoundCoordinate(point.Latitude);
                position.Longitude = RoundCoordinate(point.Longitude);
                // Legacy parity: geocoded positions are cached in the Position table for reuse.
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            cache[cacheKey] = position;
            return (position, null);
        }

        private async Task<Calculation> CalculateRouteAsync(List<ResolvedStop> stops, string apiKey, CancellationToken cancellationToken)
        {
            var routePoints = new List<RoutePoint>();
            foreach (var stop in stops)
            {
                AddRoutePoint(routePoints, stop.From, isLoading: true);
                AddRoutePoint(routePoints, stop.To, isLoading: false);
                if (stop.Via is not null) AddRoutePoint(routePoints, stop.Via, isLoading: true);
            }

            // Legacy parity: deliveries from Poland to Sweden are forced through the Świnoujście port.
            if (routePoints.Any(x => x.IsLoading && x.Position.CountryCode == "PL")
                && routePoints.Any(x => x.IsUnloading && x.Position.CountryCode == "SE"))
            {
                var port = await _dbContext.Positions.FirstOrDefaultAsync(x => x.Name == WaypointPortName, cancellationToken);
                if (port is null)
                {
                    port = new Position
                    {
                        Name = WaypointPortName,
                        Country = "POLEN",
                        CountryCode = "PL",
                        PostalAddress = "PORT",
                        PostalNr = string.Empty,
                        Street = string.Empty,
                        Latitude = 53.8996846m,
                        Longitude = 14.2595374m,
                    };
                    _dbContext.Positions.Add(port);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }

                AddRoutePoint(routePoints, port, isLoading: true);
            }

            var deliveryLegs = new List<DeliveryLeg>();
            var unmatchedDeliveries = 0;
            if (routePoints.Count < 2)
            {
                return new Calculation(CallOffRouteStatus.Partial, "Lastning och lossning sker på samma plats, så inga transportben behövs.", deliveryLegs, routePoints);
            }

            if (routePoints.Count == 2)
            {
                var legs = await _routingClient.ComputeRouteAsync(
                    apiKey,
                    new RouteRequest(ToPoint(routePoints[0].Position), ToPoint(routePoints[1].Position), Array.Empty<GeoPoint>()),
                    cancellationToken);
                if (legs.Count != 1)
                {
                    return Calculation.Failure("Google hittade ingen körbar rutt mellan lastning och lossning.");
                }

                foreach (var stop in stops)
                {
                    deliveryLegs.AddRange(SplitFerrySteps(legs[0]).Select(step => CreateLeg(stop, step, 1, routePoints)));
                }
            }
            else
            {
                foreach (var point in routePoints.Where(x => x.IsLoading))
                {
                    point.MetersFromDefaultPort = GetDistanceMeters(ToPoint(point.Position), DefaultPort);
                }

                var origin = routePoints.Where(x => x.IsLoading).OrderByDescending(x => x.MetersFromDefaultPort).First();
                var destination = routePoints.FirstOrDefault(x => x.IsUnloading && x != origin);
                if (destination is null)
                {
                    return Calculation.Failure("Ingen lossningsplats hittades för avropet.");
                }

                // Legacy listed the destination twice and skipped the last unloading; every other stop is an intermediate here.
                var intermediates = routePoints.Where(x => x.IsUnloading && x != destination && x != origin)
                    .Concat(routePoints.Where(x => x.IsLoading && !x.IsUnloading && x != origin && x != destination))
                    .Select(x => ToPoint(x.Position))
                    .ToList();
                var legs = await _routingClient.ComputeRouteAsync(
                    apiKey,
                    new RouteRequest(ToPoint(origin.Position), ToPoint(destination.Position), intermediates),
                    cancellationToken);
                if (legs.Count == 0)
                {
                    return Calculation.Failure("Google hittade ingen körbar rutt för avropet.");
                }

                foreach (var stop in stops)
                {
                    var startFound = false;
                    var endFound = false;
                    var matchedLegs = 0;
                    var firstLegIndex = deliveryLegs.Count;
                    for (var index = 0; index < legs.Count; index++)
                    {
                        var leg = legs[index];
                        startFound |= GetDistanceMeters(ToPoint(stop.From), leg.Start) <= LegMatchDistanceMeters;
                        endFound = startFound && GetDistanceMeters(ToPoint(stop.To), leg.End) <= LegMatchDistanceMeters;
                        if (startFound)
                        {
                            matchedLegs++;
                            deliveryLegs.AddRange(SplitFerrySteps(leg).Select(step => CreateLeg(stop, step, index + 1, routePoints)));
                        }

                        if (endFound)
                        {
                            break;
                        }
                    }

                    if (matchedLegs == 0 || !endFound)
                    {
                        deliveryLegs.RemoveRange(firstLegIndex, deliveryLegs.Count - firstLegIndex);
                        unmatchedDeliveries++;
                    }
                }
            }

            foreach (var stop in stops.Where(x => x.DistributionTo is not null))
            {
                var destination = stop.DistributionTo!;
                AddRoutePoint(routePoints, destination, isLoading: false);
                if (ToPoint(stop.To) == ToPoint(destination)) continue;
                var distribution = await _routingClient.ComputeRouteAsync(
                    apiKey, new RouteRequest(ToPoint(stop.To), ToPoint(destination), Array.Empty<GeoPoint>()), cancellationToken);
                if (distribution.Count != 1)
                {
                    return Calculation.Failure("Google hittade ingen körbar distributionsrutt.");
                }

                foreach (var step in SplitFerrySteps(distribution[0]))
                {
                    var leg = CreateLeg(stop, step, 0, routePoints);
                    leg.FromPositionId = stop.To.Id;
                    leg.ToPositionId = destination.Id;
                    leg.TypeOfTransport = $"SPEDITION_{step.TravelMode}";
                    deliveryLegs.Add(leg);
                }
            }

            return unmatchedDeliveries > 0
                ? new Calculation(
                    CallOffRouteStatus.Partial,
                    unmatchedDeliveries == 1
                        ? "Rutten kunde inte kopplas till en av leveranserna. Kontrollera adresserna."
                        : $"Rutten kunde inte kopplas till {unmatchedDeliveries} leveranser. Kontrollera adresserna.",
                    deliveryLegs,
                    routePoints)
                : new Calculation(CallOffRouteStatus.Calculated, null, deliveryLegs, routePoints);
        }

        private static DeliveryLeg CreateLeg(ResolvedStop stop, RouteStep step, int sortOrder, List<RoutePoint> routePoints) => new()
        {
            DeliveryFromStockId = stop.Stop.DeliveryFromStockId,
            DeliveryToCustomerId = stop.DeliveryToCustomerId,
            DeliveryToStockId = stop.DeliveryToStockId,
            DistanceKm = Math.Round(step.DistanceMeters / 1000m, 2),
            LatitudeStart = RoundCoordinate(step.Start.Latitude),
            LongitudeStart = RoundCoordinate(step.Start.Longitude),
            LatitudeEnd = RoundCoordinate(step.End.Latitude),
            LongitudeEnd = RoundCoordinate(step.End.Longitude),
            TypeOfTransport = step.TravelMode,
            SortOrder = sortOrder,
            FromPositionId = stop.From.Id,
            ToPositionId = stop.To.Id,
            FromPositionIdLeg = FindNearestPosition(routePoints, step.Start)?.Id,
            ToPositionIdLeg = FindNearestPosition(routePoints, step.End)?.Id,
        };

        // Legacy parity: Utils.Geo.FindPostitionIdFromList returns the nearest route stop without a distance limit.
        private static Position? FindNearestPosition(List<RoutePoint> routePoints, GeoPoint point) => routePoints
            .Where(x => x.Position.Id != 0)
            .OrderBy(x => GetDistanceMeters(ToPoint(x.Position), point))
            .Select(x => x.Position)
            .FirstOrDefault();

        private static void AddRoutePoint(List<RoutePoint> routePoints, Position position, bool isLoading)
        {
            var existing = routePoints.FirstOrDefault(x => x.Position.Latitude == position.Latitude && x.Position.Longitude == position.Longitude);
            if (existing is null)
            {
                routePoints.Add(new RoutePoint(position, isLoading));
            }
            else if (isLoading) existing.IsLoading = true;
            else existing.IsUnloading = true;
        }

        private async Task<CallOffInput?> LoadCallOffInputAsync(int callOffId, CancellationToken cancellationToken)
        {
            var callOff = await _dbContext.CallOffs
                .AsNoTracking()
                .Where(x => x.Id == callOffId)
                .Select(x => new { x.CustomerDeliveryAddressId })
                .FirstOrDefaultAsync(cancellationToken);
            if (callOff is null)
            {
                return null;
            }

            var rows = await _dbContext.CallOffDeliveries
                .AsNoTracking()
                .Where(x => x.CallOffId == callOffId && x.DeliveryFromStockId.HasValue)
                .OrderBy(x => x.SortOrder)
                .ThenBy(x => x.Id)
                .Select(x => new
                {
                    DeliveryFromStockId = x.DeliveryFromStockId!.Value,
                    InventoryId = x.DeliveryFromStock != null ? x.DeliveryFromStock.InventoryId : null,
                    OrderAddressId = x.DeliveryFromStock != null && x.DeliveryFromStock.CustomerOrder != null
                        ? x.DeliveryFromStock.CustomerOrder.CustomerDeliveryAddressId
                        : null,
                })
                .ToListAsync(cancellationToken);

            var key = $"{callOff.CustomerDeliveryAddressId}#" + string.Join("|", rows.Select(x => $"{x.DeliveryFromStockId}:{x.InventoryId}:{x.OrderAddressId}"));
            return new CallOffInput(
                callOff.CustomerDeliveryAddressId,
                rows.Select(x => new CallOffRouteStop(x.DeliveryFromStockId)).ToList(),
                key);
        }

        private static CallOffRouteResult ToResult(Calculation calculation)
        {
            var positionsById = calculation.RoutePoints
                .Where(x => x.Position.Id != 0)
                .GroupBy(x => x.Position.Id)
                .ToDictionary(x => x.Key, x => x.First().Position);

            var legs = calculation.Legs.Select(leg => new TransportOrderDeliveryLegDto
            {
                Id = leg.Id,
                TransportOrderId = leg.TransportOrderId,
                CallOffId = leg.CallOffId,
                DeliveryFromStockId = leg.DeliveryFromStockId,
                DeliveryToCustomerId = leg.DeliveryToCustomerId,
                DeliveryToStockId = leg.DeliveryToStockId,
                FromPositionId = leg.FromPositionId,
                ToPositionId = leg.ToPositionId,
                LegFromPositionId = leg.FromPositionIdLeg,
                LegToPositionId = leg.ToPositionIdLeg,
                DistanceKm = leg.DistanceKm,
                TypeOfTransport = leg.TypeOfTransport,
                SortOrder = leg.SortOrder,
                LatitudeStart = leg.LatitudeStart,
                LongitudeStart = leg.LongitudeStart,
                LatitudeEnd = leg.LatitudeEnd,
                LongitudeEnd = leg.LongitudeEnd,
                HelppropFromPositionName = Lookup(positionsById, leg.FromPositionId)?.Name ?? string.Empty,
                HelppropFromPositionPostalAddress = Lookup(positionsById, leg.FromPositionId)?.PostalAddress ?? string.Empty,
                HelppropToPositionName = Lookup(positionsById, leg.ToPositionId)?.Name ?? string.Empty,
                HelppropToPositionPostalAddress = Lookup(positionsById, leg.ToPositionId)?.PostalAddress ?? string.Empty,
                HelppropLegFromPositionName = Lookup(positionsById, leg.FromPositionIdLeg)?.Name ?? string.Empty,
                HelppropLegFromPositionPostalAddress = Lookup(positionsById, leg.FromPositionIdLeg)?.PostalAddress ?? string.Empty,
                HelppropLegToPositionName = Lookup(positionsById, leg.ToPositionIdLeg)?.Name ?? string.Empty,
                HelppropLegToPositionPostalAddress = Lookup(positionsById, leg.ToPositionIdLeg)?.PostalAddress ?? string.Empty,
            }).ToList();

            return new CallOffRouteResult(calculation.Status, calculation.Message, GroupLegs(legs));
        }

        private static Position? Lookup(Dictionary<int, Position> positionsById, int? id) =>
            id.HasValue && positionsById.TryGetValue(id.Value, out var position) ? position : null;

        private static GeoPoint ToPoint(Position position) => new((double)position.Latitude!.Value, (double)position.Longitude!.Value);

        private static decimal RoundCoordinate(double value) => Math.Round((decimal)value, 8);

        private static string? Truncate(string? value, int maxLength) =>
            value is null || value.Length <= maxLength ? value : value[..maxLength];

        // Haversine distance with the same earth radius as System.Device.Location.GeoCoordinate used by legacy.
        private static double GetDistanceMeters(GeoPoint a, GeoPoint b)
        {
            var dLat = DegreesToRadians(b.Latitude - a.Latitude);
            var dLng = DegreesToRadians(b.Longitude - a.Longitude);
            var h = Math.Pow(Math.Sin(dLat / 2), 2)
                + Math.Cos(DegreesToRadians(a.Latitude)) * Math.Cos(DegreesToRadians(b.Latitude)) * Math.Pow(Math.Sin(dLng / 2), 2);
            return EarthRadiusMeters * 2 * Math.Atan2(Math.Sqrt(h), Math.Sqrt(1 - h));
        }

        private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;

        private sealed record ResolvedStop(
            CallOffRouteStop Stop, Position From, Position To,
            int? DeliveryToCustomerId = null, int? DeliveryToStockId = null,
            Position? Via = null, Position? DistributionTo = null);

        private sealed record CallOffInput(int? CustomerDeliveryAddressId, List<CallOffRouteStop> Stops, string Key);

        private sealed class RoutePoint
        {
            public RoutePoint(Position position, bool isLoading)
            {
                Position = position;
                IsLoading = isLoading;
                IsUnloading = !isLoading;
            }

            public Position Position { get; }
            public bool IsLoading { get; set; }
            public bool IsUnloading { get; set; }
            public double MetersFromDefaultPort { get; set; }
        }

        private sealed record Calculation(string Status, string? Message, List<DeliveryLeg> Legs, List<RoutePoint> RoutePoints)
        {
            public static Calculation EmptyRoute() => new(CallOffRouteStatus.Empty, null, new List<DeliveryLeg>(), new List<RoutePoint>());

            public static Calculation Failure(string message) => new(CallOffRouteStatus.Failed, message, new List<DeliveryLeg>(), new List<RoutePoint>());
        }
    }
}
