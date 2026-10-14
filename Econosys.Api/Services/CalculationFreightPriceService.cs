using Econosys.Api.Data;
using Econosys.Api.DTOs;
using Econosys.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Econosys.Api.Services;

public sealed class CalculationFreightPriceService(ApplicationDbContext dbContext)
{
    public async Task<RefreshCalculationFreightResponse> RefreshAsync(
        RefreshCalculationFreightRequest request,
        CancellationToken cancellationToken)
    {
        var warnings = new List<string>();
        if (!request.SupplierFactoryId.HasValue)
        {
            return EmptyResponse(request, "Ingen transportprislista är kopplad till vald fabrik.");
        }

        var factoryLists = await LoadPriceLists(
            dbContext.TransportCostPriceLists.Where(item => item.SupplierFactoryTransportCostPriceLists
                .Any(link => link.SupplierFactoryId == request.SupplierFactoryId.Value)),
            cancellationToken);
        if (factoryLists.Count == 0)
        {
            return EmptyResponse(request, "Ingen transportprislista är kopplad till vald fabrik.");
        }

        var factorValues = await LoadPalletFactorsAsync(request, cancellationToken);
        var results = new List<RefreshCalculationFreightRowResult>();
        if (request.AutoFreightCalc && request.PalletsPerTruck is not > 0)
        {
            return EmptyResponse(request, "Pall per bil kunde inte beräknas. Kontrollera pallfaktor och antal per pall.");
        }
        var routes = new Dictionary<int, FreightRoute?>();
        foreach (var row in request.Rows.Where(item => item.Edition is > 0))
        {
            var addressId = row.CustomerDeliveryAddressId ?? request.CustomerDeliveryAddressId;
            var routeKey = addressId ?? 0;
            if (!routes.TryGetValue(routeKey, out var route))
            {
                route = await ResolveRouteAsync(request, addressId, factoryLists, warnings, cancellationToken);
                routes[routeKey] = route;
            }
            if (route is null)
            {
                results.Add(EmptyRow(row));
                continue;
            }

            var international = route.International;
            var internationalData = route.InternationalData;
            var domestic = route.Domestic;
            var domesticData = route.DomesticData;
            var pallets = row.NrOfPallets.HasValue
                ? (decimal)row.NrOfPallets.Value
                : request.EditionPerPallet is > 0
                    ? row.Edition!.Value / (decimal)request.EditionPerPallet.Value
                    : 0m;
            if (row.NrOfPallets is null && request.EditionPerPallet is not > 0)
            {
                warnings.Add("Ange antal per pall innan fraktpriset uppdateras.");
                continue;
            }

            var places = GetPalletPlaces(pallets, request.PalletsPerTruck, request.PalletIsStackable, request.AutoFreightCalc);
            var palletFactor = request.AutoFreightCalc
                ? GetPalletFactor(factorValues, places)
                : row.PalletCalcFactor ?? 1m;
            if (request.AutoFreightCalc && palletFactor == 0)
            {
                warnings.Add("Pallfaktor saknas för en eller flera kalkylrader.");
            }

            var internationalPalletPrice = CalculatePalletPrice(international, internationalData, places, palletFactor);
            var domesticPalletPrice = domestic is null || domesticData is null
                ? 0m
                : CalculatePalletPrice(domestic, domesticData, places, palletFactor);
            var ftlPrice = route.Ftl is null || route.FtlData is null
                ? 0m
                : CalculateFtlPrice(route.Ftl, route.FtlData);
            var unloading = places == 0
                ? 0m
                : (international.UnloadingCost ?? 0m) * GetCurrencyRate(international)
                    + (domestic?.UnloadingCost ?? 0m) * GetCurrencyRate(domestic);
            var additionPercent = international.PreCalcAdditionPercent ?? 0m;
            var salesRate = ToDecimal(request.SalesCurrencyRate);
            var unitMultiplier = request.UnitMultiplier ?? 0;
            if (salesRate <= 0 || unitMultiplier <= 0)
            {
                warnings.Add("Enhet eller försäljningskurs saknas för fraktberäkningen.");
                continue;
            }

            var freight = CalculationPricingService.CalculateFreight(new CalculationFreightInput(
                pallets,
                request.PalletsPerTruck ?? 0,
                request.PalletIsStackable,
                request.AutoFreightCalc,
                (decimal)(row.NrOfVehicles ?? 0),
                domesticPalletPrice,
                internationalPalletPrice,
                ftlPrice,
                unloading,
                palletFactor,
                additionPercent,
                row.Edition!.Value,
                unitMultiplier,
                salesRate,
                Math.Clamp(request.SalesDecimals, 0, 10)));

            results.Add(new RefreshCalculationFreightRowResult(
                row.ClientKey,
                row.Id,
                row.Edition.Value,
                (double)pallets,
                (double)freight.Vehicles,
                palletFactor,
                (double)domesticPalletPrice,
                (double)internationalPalletPrice,
                ftlPrice,
                (double)unloading,
                (double)freight.Total,
                freight.PerUnit,
                additionPercent,
                freight.PalletPriceUsed,
                freight.FtlPriceUsed));
        }

        return new RefreshCalculationFreightResponse(results, warnings.Distinct().ToArray());
    }

