using System.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Services
{
    public sealed record TransportOrderRouteStop(
        int Id, int SortOrder, int? DeliveryFromStockId, int? DeliveryToCustomerId,
        int? DeliveryToStockId, int? SupplierOrderId, int? ForcedOmlastStatus, int? OmlastInventoryId);

    public interface ITransportOrderRouteService
    {
        Task<CallOffRouteResult> PreviewTransportOrderAsync(IReadOnlyList<TransportOrderRouteStop> stops, CancellationToken cancellationToken);
        Task<CallOffRouteResult> RecalculateTransportOrderAsync(int transportOrderId, CancellationToken cancellationToken);
    }

    public sealed partial class CallOffRouteService
    {
        public async Task<CallOffRouteResult> PreviewTransportOrderAsync(IReadOnlyList<TransportOrderRouteStop> stops, CancellationToken cancellationToken)
            => ToResult(await CalculateTransportOrderAsync(stops, cancellationToken));

        public async Task<CallOffRouteResult> RecalculateTransportOrderAsync(int transportOrderId, CancellationToken cancellationToken)
        {
            var input = await LoadTransportOrderStopsAsync(transportOrderId, cancellationToken);
            var calculation = await CalculateTransportOrderAsync(input, cancellationToken);
            if (calculation.Status == CallOffRouteStatus.Failed) return ToResult(calculation);

            var transaction = _dbContext.Database.IsRelational()
                ? await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : null;
            try
            {
                var current = await LoadTransportOrderStopsAsync(transportOrderId, cancellationToken);
                if (!input.SequenceEqual(current) || !await _dbContext.TransportOrders.AnyAsync(x => x.Id == transportOrderId, cancellationToken))
                {
                    if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
                    return new CallOffRouteResult(CallOffRouteStatus.Stale,
                        "Transportordern ändrades medan transportbenen beräknades. Försök igen.",
                        GroupLegs(Array.Empty<TransportOrderDeliveryLegDto>()));
                }

                var oldLegs = await _dbContext.DeliveryLegs.Where(x => x.TransportOrderId == transportOrderId).ToListAsync(cancellationToken);
                _dbContext.DeliveryLegs.RemoveRange(oldLegs);
                foreach (var leg in calculation.Legs)
                {
                    leg.TransportOrderId = transportOrderId;
                    _dbContext.DeliveryLegs.Add(leg);
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning("Saving delivery legs for transport order {TransportOrderId} failed ({ErrorType}).", transportOrderId, ex.GetType().Name);
                _dbContext.ChangeTracker.Clear();
                return new CallOffRouteResult(CallOffRouteStatus.Failed, "Transportbenen kunde inte sparas. Försök igen.",
                    GroupLegs(Array.Empty<TransportOrderDeliveryLegDto>()));
            }
            finally
            {
                if (transaction is not null) await transaction.DisposeAsync();
            }

            return ToResult(calculation);
        }

        private async Task<List<TransportOrderRouteStop>> LoadTransportOrderStopsAsync(int id, CancellationToken cancellationToken)
        {
            var rows = await _dbContext.TransportOrderDeliveries.AsNoTracking()
                .Where(x => x.TransportOrderId == id)
                .OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
                .Select(x => new TransportOrderRouteStop(x.Id, x.SortOrder ?? 0, x.DeliveryFromStockId,
                    x.DeliveryToCustomerId, x.DeliveryToStockId, x.SupplierOrderId, x.ForcedOmlastStatus, x.OmlastInventoryId))
                .ToListAsync(cancellationToken);
            return rows;
        }

        private async Task<Calculation> CalculateTransportOrderAsync(IReadOnlyList<TransportOrderRouteStop> stops, CancellationToken cancellationToken)
        {
            if (stops.Count == 0) return Calculation.EmptyRoute();
            try
            {
                var apiKey = (await _dbContext.CompanyInfos.AsNoTracking().Select(x => x.GoogleApiKey).FirstOrDefaultAsync(cancellationToken))?.Trim();
                if (string.IsNullOrWhiteSpace(apiKey)) return Calculation.Failure("Google API-nyckel saknas i Företagsinfo.");

                var cache = new Dictionary<string, Position>(StringComparer.Ordinal);
                var resolved = new List<ResolvedStop>();
                foreach (var stop in stops.OrderBy(x => x.SortOrder).ThenBy(x => x.Id))
                {
                    var fromStock = stop.DeliveryFromStockId.HasValue
                        ? await _dbContext.DeliveryFromStocks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == stop.DeliveryFromStockId, cancellationToken) : null;
                    var toCustomer = stop.DeliveryToCustomerId.HasValue
                        ? await _dbContext.DeliveryToCustomers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == stop.DeliveryToCustomerId, cancellationToken) : null;
                    var toStock = stop.DeliveryToStockId.HasValue
                        ? await _dbContext.DeliveryToStocks.AsNoTracking().FirstOrDefaultAsync(x => x.Id == stop.DeliveryToStockId, cancellationToken) : null;
                    if (fromStock?.IsSlattPallet == true || toCustomer?.IsSlattPallet == true || toStock?.IsSlattPallet == true) continue;
                    if ((stop.DeliveryFromStockId.HasValue && fromStock is null)
                        || (stop.DeliveryToCustomerId.HasValue && toCustomer is null)
                        || (stop.DeliveryToStockId.HasValue && toStock is null)
                        || new[] { stop.DeliveryFromStockId, stop.DeliveryToCustomerId, stop.DeliveryToStockId }.Count(x => x.HasValue) != 1)
                        return Calculation.Failure("En leverans på transportordern är ogiltig eller saknas.");

                    var supplierOrderId = toCustomer?.SupplierOrderId ?? toStock?.SupplierOrderId ?? stop.SupplierOrderId;
                    var supplier = supplierOrderId.HasValue
                        ? await _dbContext.SupplierOrders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == supplierOrderId, cancellationToken) : null;
                    var factory = supplier?.SupplierFactoryId is int factoryId
                        ? await _dbContext.SupplierFactories.FirstOrDefaultAsync(x => x.Id == factoryId, cancellationToken) : null;

                    Position? from;
                    Position? via = null;
                    string? error;
                    if (fromStock is not null)
                    {
                        (from, error) = await ResolveTransportInventoryAsync(fromStock.InventoryId, cache, apiKey, cancellationToken);
                    }
                    else
                    {
                        if (factory is null) return Calculation.Failure("Leveransen saknar en leverantörsfabrik.");
                        (from, error) = await ResolvePositionAsync(factory.Name, factory.Address, factory.PostalNr, factory.City,
                            factory.Country, factory.CountryCode, $"Fabriken {factory.Name}", cache, apiKey, cancellationToken);
                        if (from is not null) factory.PositionId ??= from.Id;
                        if (error is null && factory.ViaInventoryId.HasValue)
                        {
                            (via, error) = await ResolveTransportInventoryAsync(factory.ViaInventoryId, cache, apiKey, cancellationToken);
                        }
                    }
                    if (error is not null) return Calculation.Failure(error);

                    Position? to;
                    Position? distributionTo = null;
                    if (toStock is not null && stop.ForcedOmlastStatus is null or 0)
                    {
                        (to, error) = await ResolveTransportInventoryAsync(toStock.InventoryId ?? supplier?.InventoryId, cache, apiKey, cancellationToken);
                    }
                    else
                    {
                        var customerOrderId = toCustomer?.CustomerOrderId ?? fromStock?.CustomerOrderId;
                        var addressId = customerOrderId.HasValue
                            ? await _dbContext.CustomerOrders.AsNoTracking().Where(x => x.Id == customerOrderId)
                                .Select(x => x.CustomerDeliveryAddressId).FirstOrDefaultAsync(cancellationToken) : supplier?.CustomerDeliveryAddressId;
                        var address = addressId.HasValue
                            ? await _dbContext.CustomerDeliveryAddresses.FirstOrDefaultAsync(x => x.Id == addressId, cancellationToken) : null;
                        if (address is null) return Calculation.Failure("Leveransadress saknas för en leverans på transportordern.");
                        var forced = stop.ForcedOmlastStatus ?? 0;
                        if (fromStock is not null && forced == 0) forced = 2;
                        if (forced is < 0 or > 2) return Calculation.Failure("Ogiltig omlastningsinställning på transportordern.");

                        if (fromStock is null && forced == 2 && address.NextTransportDeliveryAddressId.HasValue)
                        {
                            address = await _dbContext.CustomerDeliveryAddresses
                                .FirstOrDefaultAsync(x => x.Id == address.NextTransportDeliveryAddressId, cancellationToken);
                            if (address is null) return Calculation.Failure("Den slutliga leveransadressen saknas.");
                        }

                        CustomerDeliveryAddress? viaAddress = null;
                        if (forced == 0)
                            viaAddress = await _dbContext.CustomerDeliveryAddresses
                                .Where(x => x.NextTransportDeliveryAddressId == address.Id)
                                .OrderBy(x => x.Id).FirstOrDefaultAsync(cancellationToken);
                        if (forced == 1)
                            (to, error) = await ResolveTransportInventoryAsync(stop.OmlastInventoryId, cache, apiKey, cancellationToken);
                        else
                            (to, error) = await ResolveTransportAddressAsync(viaAddress ?? address, cache, apiKey, cancellationToken);
                        if (error is null && (forced == 1 || viaAddress is not null))
                            (distributionTo, error) = await ResolveTransportAddressAsync(address, cache, apiKey, cancellationToken);
                    }
                    if (error is not null) return Calculation.Failure(error);
                    if (from is null || to is null) return Calculation.Failure("Lastnings- eller lossningsplats saknas.");
                    resolved.Add(new ResolvedStop(new CallOffRouteStop(stop.DeliveryFromStockId), from, to,
                        stop.DeliveryToCustomerId, stop.DeliveryToStockId, via, distributionTo));
                }

                if (resolved.Count == 0) return Calculation.EmptyRoute();
                await _dbContext.SaveChangesAsync(cancellationToken);
                return await CalculateRouteAsync(resolved, apiKey, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                var googleError = ex as GoogleRoutingException;
                _logger.LogWarning("Route calculation for transport order failed ({ErrorType}, status {StatusCode}, Google: {GoogleMessage}).",
                    ex.GetType().Name, googleError?.StatusCode, googleError?.GoogleMessage);
                return Calculation.Failure(string.IsNullOrWhiteSpace(googleError?.GoogleMessage)
                    ? "Transportbenen kunde inte beräknas eftersom Google inte svarade korrekt. Försök igen."
                    : $"Transportbenen kunde inte beräknas. Google svarade: {googleError.GoogleMessage}");
            }
        }

        private async Task<(Position? Position, string? Error)> ResolveTransportInventoryAsync(
            int? id, Dictionary<string, Position> cache, string apiKey, CancellationToken cancellationToken)
        {
            var inventory = id.HasValue ? await _dbContext.Inventories.FirstOrDefaultAsync(x => x.Id == id, cancellationToken) : null;
            if (inventory is null) return (null, "Lager saknas för en leverans på transportordern.");
            var postal = !string.IsNullOrWhiteSpace(inventory.PostalNrText) ? inventory.PostalNrText : GoogleRoutingClient.Invariant(inventory.PostalNr);
            var result = await ResolvePositionAsync(inventory.Name, inventory.Address, postal, inventory.PostalAddress,
                inventory.Country, inventory.CountryCode, $"Lagret {inventory.Name}", cache, apiKey, cancellationToken);
            if (result.Position is not null) inventory.PositionId ??= result.Position.Id;
            return result;
        }

        private async Task<(Position? Position, string? Error)> ResolveTransportAddressAsync(
            CustomerDeliveryAddress address, Dictionary<string, Position> cache, string apiKey, CancellationToken cancellationToken)
        {
            var postal = address.PostalNrValue.HasValue ? GoogleRoutingClient.Invariant(address.PostalNrValue) : address.PostalNr;
            var result = await ResolvePositionAsync(address.Name, address.Address, postal, address.PostalAddress,
                address.Country, address.CountryCode, $"Leveransadressen {address.Name}", cache, apiKey, cancellationToken);
            if (result.Position is not null) address.PositionId ??= result.Position.Id;
            return result;
        }
    }
}
