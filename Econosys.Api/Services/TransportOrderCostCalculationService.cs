using Econosys.Api.Common;
using Econosys.Api.Data;
using Econosys.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Services
{
    // Ports legacy EconosysService.CalcTransportOrderCosts (called by TransportOrderCostCalcViewModel.CreateFromData)
    // and the version-management logic from CreateFromData itself.
    //
    // Supports automatic and forced price lists, including legacy LTL and archived auto-freight calculations.
    public class TransportOrderCostCalculationService : ITransportOrderCostCalculationService
    {
        private readonly ApplicationDbContext _dbContext;

        public TransportOrderCostCalculationService(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        private sealed class ResolvedDelivery
        {
            public double? NrOfItemsRaw { get; init; }
            public int? NrOfPalletsRaw { get; init; }
            public bool PalletIsStackable { get; init; }
            public decimal? PalletCalcFactor { get; init; }
            public int? SupplierFactoryId { get; init; }
            public int? CustomerDeliveryAddressId { get; init; }
            public CalculationRow? CalculationRow { get; init; }
            public short? UnitMultiplicator { get; init; }
            public bool IsSlattPallet { get; init; }
        }

        private sealed class SupplierOrderGroup
        {
            public required int? SupplierOrderId { get; init; }
            public required List<TransportOrderDelivery> Deliveries { get; init; }

            public decimal NrOfItems { get; set; }
            public int NrOfPallets { get; set; }
            public int NrOfPalletPlaces { get; set; }
            public decimal PalletFactor { get; set; }
            public decimal UnitMultiplicator { get; set; }
            public int? SupplierFactoryId { get; set; }
            public bool IsOmlast { get; set; }
            public int? OmlastInventoryId { get; set; }
            public int? DeliveryAddressId { get; set; }
            public string CountryCode { get; set; } = string.Empty;
            public int? PostalNrTo { get; set; }
            public string? StreetTo { get; set; }

            public int CalcRowNrOfPalletPlaces { get; set; }
            public int CalcRowNrOfVehicles { get; set; }
            public decimal CalcRowPalletFactor { get; set; }
            public decimal CalcRowInternationalFreight { get; set; }
            public decimal CalcRowDomesticFreight { get; set; }
            public decimal CalcRowUnloading { get; set; }
            public decimal CalcRowFreight { get; set; }
            public decimal CalcRowFtl { get; set; }
            public decimal CalcRowNrOfItems { get; set; }
            public bool CalcRowUseAdjustedPalletPlaces { get; set; }
            public decimal CalcRowSalesCurrencyRate { get; set; } = 1;
            public decimal CalcRowPreCalcAdditionPercent { get; set; }
            public bool HasError { get; set; }
            public string ErrInfo { get; set; } = string.Empty;
            public string CalculationCalcInfo { get; set; } = string.Empty;

            public decimal? CaclulationInternationalCost { get; set; }
            public decimal? CaclulationDomesticCost { get; set; }
            public decimal? CaclulationFtl { get; set; }
            public decimal? CaclulationUnloading { get; set; }
            public decimal CaclulationPreCalcAdditionPercent { get; set; }
            public decimal CaclulationTotal { get; set; }
            public decimal CaclulationUsedTotal { get; set; }
            public decimal TotalPalletArea { get; set; }

            public decimal ToInternationalCost { get; set; }
            public decimal TransportOrderDomesticCost { get; set; }
            public decimal TransportOrderLoading { get; set; }
            public decimal TransportOrderUnloading { get; set; }
            public decimal TransportOrderTotal { get; set; }
            public decimal DiffTransportOrderCaclulation { get; set; }
            public string TransportOrderCalcInfo { get; set; } = string.Empty;
        }

        public async Task<TransportOrderCostCalculationResult> RecalculateAsync(int transportOrderId, int deliveryStatus)
        {
            var deliveries = await _dbContext.TransportOrderDeliveries
                .Where(x => x.TransportOrderId == transportOrderId)
                .OrderBy(x => x.SortOrder)
                .ToListAsync();

            if (deliveries.Count == 0)
            {
                return TransportOrderCostCalculationResult.Fail("Transportordern saknar leveranser att beräkna kalkyl på.");
            }

            var latestVersion = await _dbContext.TransportOrderCostCalcs
                .AsNoTracking()
                .Where(x => x.TransportOrderId == transportOrderId)
                .OrderByDescending(x => x.CreatedDateTime)
                .FirstOrDefaultAsync();

            var isManualCalc = latestVersion?.IsManualCalc ?? false;
            var isLtl = latestVersion?.IsLTL ?? false;
            var forcePriceListId = latestVersion?.CostCalcForcePriceListId;

            var groups = await BuildSupplierOrderGroupsAsync(deliveries);
            if (groups is null)
            {
                return TransportOrderCostCalculationResult.Fail("Automatisk kalkylberäkning stöds ännu inte för leveranser med arkiverad fordonsbaserad fraktberäkning. Använd manuell inmatning.");
            }

            var totalPalletPlaceArea = groups.Sum(x => x.NrOfPalletPlaces * x.PalletFactor);
            if (totalPalletPlaceArea == 0)
            {
                return TransportOrderCostCalculationResult.Fail("Kunde inte beräkna kalkyl: total pallplatsarea är noll.");
            }

            foreach (var group in groups)
            {
                group.TotalPalletArea = group.NrOfPalletPlaces * group.PalletFactor;
                CalculateCalculationRowUsedCosts(group);
            }

            string? calcCityFrom = null;
            if (!isManualCalc)
            {
                var factoryIds = groups.Where(x => x.SupplierFactoryId.HasValue).Select(x => x.SupplierFactoryId!.Value).Distinct().ToList();
                var firstDelivery = groups.SelectMany(x => x.Deliveries).OrderBy(x => x.SortOrder).FirstOrDefault();
                var postalNrTo = groups.FirstOrDefault(x => x.Deliveries.Contains(firstDelivery!))?.PostalNrTo ?? 0;

                var (tcplMax, cityFrom) = await ResolvePriceListAsync(forcePriceListId, factoryIds, postalNrTo);
                if (tcplMax is null)
                {
                    return TransportOrderCostCalculationResult.Fail("Kunde inte hitta en giltig fraktprislista för transportordern.");
                }

                calcCityFrom = cityFrom;

                var tcplMaxDataRow = tcplMax.TransportCostPriceListData.FirstOrDefault(x => x.PostalNrFrom <= postalNrTo && x.PostalNrTo >= postalNrTo);
                if (tcplMaxDataRow is null)
                {
                    return TransportOrderCostCalculationResult.Fail("Prislistan saknar prisdata för postnummerintervallet.");
                }

                var currencyRate = (decimal)(tcplMax.Currency?.RateToSek ?? 1);
                decimal freightPrice;
                decimal dmtAddition;
                decimal currencyAddition;
                decimal otherAddition;
                decimal seca;
                decimal other;
                decimal totalAddition;

                if (isLtl)
                {
                    var totalAreaDecimalPart = totalPalletPlaceArea - Math.Floor(totalPalletPlaceArea);
                    var palletPlaceColumn = totalAreaDecimalPart <= 0.25M
                        ? (int)Math.Floor(totalPalletPlaceArea)
                        : (int)Math.Ceiling(totalPalletPlaceArea);
                    freightPrice = palletPlaceColumn > 0
                        ? GetPalletPlacePrice(tcplMaxDataRow, Math.Min(palletPlaceColumn, 32)) * palletPlaceColumn
                        : 0;
                    dmtAddition = tcplMax.DmtPercentPallet.GetValueOrDefault();
                    currencyAddition = tcplMax.AdditionCurrencyPercentPallet.GetValueOrDefault();
                    otherAddition = tcplMax.OtherPercentPallet.GetValueOrDefault();
                    seca = tcplMax.SecaMarpolPallet.GetValueOrDefault();
                    other = tcplMax.OtherPallet.GetValueOrDefault();
                    totalAddition = tcplMax.AdditionTotalPercentPallet.GetValueOrDefault();
                }
                else
                {
                    freightPrice = tcplMaxDataRow.PFTL.GetValueOrDefault();
                    dmtAddition = tcplMax.DmtPercentFtl.GetValueOrDefault();
                    currencyAddition = tcplMax.AdditionCurrencyPercentFtl.GetValueOrDefault();
                    otherAddition = tcplMax.OtherPercentFtl.GetValueOrDefault();
                    seca = tcplMax.SecaMarpolFtl.GetValueOrDefault();
                    other = tcplMax.OtherFtl.GetValueOrDefault();
                    totalAddition = tcplMax.AdditionTotalPercentFtl.GetValueOrDefault();
                }

                foreach (var group in groups)
                {
                    group.ToInternationalCost = (freightPrice * (1 + dmtAddition + currencyAddition + otherAddition)
                        + seca * (isLtl ? group.TotalPalletArea : 1)
                        + other * (isLtl ? group.TotalPalletArea : 1))
                        * (1 + totalAddition)
                        * currencyRate
                        * group.NrOfPalletPlaces * group.PalletFactor / totalPalletPlaceArea;
                }

                foreach (var group in groups)
                {
                    await CalculateDomesticCostAsync(group);
                }

                ApplyLoadingAndUnloading(groups, tcplMax);

                foreach (var group in groups)
                {
                    group.TransportOrderTotal = group.ToInternationalCost + group.TransportOrderDomesticCost + group.TransportOrderLoading + group.TransportOrderUnloading;
                    group.DiffTransportOrderCaclulation = group.CaclulationUsedTotal - group.TransportOrderTotal;
                }
            }

            var costCalc = await ApplyVersionAsync(transportOrderId, deliveryStatus, groups, calcCityFrom, isManualCalc, isLtl, forcePriceListId);
            return TransportOrderCostCalculationResult.Ok(costCalc);
        }

        private async Task<List<SupplierOrderGroup>?> BuildSupplierOrderGroupsAsync(List<TransportOrderDelivery> deliveries)
        {
            var groups = new List<SupplierOrderGroup>();

            foreach (var groupDeliveries in deliveries.GroupBy(x => x.SupplierOrderId))
            {
                var group = new SupplierOrderGroup
                {
                    SupplierOrderId = groupDeliveries.Key,
                    Deliveries = groupDeliveries.ToList(),
                };

                foreach (var delivery in group.Deliveries)
                {
                    var resolved = await ResolveDeliveryAsync(delivery);
                    if (resolved is null)
                    {
                        continue;
                    }

                    group.NrOfItems += (decimal)(resolved.NrOfItemsRaw ?? 0);
                    group.NrOfPallets += resolved.NrOfPalletsRaw ?? 0;
                    group.NrOfPalletPlaces = (int)Math.Ceiling(group.NrOfPallets / (decimal)(resolved.PalletIsStackable ? 2 : 1));
                    group.PalletFactor = group.PalletFactor != 0 ? group.PalletFactor : resolved.PalletCalcFactor.GetValueOrDefault(1);
                    group.UnitMultiplicator = group.UnitMultiplicator != 0 ? group.UnitMultiplicator : resolved.UnitMultiplicator.GetValueOrDefault(1);
                    group.SupplierFactoryId ??= resolved.SupplierFactoryId;

                    var calcRow = resolved.CalculationRow;
                    if (calcRow?.ArchivedAutoFreightCalc == true)
                    {
                        var palletsPerTruck = calcRow.ArchivedPalletsPerTruck.GetValueOrDefault();
                        if (palletsPerTruck == 0)
                        {
                            group.HasError = true;
                            group.ErrInfo = "Saknar arkiverad ArchivedPalletsPerTruck på kalkylrad";
                            group.CalcRowNrOfPalletPlaces = 0;
                            group.CalcRowNrOfVehicles = 0;
                        }
                        else
                        {
                            var nrOfPallets = (int)Math.Ceiling(calcRow.NrOfPalletsPopup.GetValueOrDefault());
                            var nrOfOverflowPallets = nrOfPallets % palletsPerTruck;
                            group.CalcRowNrOfVehicles = (nrOfPallets - nrOfOverflowPallets) / palletsPerTruck;
                            group.CalcRowNrOfPalletPlaces = (int)Math.Ceiling(
                                nrOfOverflowPallets / (decimal)(calcRow.ArchivedPalletIsStackable ? 2 : 1));
                            if (!calcRow.CalcPalletPriceUsed)
                            {
                                group.CalcRowNrOfPalletPlaces = 0;
                            }
                        }
                    }
                    else
                    {
                        group.CalcRowNrOfPalletPlaces = (int)Math.Ceiling((calcRow?.NrOfPalletsPopup.GetValueOrDefault() ?? 0) / (calcRow?.ArchivedPalletIsStackable == true ? 2 : 1));
                        group.CalcRowNrOfVehicles = (int)(calcRow?.NrOfVehiclesPopup.GetValueOrDefault() ?? 0);
                    }
                    group.CalcRowPalletFactor = calcRow?.ArchivedPalletCalcFactor.GetValueOrDefault() ?? 0;
                    group.CalcRowInternationalFreight = (decimal)(calcRow?.FreightCostPerPalletInternationalPopup.GetValueOrDefault() ?? 0);
                    group.CalcRowDomesticFreight = (decimal)(calcRow?.FreightCostPerPalletDomesticPopup.GetValueOrDefault() ?? 0);
                    group.CalcRowUnloading = (decimal)(calcRow?.FreightUnloadingPopup.GetValueOrDefault() ?? 0);
                    group.CalcRowFreight = (decimal)(calcRow?.Freight.GetValueOrDefault() ?? 0);
                    group.CalcRowFtl = calcRow?.FreightCostPerFtlInternationalPopup.GetValueOrDefault() ?? 0;
                    group.CalcRowNrOfItems = calcRow?.Edition.GetValueOrDefault() ?? 0;
                    group.CalcRowUseAdjustedPalletPlaces = calcRow?.ArchivedPriceListUseAdjustedPalletPlaces ?? false;
                    group.CalcRowSalesCurrencyRate = calcRow?.ArchivedSalesCurrencyRate.GetValueOrDefault(1) ?? 1;
                    group.CalcRowPreCalcAdditionPercent = calcRow?.PreCalcAdditionPercent.GetValueOrDefault(0) ?? 0;

                    group.DeliveryAddressId = resolved.CustomerDeliveryAddressId;
                    group.CountryCode = string.Empty;
                    group.PostalNrTo = null;
                    group.StreetTo = null;

                    if (resolved.CustomerDeliveryAddressId.HasValue)
                    {
                        var address = await _dbContext.CustomerDeliveryAddresses
                            .AsNoTracking()
                            .FirstOrDefaultAsync(x => x.Id == resolved.CustomerDeliveryAddressId.Value);
                        if (address is not null)
                        {
                            group.CountryCode = address.CountryCode ?? string.Empty;
                            group.PostalNrTo = address.PostalNrValue;
                            group.StreetTo = address.Address;
                        }
                    }

                    var updateOmlast = group.Deliveries.Count == 1 || !resolved.IsSlattPallet;
                    if (updateOmlast)
                    {
                        if (delivery.ForcedOmlastStatus == 1 && delivery.OmlastInventoryId.HasValue)
                        {
                            group.IsOmlast = true;
                            group.OmlastInventoryId = delivery.OmlastInventoryId;
                        }
                        else
                        {
                            group.IsOmlast = false;
                            group.OmlastInventoryId = null;
                            if (resolved.CustomerDeliveryAddressId.HasValue)
                            {
                                var omlastAddress = await _dbContext.CustomerDeliveryAddresses
                                    .AsNoTracking()
                                    .Where(x => x.NextTransportDeliveryAddressId == resolved.CustomerDeliveryAddressId.Value)
                                    .FirstOrDefaultAsync();
                                if (omlastAddress is not null)
                                {
                                    group.IsOmlast = true;
                                    group.OmlastInventoryId = omlastAddress.InventoryId;
                                }
                            }
                        }
                    }
                }

                groups.Add(group);
            }

            return groups;
        }

        private async Task<ResolvedDelivery?> ResolveDeliveryAsync(TransportOrderDelivery delivery)
        {
            if (delivery.DeliveryFromStockId.HasValue)
            {
                var q = await (from d in _dbContext.DeliveryFromStocks
                                where d.Id == delivery.DeliveryFromStockId
                                select new
                                {
                                    d.NrOfItems,
                                    d.NrOfPallets,
                                    d.PalletIsStackable,
                                    d.PalletCalcFactor,
                                    d.IsSlattPallet,
                                    SupplierFactoryId = d.CustomerOrder != null && d.CustomerOrder.SupplierOrder != null ? d.CustomerOrder.SupplierOrder.SupplierFactoryId : (int?)null,
                                    CustomerDeliveryAddressId = d.CustomerOrder != null ? d.CustomerOrder.CustomerDeliveryAddressId : (int?)null,
                                    CalculationRow = d.CustomerOrder != null ? d.CustomerOrder.SelectedCalculationRow : null,
                                    UnitMultiplicator = d.CustomerOrder != null && d.CustomerOrder.Unit != null ? d.CustomerOrder.Unit.Multiplicator : (short?)1,
                                }).FirstOrDefaultAsync();
                if (q is null)
                {
                    return null;
                }
                return new ResolvedDelivery
                {
                    NrOfItemsRaw = q.NrOfItems,
                    NrOfPalletsRaw = q.NrOfPallets,
                    PalletIsStackable = q.PalletIsStackable,
                    PalletCalcFactor = q.PalletCalcFactor,
                    SupplierFactoryId = q.SupplierFactoryId,
                    CustomerDeliveryAddressId = q.CustomerDeliveryAddressId,
                    CalculationRow = q.CalculationRow,
                    UnitMultiplicator = q.UnitMultiplicator,
                    IsSlattPallet = q.IsSlattPallet,
                };
            }

            if (delivery.DeliveryToCustomerId.HasValue)
            {
                var q = await (from d in _dbContext.DeliveryToCustomers
                                where d.Id == delivery.DeliveryToCustomerId
                                select new
                                {
                                    d.NrOfItems,
                                    d.NrOfPallets,
                                    d.PalletIsStackable,
                                    d.PalletCalcFactor,
                                    d.IsSlattPallet,
                                    SupplierFactoryId = d.CustomerOrder != null && d.CustomerOrder.SupplierOrder != null ? d.CustomerOrder.SupplierOrder.SupplierFactoryId : (int?)null,
                                    CustomerDeliveryAddressId = d.CustomerOrder != null ? d.CustomerOrder.CustomerDeliveryAddressId : (int?)null,
                                    CalculationRow = d.CustomerOrder != null ? d.CustomerOrder.SelectedCalculationRow : null,
                                    UnitMultiplicator = d.CustomerOrder != null && d.CustomerOrder.Unit != null ? d.CustomerOrder.Unit.Multiplicator : (short?)1,
                                }).FirstOrDefaultAsync();
                if (q is null)
                {
                    return null;
                }
                return new ResolvedDelivery
                {
                    NrOfItemsRaw = q.NrOfItems,
                    NrOfPalletsRaw = q.NrOfPallets,
                    PalletIsStackable = q.PalletIsStackable,
                    PalletCalcFactor = q.PalletCalcFactor,
                    SupplierFactoryId = q.SupplierFactoryId,
                    CustomerDeliveryAddressId = q.CustomerDeliveryAddressId,
                    CalculationRow = q.CalculationRow,
                    UnitMultiplicator = q.UnitMultiplicator,
                    IsSlattPallet = q.IsSlattPallet,
                };
            }

            if (delivery.DeliveryToStockId.HasValue)
            {
                var q = await (from d in _dbContext.DeliveryToStocks
                                where d.Id == delivery.DeliveryToStockId
                                select new
                                {
                                    d.NrOfItems,
                                    d.NrOfPallets,
                                    d.PalletIsStackable,
                                    d.PalletCalcFactor,
                                    d.IsSlattPallet,
                                    SupplierFactoryId = d.SupplierOrder != null ? d.SupplierOrder.SupplierFactoryId : (int?)null,
                                    CustomerDeliveryAddressId = d.SupplierOrder != null ? d.SupplierOrder.CustomerDeliveryAddressId : (int?)null,
                                    CalculationRow = d.SupplierOrder != null ? d.SupplierOrder.SelectedCalculationRow : null,
                                    UnitMultiplicator = d.SupplierOrder != null && d.SupplierOrder.Unit != null ? d.SupplierOrder.Unit.Multiplicator : (short?)1,
                                }).FirstOrDefaultAsync();
                if (q is null)
                {
                    return null;
                }
                return new ResolvedDelivery
                {
                    NrOfItemsRaw = q.NrOfItems,
                    NrOfPalletsRaw = q.NrOfPallets,
                    PalletIsStackable = q.PalletIsStackable,
                    PalletCalcFactor = q.PalletCalcFactor,
                    SupplierFactoryId = q.SupplierFactoryId,
                    CustomerDeliveryAddressId = q.CustomerDeliveryAddressId,
                    CalculationRow = q.CalculationRow,
                    UnitMultiplicator = q.UnitMultiplicator,
                    IsSlattPallet = q.IsSlattPallet,
                };
            }

            return null;
        }

        // "Calculation-used" costs are the order's own original estimate, independent of price lists — always computed, even for manual calc.
        private static void CalculateCalculationRowUsedCosts(SupplierOrderGroup group)
        {
            var unitMultiplicator = group.UnitMultiplicator == 0 ? 1 : group.UnitMultiplicator;
            var calcRowNrOfItems = group.CalcRowNrOfItems == 0 ? 1 : group.CalcRowNrOfItems;

            group.CaclulationInternationalCost = group.CalcRowNrOfPalletPlaces == 0
                ? null
                : Math.Round(group.CalcRowInternationalFreight * group.CalcRowNrOfPalletPlaces * group.CalcRowPalletFactor) / calcRowNrOfItems * group.NrOfItems;
            group.CaclulationDomesticCost = group.CalcRowNrOfPalletPlaces == 0
                ? null
                : Math.Round(group.CalcRowDomesticFreight * group.CalcRowNrOfPalletPlaces * group.CalcRowPalletFactor) / calcRowNrOfItems * group.NrOfItems;
            group.CaclulationFtl = group.CalcRowNrOfVehicles == 0
                ? null
                : Math.Round(group.CalcRowFtl) / calcRowNrOfItems * group.NrOfItems;
            group.CaclulationUnloading = group.CalcRowNrOfPalletPlaces == 0
                ? null
                : Math.Round(group.CalcRowUnloading) / calcRowNrOfItems * group.NrOfItems;
            group.CaclulationPreCalcAdditionPercent = group.CalcRowPreCalcAdditionPercent;

            group.CaclulationTotal = Math.Round(
                Math.Round(
                    (group.CalcRowInternationalFreight + group.CalcRowDomesticFreight) * group.CalcRowNrOfPalletPlaces * group.CalcRowPalletFactor
                    + (group.CalcRowNrOfVehicles == 0 ? 0 : group.CalcRowFtl)
                    + (group.CalcRowNrOfPalletPlaces == 0 ? 0 : group.CalcRowUnloading))
                / (calcRowNrOfItems / unitMultiplicator)
                * group.NrOfItems / unitMultiplicator
                * (1 + group.CalcRowPreCalcAdditionPercent / 100));
            group.CaclulationUsedTotal = group.CalcRowFreight * group.CalcRowSalesCurrencyRate * group.NrOfItems / unitMultiplicator;
        }

        private async Task<(TransportCostPriceList? PriceList, string? CityFrom)> ResolvePriceListAsync(int? forcePriceListId, List<int> factoryIds, int postalNrTo)
        {
            if (forcePriceListId.HasValue && forcePriceListId.Value != 0)
            {
                var forced = await _dbContext.TransportCostPriceLists
                    .AsNoTracking()
                    .Include(x => x.TransportCostPriceListData)
                    .Include(x => x.Currency)
                    .FirstOrDefaultAsync(x => x.Id == forcePriceListId.Value);
                return (forced, "TVINGAD PRISLISTA");
            }

            var candidates = await _dbContext.SupplierFactoryTransportCostPriceLists
                .AsNoTracking()
                .Where(x => x.SupplierFactoryId.HasValue && factoryIds.Contains(x.SupplierFactoryId.Value))
                .Select(x => x.TransportCostPriceList!)
                .Distinct()
                .Include(x => x.TransportCostPriceListData)
                .Include(x => x.Currency)
                .Include(x => x.SupplierFactoryTransportCostPriceLists)
                .ToListAsync();

            var tcplMax = candidates
                .Select(x => new
                {
                    PriceList = x,
                    Ftl = x.TransportCostPriceListData.FirstOrDefault(d => d.PostalNrFrom <= postalNrTo && d.PostalNrTo >= postalNrTo)?.PFTL,
                })
                .OrderBy(x => x.Ftl ?? decimal.MinValue)
                .LastOrDefault();

            if (tcplMax?.PriceList is null)
            {
                return (null, null);
            }

            var maxFactoryLink = tcplMax.PriceList.SupplierFactoryTransportCostPriceLists
                .FirstOrDefault(x => x.SupplierFactoryId.HasValue && factoryIds.Contains(x.SupplierFactoryId.Value));
            string? cityFrom = null;
            if (maxFactoryLink?.SupplierFactoryId is not null)
            {
                var factory = await _dbContext.SupplierFactories.AsNoTracking().FirstOrDefaultAsync(x => x.Id == maxFactoryLink.SupplierFactoryId.Value);
                cityFrom = factory?.City;
            }

            return (tcplMax.PriceList, cityFrom);
        }

        private async Task CalculateDomesticCostAsync(SupplierOrderGroup group)
        {
            if (!group.IsOmlast)
            {
                group.TransportOrderDomesticCost = 0;
                return;
            }

            var candidates = await _dbContext.TransportCostPriceLists
                .AsNoTracking()
                .Include(x => x.TransportCostPriceListData)
                .Include(x => x.Currency)
                .Where(x => x.InventoryTransportCostPriceLists.Any(p => p.InventoryId == group.OmlastInventoryId)
                    && x.TransportCostPriceListData.Any(p => p.CountryCode == group.CountryCode)
                    && x.TransportCostPriceListData.Any(p => p.PostalNrFrom <= group.PostalNrTo)
                    && x.TransportCostPriceListData.Any(p => p.PostalNrTo >= group.PostalNrTo))
                .ToListAsync();

            if (candidates.Count != 1)
            {
                group.TransportOrderDomesticCost = -1;
                group.TransportOrderCalcInfo += "Kopplad omlastningslagerprislista kan ej laddas.";
                return;
            }

            var tcpl = candidates[0];
            var tcplData = tcpl.TransportCostPriceListData.FirstOrDefault(p => p.PostalNrFrom <= group.PostalNrTo && p.PostalNrTo >= group.PostalNrTo);
            if (tcplData is null)
            {
                group.TransportOrderDomesticCost = -1;
                return;
            }

            var tcplCurrencyRate = (decimal)(tcpl.Currency?.RateToSek ?? 1);
            var firstDataRow = tcpl.TransportCostPriceListData.FirstOrDefault();
            var additionSekPerPallet = firstDataRow?.AdditionSekPerPallet.GetValueOrDefault() ?? 0;

            decimal domesticPrice;
            if (tcpl.IsStafflad)
            {
                var nrOfPalletPlacesColumn = group.NrOfPalletPlaces * group.PalletFactor;
                nrOfPalletPlacesColumn = nrOfPalletPlacesColumn - Math.Truncate(nrOfPalletPlacesColumn) <= 0.25M
                    ? Math.Truncate(nrOfPalletPlacesColumn)
                    : Math.Ceiling(nrOfPalletPlacesColumn);
                var palletPlacePrice = GetPalletPlacePrice(tcplData, (int)Math.Min(nrOfPalletPlacesColumn, 32));
                var basePrice = palletPlacePrice * (1 + tcpl.DmtPercentPallet.GetValueOrDefault() + tcpl.AdditionCurrencyPercentPallet.GetValueOrDefault() + tcpl.OtherPercentPallet.GetValueOrDefault())
                    + tcpl.SecaMarpolPallet.GetValueOrDefault()
                    + tcpl.OtherPallet.GetValueOrDefault();

                if (group.CalcRowUseAdjustedPalletPlaces)
                {
                    domesticPrice = (basePrice + additionSekPerPallet * (1 + tcpl.AdditionTotalPercentPallet.GetValueOrDefault()))
                        * tcplCurrencyRate * group.NrOfPalletPlaces;
                }
                else
                {
                    domesticPrice = basePrice * Math.Ceiling(group.NrOfPalletPlaces * group.PalletFactor)
                        + additionSekPerPallet * Math.Ceiling((decimal)group.NrOfPalletPlaces);
                    domesticPrice *= tcplCurrencyRate;
                }
            }
            else
            {
                var palletPlacePrice = GetPalletPlacePrice(tcplData, Math.Min(group.NrOfPalletPlaces, 32));
                var basePrice = palletPlacePrice * (1 + tcpl.DmtPercentPallet.GetValueOrDefault() + tcpl.AdditionCurrencyPercentPallet.GetValueOrDefault() + tcpl.OtherPercentPallet.GetValueOrDefault())
                    + tcpl.SecaMarpolPallet.GetValueOrDefault()
                    + tcpl.OtherPallet.GetValueOrDefault()
                    + additionSekPerPallet * (1 + tcpl.AdditionTotalPercentPallet.GetValueOrDefault()) * tcplCurrencyRate;
                domesticPrice = basePrice * group.NrOfPalletPlaces;
            }

            group.TransportOrderDomesticCost = domesticPrice;
            group.TransportOrderCalcInfo += $"Kopplad prislista: {tcpl.ImportName}";
        }

        private static void ApplyLoadingAndUnloading(List<SupplierOrderGroup> groups, TransportCostPriceList tcplMax)
        {
            var uniqueStreets = groups.Where(x => x.StreetTo is not null).Select(x => x.StreetTo).Distinct().Count();
            foreach (var group in groups)
            {
                var sharingCount = groups.Count(x => x.StreetTo == group.StreetTo);
                group.TransportOrderUnloading = uniqueStreets == 0
                    ? 0
                    : tcplMax.UnloadingCost.GetValueOrDefault() * (uniqueStreets - 1) / uniqueStreets / sharingCount;
            }

            var uniqueFactories = groups.Where(x => x.SupplierFactoryId.HasValue).Select(x => x.SupplierFactoryId).Distinct().Count();
            foreach (var group in groups)
            {
                var sharingCount = groups.Count(x => x.SupplierFactoryId == group.SupplierFactoryId);
                group.TransportOrderLoading = uniqueFactories == 0
                    ? 0
                    : tcplMax.LoadingCost.GetValueOrDefault() * (uniqueFactories - 1) / uniqueFactories / sharingCount;
            }
        }

        private static decimal GetPalletPlacePrice(TransportCostPriceListData data, int nrOfPalletPlaces) => Math.Max(1, nrOfPalletPlaces) switch
        {
            1 => data.P1.GetValueOrDefault(),
            2 => data.P2.GetValueOrDefault(),
            3 => data.P3.GetValueOrDefault(),
            4 => data.P4.GetValueOrDefault(),
            5 => data.P5.GetValueOrDefault(),
            6 => data.P6.GetValueOrDefault(),
            7 => data.P7.GetValueOrDefault(),
            8 => data.P8.GetValueOrDefault(),
            9 => data.P9.GetValueOrDefault(),
            10 => data.P10.GetValueOrDefault(),
            11 => data.P11.GetValueOrDefault(),
            12 => data.P12.GetValueOrDefault(),
            13 => data.P13.GetValueOrDefault(),
            14 => data.P14.GetValueOrDefault(),
            15 => data.P15.GetValueOrDefault(),
            16 => data.P16.GetValueOrDefault(),
            17 => data.P17.GetValueOrDefault(),
            18 => data.P18.GetValueOrDefault(),
            19 => data.P19.GetValueOrDefault(),
            20 => data.P20.GetValueOrDefault(),
            21 => data.P21.GetValueOrDefault(),
            22 => data.P22.GetValueOrDefault(),
            23 => data.P23.GetValueOrDefault(),
            24 => data.P24.GetValueOrDefault(),
            25 => data.P25.GetValueOrDefault(),
            26 => data.P26.GetValueOrDefault(),
            27 => data.P27.GetValueOrDefault(),
            28 => data.P28.GetValueOrDefault(),
            29 => data.P29.GetValueOrDefault(),
            30 => data.P30.GetValueOrDefault(),
            31 => data.P31.GetValueOrDefault(),
            _ => data.P32.GetValueOrDefault(),
        };

        // Ports TransportOrderCostCalcViewModel.CreateFromData: version reuse/creation decision tree + field carryforward.
        private async Task<TransportOrderCostCalc> ApplyVersionAsync(
            int transportOrderId,
            int deliveryStatus,
            List<SupplierOrderGroup> groups,
            string? calcCityFrom,
            bool isManualCalc,
            bool isLtl,
            int? forcePriceListId)
        {
            var versions = await _dbContext.TransportOrderCostCalcs
                .Include(x => x.TransportOrderCostCalcSupplierOrders)
                    .ThenInclude(x => x.TransportOrderCostCalcSupplierOrderDeliveries)
                .Where(x => x.TransportOrderId == transportOrderId)
                .OrderByDescending(x => x.CreatedDateTime)
                .ToListAsync();

            var current = versions.FirstOrDefault();
            var prevVersion = current;

            TransportOrderCostCalc? target = null;
            List<TransportOrderCostCalcSupplierOrder> orgRows = new();
            var isNewRecord = true;

            if (current is not null && current.CreatedAtStatus != deliveryStatus && deliveryStatus is 1 or 2 or 3)
            {
                if (deliveryStatus == 1 && current.CreatedAtStatus == 2)
                {
                    target = versions.Where(x => x.CreatedAtStatus == 2).OrderByDescending(x => x.CreatedDateTime).FirstOrDefault();
                }
                else if (deliveryStatus == 2 && current.CreatedAtStatus == 1)
                {
                    target = versions.Where(x => x.CreatedAtStatus == 1).OrderByDescending(x => x.CreatedDateTime).FirstOrDefault();
                }
                else if (deliveryStatus == 3 && current.CreatedAtStatus == 2)
                {
                    target = versions.Where(x => x.CreatedAtStatus == 2).OrderByDescending(x => x.CreatedDateTime).FirstOrDefault();
                }

                if (target is not null)
                {
                    orgRows = target.TransportOrderCostCalcSupplierOrders.ToList();
                    target.CreatedAtStatus = deliveryStatus;
                    isNewRecord = false;
                }
            }
            else if (current is not null && current.CreatedAtStatus == deliveryStatus)
            {
                target = versions.Where(x => x.CreatedAtStatus == deliveryStatus).OrderByDescending(x => x.CreatedDateTime).FirstOrDefault();
                if (target is not null)
                {
                    orgRows = target.TransportOrderCostCalcSupplierOrders.ToList();
                    isNewRecord = false;
                }
            }

            if (target is null)
            {
                target = new TransportOrderCostCalc
                {
                    TransportOrderId = transportOrderId,
                    CreatedAtStatus = deliveryStatus,
                    CreatedDateTime = SwedishTime.Now,
                    IsLTL = isLtl,
                    IsManualCalc = isManualCalc,
                    CostCalcForcePriceListId = forcePriceListId,
                };
                _dbContext.TransportOrderCostCalcs.Add(target);
                isNewRecord = true;
            }
            else
            {
                target.IsLTL = isLtl;
                target.IsManualCalc = isManualCalc;
                target.CostCalcForcePriceListId = forcePriceListId;
            }

            target.CalcCityFrom = calcCityFrom;
            target.CalcPostalNrTo = groups.Select(x => x.PostalNrTo).FirstOrDefault(x => x.HasValue) ?? target.CalcPostalNrTo;

            foreach (var supplierOrder in target.TransportOrderCostCalcSupplierOrders.ToList())
            {
                _dbContext.TransportOrderCostCalcSupplierOrderDeliveries.RemoveRange(supplierOrder.TransportOrderCostCalcSupplierOrderDeliveries);
            }
            _dbContext.TransportOrderCostCalcSupplierOrders.RemoveRange(target.TransportOrderCostCalcSupplierOrders);
            target.TransportOrderCostCalcSupplierOrders.Clear();

            var carryForwardSource = isNewRecord
                ? prevVersion?.TransportOrderCostCalcSupplierOrders.ToList() ?? new List<TransportOrderCostCalcSupplierOrder>()
                : orgRows;

            foreach (var group in groups)
            {
                var row = new TransportOrderCostCalcSupplierOrder
                {
                    SupplierOrderId = group.SupplierOrderId,
                    CaclulationInternationalCost = group.CaclulationInternationalCost,
                    CaclulationDomesticCost = group.CaclulationDomesticCost,
                    CaclulationFtl = group.CaclulationFtl,
                    CaclulationUnloading = group.CaclulationUnloading,
                    CaclulationTotal = group.CaclulationTotal,
                    CaclulationUsedTotal = group.CaclulationUsedTotal,
                    CalculationCalcInfo = group.CalculationCalcInfo,
                    ToInternationalCost = group.ToInternationalCost,
                    TransportOrderDomesticCost = group.TransportOrderDomesticCost,
                    TransportOrderLoading = group.TransportOrderLoading,
                    TransportOrderUnloading = group.TransportOrderUnloading,
                    TransportOrderTotal = group.TransportOrderTotal,
                    TransportOrderCalcInfo = group.TransportOrderCalcInfo,
                    DiffTransportOrderCaclulation = group.DiffTransportOrderCaclulation,
                    TotalNrOfItems = group.NrOfItems,
                    TotalNrOfPallets = group.NrOfPallets,
                    TotalNrOfPalletPlaces = group.NrOfPalletPlaces,
                    TotalPalletArea = group.TotalPalletArea,
                    PalletFactor = group.PalletFactor,
                };

                var sourceRow = carryForwardSource.FirstOrDefault(x => x.SupplierOrderId == group.SupplierOrderId);
                if (sourceRow is not null)
                {
                    row.TransportOrderOther = sourceRow.TransportOrderOther;
                    row.ResultInternationalCost = sourceRow.ResultInternationalCost;
                    row.ResultDomesticCost = sourceRow.ResultDomesticCost;
                    row.ResultOther = sourceRow.ResultOther;
                    row.ResultNote = sourceRow.ResultNote;
                    if (isManualCalc)
                    {
                        row.DiffTransportOrderCaclulation = sourceRow.DiffTransportOrderCaclulation;
                        row.ToInternationalCost = sourceRow.ToInternationalCost;
                        row.TransportOrderDomesticCost = sourceRow.TransportOrderDomesticCost;
                        row.TransportOrderLoading = sourceRow.TransportOrderLoading;
                        row.TransportOrderUnloading = sourceRow.TransportOrderUnloading;
                    }
                }

                row.TransportOrderTotal = row.ToInternationalCost.GetValueOrDefault()
                    + row.TransportOrderDomesticCost.GetValueOrDefault()
                    + row.TransportOrderUnloading.GetValueOrDefault()
                    + row.TransportOrderLoading.GetValueOrDefault()
                    + row.TransportOrderOther.GetValueOrDefault();
                row.DiffTransportOrderCaclulation = row.CaclulationUsedTotal.GetValueOrDefault()
                    - row.TransportOrderTotal.GetValueOrDefault();
                row.ResultTotal = row.ResultInternationalCost.GetValueOrDefault()
                    + row.ResultDomesticCost.GetValueOrDefault()
                    + row.ResultOther.GetValueOrDefault();
                row.DiffResultCaclulation = row.CaclulationUsedTotal.GetValueOrDefault()
                    - row.ResultTotal.GetValueOrDefault();
                row.DiffResultTransportOrder = row.TransportOrderTotal.GetValueOrDefault()
                    - row.ResultTotal.GetValueOrDefault();

                foreach (var delivery in group.Deliveries)
                {
                    row.TransportOrderCostCalcSupplierOrderDeliveries.Add(new TransportOrderCostCalcSupplierOrderDelivery
                    {
                        DeliveryFromStockId = delivery.DeliveryFromStockId,
                        DeliveryToCustomerId = delivery.DeliveryToCustomerId,
                        DeliveryToStockId = delivery.DeliveryToStockId,
                    });
                }

                target.TransportOrderCostCalcSupplierOrders.Add(row);
            }

            target.ResultInternationalCost = target.TransportOrderCostCalcSupplierOrders.Sum(x => x.ResultInternationalCost ?? 0);

            await _dbContext.SaveChangesAsync();

            return target;
        }
    }
}
