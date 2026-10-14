namespace Econosys.Api.Services;

/// <summary>Explicit input to the legacy calculation freight algorithm.</summary>
public sealed record CalculationFreightInput(
    decimal Pallets,
    int PalletsPerTruck,
    bool Stackable,
    bool Automatic,
    decimal Vehicles,
    decimal DomesticPerPallet,
    decimal InternationalPerPallet,
    decimal PerVehicle,
    decimal Unloading,
    decimal PalletFactor,
    decimal AdditionPercent,
    int Edition,
    int UnitMultiplier,
    decimal SalesCurrencyRate,
    int Decimals);

/// <summary>Calculated freight, without applying it to the sales row.</summary>
public sealed record CalculationFreightResult(
    int PalletPlaces,
    decimal Vehicles,
    decimal Total,
    decimal PerUnit,
    bool PalletPriceUsed,
    bool FtlPriceUsed);

/// <summary>Price-list inputs use stored fractional surcharges, not percentage points.</summary>
public sealed record CalculationListPriceInput(
    decimal Price,
    decimal Dmt,
    decimal CurrencyAddition,
    decimal OtherPercent,
    decimal Seca,
    decimal Other,
    decimal AdditionPerPallet,
    decimal TotalAddition,
    decimal CurrencyRate);

/// <summary>Numerical calculation rules extracted from the legacy calculation view model.</summary>
public static class CalculationPricingService
{
    public static CalculationFreightResult CalculateFreight(CalculationFreightInput input)
    {
        ValidateUnits(input.Edition, input.UnitMultiplier, input.SalesCurrencyRate, input.Decimals);
        if (input.Pallets < 0 || input.Vehicles < 0 || input.PalletFactor < 0)
        {
            throw new ArgumentException("Pallets, vehicles and pallet factor must not be negative.");
        }
        if (input.Automatic && input.PalletsPerTruck <= 0)
        {
            throw new ArgumentException("Automatic freight requires a positive truck capacity.");
        }

        var roundedPallets = checked((int)Math.Ceiling(input.Pallets));
        var vehicles = input.Automatic ? roundedPallets / input.PalletsPerTruck : input.Vehicles;
        var remainder = input.Automatic ? roundedPallets % input.PalletsPerTruck : input.Pallets;
        var places = checked((int)Math.Ceiling(remainder / (input.Stackable ? 2m : 1m)));
        var mixedPrice = input.PerVehicle * vehicles
            + (input.DomesticPerPallet + input.InternationalPerPallet) * places * input.PalletFactor
            + (places > 0 ? input.Unloading : 0);
        var ftlPrice = input.PerVehicle * (vehicles + (places > 0 ? 1 : 0));
        var allFtl = input.Automatic && ftlPrice < mixedPrice;
        var total = Math.Round(allFtl ? ftlPrice : mixedPrice, input.Decimals)
            * (1 + input.AdditionPercent / 100);
        var perUnit = Math.Round(total / input.Edition * input.UnitMultiplier / input.SalesCurrencyRate, input.Decimals);

        return new CalculationFreightResult(
            places, vehicles, total, perUnit,
            !allFtl && (input.Automatic ? places > 0 : input.Pallets > 0),
            allFtl || vehicles >= 1);
    }

    public static (int Column, bool Adjusted) GetPriceColumn(int places, decimal factor, bool tiered)
    {
        if (places < 0 || factor < 0)
        {
            throw new ArgumentException("Pallet places and factor must not be negative.");
        }
        var effectivePlaces = tiered ? places * factor : places;
        var adjusted = tiered && effectivePlaces - Math.Truncate(effectivePlaces) <= 0.25m;
        var column = adjusted ? Math.Truncate(effectivePlaces) : Math.Ceiling(effectivePlaces);
        return (checked((int)Math.Min(column, 32)), adjusted);
    }

    public static decimal CalculateListPalletPrice(
        CalculationListPriceInput input, int places, decimal factor, bool tiered, bool adjusted)
    {
        if (places == 0)
        {
            return 0;
        }
        if (places < 0 || factor <= 0 || input.CurrencyRate <= 0)
        {
            throw new ArgumentException("Valid pallet places, factor and currency rate are required.");
        }
        var multiplier = 1 + input.Dmt + input.CurrencyAddition + input.OtherPercent;
        var additions = input.Seca + input.Other + input.AdditionPerPallet;
        var totalMultiplier = (1 + input.TotalAddition) * input.CurrencyRate;
        if (tiered && adjusted)
        {
            return (input.Price * factor * multiplier + additions) * totalMultiplier / factor;
        }
        var price = (input.Price * multiplier + additions) * totalMultiplier;
        return tiered ? price * Math.Ceiling(places * factor) / places / factor : price;
    }

    public static decimal CalculateListFtlPrice(CalculationListPriceInput input) =>
        (input.Price * (1 + input.Dmt + input.CurrencyAddition + input.OtherPercent) + input.Seca + input.Other)
        * (1 + input.TotalAddition) * input.CurrencyRate;

    public static (decimal TotalCost, decimal SalesPrice, decimal AdditionPercent, decimal Contribution, decimal TotalContribution)
        CalculateSalesRow(decimal purchase, decimal currencyRate, decimal freight, decimal other,
            decimal storage, decimal packaging, decimal sales, decimal addition, bool lockOutPrice,
            int edition, int unitMultiplier, decimal salesRate, int decimals)
    {
        ValidateUnits(edition, unitMultiplier, salesRate, decimals);
        var total = purchase * currencyRate + freight + other + storage + packaging;
        if (lockOutPrice)
        {
            sales = total * (1 + addition / 100);
        }
        else
        {
            addition = total == 0 ? 0 : 100 * (sales / total - 1);
        }
        var contribution = Math.Round((sales - total) * salesRate, decimals);
        return (total, sales, addition, contribution, Math.Round(contribution * edition / unitMultiplier, decimals));
    }

    public static decimal? CalculatePackaging(decimal purchase, decimal sales, decimal purchaseRate,
        decimal salesRate, decimal pallets, int edition, int multiplier)
    {
        ValidateUnits(edition, multiplier, salesRate, 0);
        var balance = (sales - purchase * purchaseRate / salesRate) * Math.Ceiling(pallets) / (edition / (decimal)multiplier);
        return balance < 0 ? -balance : null;
    }

    private static void ValidateUnits(int edition, int multiplier, decimal rate, int decimals)
    {
        if (edition <= 0 || multiplier <= 0 || rate <= 0 || decimals is < 0 or > 10)
        {
            throw new ArgumentException("Positive edition, unit multiplier and currency rate, and 0-10 decimals are required.");
        }
    }
}