    private async Task<string[]?> LoadPalletFactorsAsync(
        RefreshCalculationFreightRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.AutoFreightCalc)
        {
            return null;
        }

        var length = request.PalletLength;
        var width = request.PalletWidth;
        if (length is not > 0 || width is not > 0)
        {
            return null;
        }

        var factor = await dbContext.PalletFactors
            .AsNoTracking()
            .Where(item => item.LengthFrom <= length && item.LengthTo >= length)
            .Where(item => item.WidthFrom <= width && item.WidthTo >= width)
            .OrderBy(item => item.Id)
            .Select(item => item.PalletFactors)
            .FirstOrDefaultAsync(cancellationToken);
        return factor?.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private static Task<List<TransportCostPriceList>> LoadPriceLists(
        IQueryable<TransportCostPriceList> query,
        CancellationToken cancellationToken) => query
            .AsNoTracking()
            .AsSplitQuery()
            .Include(item => item.Currency)
            .Include(item => item.TransportCostPriceListData)
            .OrderBy(item => item.Id)
            .ToListAsync(cancellationToken);

    private sealed record FreightRoute(
        TransportCostPriceList International,
        TransportCostPriceListData InternationalData,
        TransportCostPriceList? Domestic,
        TransportCostPriceListData? DomesticData,
        TransportCostPriceList? Ftl,
        TransportCostPriceListData? FtlData);

    private async Task<FreightRoute?> ResolveRouteAsync(
        RefreshCalculationFreightRequest request,
        int? addressId,
        IReadOnlyList<TransportCostPriceList> factoryLists,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var address = addressId.HasValue
            ? await dbContext.CustomerDeliveryAddresses
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == addressId.Value, cancellationToken)
            : null;
        var connectedAddress = address is null
            ? null
            : await dbContext.CustomerDeliveryAddresses
                .AsNoTracking()
                .Where(item => item.NextTransportDeliveryAddressId == address.Id)
                .OrderBy(item => item.Id)
                .FirstOrDefaultAsync(cancellationToken);

        // Manual country/postal fields belong to the header address only.
        var useOverride = addressId == request.CustomerDeliveryAddressId;
        var destinationCountry = NormalizeCountry(useOverride
            ? request.DeliveryAddressCountryCode ?? address?.CountryCode
            : address?.CountryCode);
        var destinationPostal = useOverride
            ? request.DeliveryAddressPostalNr ?? address?.PostalNrValue
            : address?.PostalNrValue;
        var originCountry = NormalizeCountry(connectedAddress?.CountryCode ?? destinationCountry);
        var originPostal = connectedAddress?.PostalNrValue ?? destinationPostal;
        var internationalMatch = FindPrice(factoryLists, originCountry, originPostal, warnings, "utrikes");
        if (internationalMatch is null)
        {
            warnings.Add("Ingen transportprisrad hittades för land och postnummer.");
            return null;
        }
        var international = internationalMatch.Value.List;
        var internationalData = internationalMatch.Value.Data;

