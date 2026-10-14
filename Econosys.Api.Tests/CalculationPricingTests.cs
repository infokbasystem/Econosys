using Econosys.Api.Services;
using Xunit;

namespace Econosys.Api.Tests;

public class CalculationPricingTests
{
    private static CalculationFreightInput Freight => new(
        35, 33, false, true, 0, 100, 200, 5000, 100, 1, 10, 35000, 1000, 1, 2);

    [Fact]
    public void AutomaticFreightSplitsFullTrucksAndRemainder()
    {
        var result = CalculationPricingService.CalculateFreight(Freight);
        Assert.Equal(1m, result.Vehicles);
        Assert.Equal(2, result.PalletPlaces);
        Assert.Equal(6270m, result.Total);
        Assert.Equal(179.14m, result.PerUnit);
        Assert.True(result.PalletPriceUsed);
        Assert.True(result.FtlPriceUsed);
    }

    [Fact]
    public void AutomaticFreightChoosesCheaperFullTruck()
    {
        var result = CalculationPricingService.CalculateFreight(Freight with { Pallets = 32 });
        Assert.Equal(5500m, result.Total);
        Assert.False(result.PalletPriceUsed);
        Assert.True(result.FtlPriceUsed);
    }

    [Fact]
    public void StackableCapacityAndRemainderAreDistinct()
    {
        var result = CalculationPricingService.CalculateFreight(Freight with { Pallets = 69, PalletsPerTruck = 66, Stackable = true });
        Assert.Equal(1m, result.Vehicles);
        Assert.Equal(2, result.PalletPlaces);
    }

    [Fact]
    public void ManualFreightPreservesVehiclesAndDoesNotOptimize()
    {
        var result = CalculationPricingService.CalculateFreight(Freight with { Automatic = false, Vehicles = 2, Pallets = 3 });
        Assert.Equal(2m, result.Vehicles);
        Assert.Equal(3, result.PalletPlaces);
        Assert.Equal(12100m, result.Total);
    }

    [Theory]
    [InlineData("1.25", 1, true)]
    [InlineData("1.25001", 2, false)]
    [InlineData("40", 32, true)]
    public void TierBoundaryMatchesLegacy(string factor, int column, bool adjusted)
    {
        Assert.Equal((column, adjusted), CalculationPricingService.GetPriceColumn(1, decimal.Parse(factor, System.Globalization.CultureInfo.InvariantCulture), true));
    }

    [Fact]
    public void ListSurchargesUseFractionsAndPreserveOrder()
    {
        var input = new CalculationListPriceInput(100, 0.1m, 0.05m, 0.02m, 3, 4, 5, 0.1m, 10);
        Assert.Equal(1419m, CalculationPricingService.CalculateListPalletPrice(input, 1, 1, false, false));
        Assert.Equal(1364m, CalculationPricingService.CalculateListFtlPrice(input));
    }

    [Fact]
    public void LockOutPriceTrueComputesSalesFromMarkupDespiteLegacyLabel()
    {
        var result = CalculationPricingService.CalculateSalesRow(10, 2, 1, 2, 3, 4, 999, 20, true, 1000, 1000, 10, 2);
        Assert.Equal(30m, result.TotalCost);
        Assert.Equal(36m, result.SalesPrice);
        Assert.Equal(60m, result.Contribution);
    }

    [Fact]
    public void LockOutPriceFalsePreservesSalesAndComputesMarkup()
    {
        var result = CalculationPricingService.CalculateSalesRow(10, 2, 1, 2, 3, 4, 36, 999, false, 1000, 1000, 10, 2);
        Assert.Equal(20m, result.AdditionPercent);
        Assert.Equal(36m, result.SalesPrice);
    }

    [Fact]
    public void PackagingOnlyAppliesUncoveredPurchaseCost()
    {
        Assert.Null(CalculationPricingService.CalculatePackaging(10, 15, 1, 1, 2.1m, 3000, 1000));
        Assert.Equal(5m, CalculationPricingService.CalculatePackaging(15, 10, 1, 1, 2.1m, 3000, 1000));
    }

    [Theory]
    [InlineData(0, 1000, 1)]
    [InlineData(1000, 0, 1)]
    [InlineData(1000, 1000, 0)]
    public void InvalidDivisorsProduceValidationError(int edition, int multiplier, int rate)
    {
        Assert.Throws<ArgumentException>(() => CalculationPricingService.CalculateFreight(Freight with { Edition = edition, UnitMultiplier = multiplier, SalesCurrencyRate = rate }));
    }
}