        var inventoryId = connectedAddress?.InventoryId;
        var inventoryLists = inventoryId.HasValue
            ? await LoadPriceLists(
                dbContext.TransportCostPriceLists.Where(item => item.InventoryTransportCostPriceLists
                    .Any(link => link.InventoryId == inventoryId.Value)),
                cancellationToken)
            : [];
        var domesticMatch = FindPrice(inventoryLists, destinationCountry, destinationPostal, warnings, "omlastning");
        if (connectedAddress is not null && domesticMatch is null)
        {
            warnings.Add("Ingen transportprislista är kopplad till via-lagret.");
        }

        if (connectedAddress is null && domesticMatch is null && !string.IsNullOrWhiteSpace(internationalData.Transhipment))
        {
            var transhipmentLists = await LoadPriceLists(
                dbContext.TransportCostPriceLists.Where(item => item.ImportName == internationalData.Transhipment),
                cancellationToken);
            var transhipmentList = transhipmentLists.FirstOrDefault();
            if (transhipmentList is null)
            {
                warnings.Add("Kopplad inrikesprislista saknas.");
            }
            else
            {
                domesticMatch = FindPrice([transhipmentList], destinationCountry, destinationPostal, warnings, "inrikes");
            }
        }

        var ftlCountry = connectedAddress is null ? originCountry : destinationCountry;
        var ftlPostal = connectedAddress is null ? originPostal : destinationPostal;
        var ftlMatch = FindPrice(factoryLists, ftlCountry, ftlPostal, warnings, "FTL");
        return new FreightRoute(
            international,
            internationalData,
            domesticMatch?.List,
            domesticMatch?.Data,
            ftlMatch?.List,
            ftlMatch?.Data);
    }

    private static RefreshCalculationFreightResponse EmptyResponse(
        RefreshCalculationFreightRequest request,
        string warning) => new(
            request.Rows
                .Where(row => row.Edition is > 0)
                .Select(EmptyRow)
                .ToArray(),
            [warning]);

    private static RefreshCalculationFreightRowResult EmptyRow(RefreshCalculationFreightRowRequest row) => new(
        row.ClientKey,
        row.Id,
        row.Edition!.Value,
        row.NrOfPallets ?? 0,
        row.NrOfVehicles ?? 0,
        row.PalletCalcFactor ?? 0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        false,
        false);

    private static decimal GetPalletPlaces(decimal pallets, int? palletsPerTruck, bool stackable, bool automatic)
    {
        var roundedPallets = (int)Math.Ceiling(pallets);
        if (automatic && palletsPerTruck is > 0)
        {
            var remainder = roundedPallets % palletsPerTruck.Value;
            return Math.Ceiling(remainder / (stackable ? 2m : 1m));
        }

        return Math.Ceiling(pallets / (stackable ? 2m : 1m));
    }

    private static decimal GetPalletFactor(string[]? factors, decimal places)
    {
        if (places == 0)
        {
            return 1m;
        }
        var index = (int)places - 1;
        if (factors is null || index < 0 || index >= factors.Length)
        {
            return 0m;
        }
        var value = factors[index];
        var culture = value.Contains(',') && !value.Contains('.')
            ? System.Globalization.CultureInfo.GetCultureInfo("sv-SE")
            : System.Globalization.CultureInfo.InvariantCulture;
        return decimal.TryParse(value, System.Globalization.NumberStyles.Number, culture, out var factor)
            ? factor
            : 0m;
    }

    private static (TransportCostPriceList List, TransportCostPriceListData Data)? FindPrice(
        IReadOnlyList<TransportCostPriceList> lists,
        string? country,
        int? postal,
        List<string> warnings,
        string leg)
    {
        var matches = lists.SelectMany(list => list.TransportCostPriceListData
                .Where(data => Matches(data, country, postal))
                .Select(data => (List: list, Data: data)))
            .OrderBy(item => item.List.Id)
            .ThenBy(item => item.Data.Id)
            .ToArray();
        if (matches.Length > 1)
        {
            warnings.Add($"Dubblettpriser finns för {leg}.");
            return null;
        }
        return matches.Length == 0 ? null : matches[0];
    }

    private static bool Matches(TransportCostPriceListData data, string? country, int? postal) =>
        !string.IsNullOrWhiteSpace(country)
        && postal.HasValue
        && string.Equals(data.CountryCode, country, StringComparison.OrdinalIgnoreCase)
        && data.PostalNrFrom <= postal.Value
        && data.PostalNrTo >= postal.Value;

    private static decimal CalculatePalletPrice(
        TransportCostPriceList list,
        TransportCostPriceListData data,
        decimal places,
        decimal factor)
    {
        if (places == 0 || factor <= 0)
        {
            return 0m;
        }
        var (column, adjusted) = CalculationPricingService.GetPriceColumn(
            (int)places,
            factor,
            list.IsStafflad);
        var input = new CalculationListPriceInput(
            GetTierPrice(data, column),
            list.DmtPercentPallet ?? 0m,
            list.AdditionCurrencyPercentPallet ?? 0m,
            list.OtherPercentPallet ?? 0m,
            list.SecaMarpolPallet ?? 0m,
            list.OtherPallet ?? 0m,
            data.AdditionSekPerPallet ?? 0m,
            list.AdditionTotalPercentPallet ?? 0m,
            GetCurrencyRate(list));
        return CalculationPricingService.CalculateListPalletPrice(input, (int)places, factor, list.IsStafflad, adjusted);
    }

    private static decimal CalculateFtlPrice(TransportCostPriceList list, TransportCostPriceListData data) =>
        CalculationPricingService.CalculateListFtlPrice(new CalculationListPriceInput(
            data.PFTL ?? 0m,
            list.DmtPercentFtl ?? 0m,
            list.AdditionCurrencyPercentFtl ?? 0m,
            list.OtherPercentFtl ?? 0m,
            list.SecaMarpolFtl ?? 0m,
            list.OtherFtl ?? 0m,
            0m,
            list.AdditionTotalPercentFtl ?? 0m,
            GetCurrencyRate(list)));

    private static decimal GetTierPrice(TransportCostPriceListData data, int column) => column switch
    {
        1 => data.P1 ?? 0m,
        2 => data.P2 ?? 0m,
        3 => data.P3 ?? 0m,
        4 => data.P4 ?? 0m,
        5 => data.P5 ?? 0m,
        6 => data.P6 ?? 0m,
        7 => data.P7 ?? 0m,
        8 => data.P8 ?? 0m,
        9 => data.P9 ?? 0m,
        10 => data.P10 ?? 0m,
        11 => data.P11 ?? 0m,
        12 => data.P12 ?? 0m,
        13 => data.P13 ?? 0m,
        14 => data.P14 ?? 0m,
        15 => data.P15 ?? 0m,
        16 => data.P16 ?? 0m,
        17 => data.P17 ?? 0m,
        18 => data.P18 ?? 0m,
        19 => data.P19 ?? 0m,
        20 => data.P20 ?? 0m,
        21 => data.P21 ?? 0m,
        22 => data.P22 ?? 0m,
        23 => data.P23 ?? 0m,
        24 => data.P24 ?? 0m,
        25 => data.P25 ?? 0m,
        26 => data.P26 ?? 0m,
        27 => data.P27 ?? 0m,
        28 => data.P28 ?? 0m,
        29 => data.P29 ?? 0m,
        30 => data.P30 ?? 0m,
        31 => data.P31 ?? 0m,
        32 => data.P32 ?? 0m,
        _ => 0m,
    };

    private static decimal GetCurrencyRate(TransportCostPriceList? list)
    {
        var rate = ToDecimal(list?.Currency?.RateToSek);
        return rate > 0 ? rate : 1m;
    }

    private static decimal ToDecimal(double? value) => value.HasValue && double.IsFinite(value.Value)
        && value.Value > (double)decimal.MinValue && value.Value < (double)decimal.MaxValue
            ? (decimal)value.Value
            : 0m;

    private static string? NormalizeCountry(string? country) => string.IsNullOrWhiteSpace(country)
        ? null
        : country.Trim();
}