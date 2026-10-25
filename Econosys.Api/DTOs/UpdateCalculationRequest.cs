using System.ComponentModel.DataAnnotations;

namespace Econosys.Api.DTOs;

public sealed class UpdateCalculationRequest
{
    public bool IsArchived { get; init; }
    public int? CustomerId { get; init; }
    public int? SupplierId { get; init; }
    [Range(1, int.MaxValue)]
    public int? UnitId { get; init; }
    [Range(1, int.MaxValue)]
    public int? PurchaseCurrencyId { get; init; }
    [Range(1, int.MaxValue)]
    public int? SalesCurrencyId { get; init; }

    [Range(0, double.MaxValue)]
    public double? PurchaseCurrencyRate { get; init; }

    [Range(0, double.MaxValue)]
    public double? SalesCurrencyRate { get; init; }

    [Range(0, double.MaxValue)]
    public double? CurrencyRate { get; init; }

    [Range(0, 10)]
    public int PurchaseDecimals { get; init; }

    [Range(0, 10)]
    public int SalesDecimals { get; init; }

    public bool LockOutPrice { get; init; }

    [MaxLength(255)]
    public string? CalculationFollowUp { get; init; }

    [MaxLength(1000)]
    public string? OtherCostNote { get; init; }

    [MaxLength(255)]
    public string? FreightNote { get; init; }

    [MaxLength(255)]
    public string? LoadingInstruction { get; init; }

    public bool AutomaticLoadingInstruction { get; init; }
    public bool AutoFreightCalc { get; init; }
    public bool AutoFreightPrice { get; init; }
    public bool DeactivateFreightCalculation { get; init; }
    [Range(1, int.MaxValue)]
    public int? SupplierFactoryId { get; init; }
    [Range(1, int.MaxValue)]
    public int? CustomerDeliveryAddressId { get; init; }

    [Range(0, double.MaxValue)]
    public double? FreightPerPallet { get; init; }

    [Range(0, double.MaxValue)]
    public double? FreightPerVehicle { get; init; }

    [Range(0, double.MaxValue)]
    public double? LoadingPerDelivery { get; init; }

    [MaxLength(50)]
    public string? DeliveryAddressCountryCode { get; init; }

    [Range(0, int.MaxValue)]
    public int? DeliveryAddressPostalNr { get; init; }

    [Range(1, int.MaxValue)]
    public int? PalletFormatId { get; init; }

    [Range(0, int.MaxValue)]
    public int? PalletLength { get; init; }

    [Range(0, int.MaxValue)]
    public int? PalletWidth { get; init; }

    [Range(0, int.MaxValue)]
    public int? PalletHeight { get; init; }

    [Range(0, int.MaxValue)]
    public int? EditionPerPallet { get; init; }

    public bool PalletIsStackable { get; init; }

    [Range(0, int.MaxValue)]
    public int? PalletsPerTruck { get; init; }

    [Range(0, double.MaxValue)]
    public decimal? PalletCalcFactor { get; init; }

    [Range(0, double.MaxValue)]
    public decimal? PalletPriceIn { get; init; }

    [Range(0, double.MaxValue)]
    public decimal? PalletPriceOut { get; init; }

    public bool DoDebitPalletPrice { get; init; }
    public bool DoBuntas { get; init; }

    [Range(0, int.MaxValue)]
    public int? NrOfPerBundle { get; init; }

    public bool IsYtterforpackning { get; init; }

    [Range(0, int.MaxValue)]
    public int? NrOfPerOuterPackaging { get; init; }

    public bool CalcStorage { get; init; }

    [Range(0, double.MaxValue)]
    public double? LoadingPerPallet { get; init; }

    [Range(0, double.MaxValue)]
    public double? StoragePerM2 { get; init; }

    [Range(0, double.MaxValue)]
    public double? Interest { get; init; }

    [Range(0, double.MaxValue)]
    public double? FreightFromStockPerPallet { get; init; }

    public IReadOnlyList<UpdateCalculationRowRequest>? Rows { get; init; }
    public IReadOnlyList<int>? DeletedRowIds { get; init; }

    public bool ApprovePackaging { get; init; }
    public bool ApproveFreight { get; init; }

    /// <summary>Stamps the other-cost approval with the current user and time.</summary>
    public bool ApproveOtherCosts { get; init; }

    /// <summary>Stamps the storage approval with the current user and time.</summary>
    public bool ApproveStorage { get; init; }
}

public sealed class UpdateCalculationRowRequest
{
    [Range(1, int.MaxValue)]
    public int? Id { get; init; }

    [Range(1, int.MaxValue)]
    public int? Edition { get; init; }

    public double? PurchasePrice { get; init; }
    public double? Freight { get; init; }
    public double? Other { get; init; }
    public double? Storage { get; init; }
    public decimal? PackagingCost { get; init; }
    public double? PercentAddition { get; init; }
    public double? SalesPrice { get; init; }
    public double? NrOfPallets { get; init; }
    public double? NrOfVehicles { get; init; }
    public decimal? PalletCalcFactor { get; init; }
    public double? FreightDomesticPerPallet { get; init; }
    public double? FreightInternationalPerPallet { get; init; }
    public decimal? FreightPerVehicle { get; init; }
    public double? FreightUnloading { get; init; }
    public double? FreightTotal { get; init; }
    public decimal? FreightPerUnit { get; init; }
    public decimal? PreCalcAdditionPercent { get; init; }
    public bool CalcPalletPriceUsed { get; init; }
    public bool CalcFtlPriceUsed { get; init; }

    [Range(1, int.MaxValue)]
    public int? CustomerDeliveryAddressId { get; init; }

    [Range(0, int.MaxValue)]
    public int? StorageNrOfPallets { get; init; }

    [Range(0, double.MaxValue)]
    public double? StorageMonths { get; init; }

    /// <summary>Null leaves the saved cost rows untouched; a list replaces them.</summary>
    public IReadOnlyList<UpdateCalculationRowCostRequest>? Costs { get; init; }

    [MaxLength(50)]
    public string? Info { get; init; }
}

public sealed class UpdateCalculationRowCostRequest
{
    [Range(1, int.MaxValue)]
    public int? Id { get; init; }

    [Range(1, int.MaxValue)]
    public int? CostId { get; init; }

    [MaxLength(100)]
    public string? CostTypeText { get; init; }

    [Range(0, 99999)]
    public decimal? NrOf { get; init; }

    [Range(-9999999999, 9999999999)]
    public decimal? InPrice { get; init; }

    [Range(1, int.MaxValue)]
    public int? InPriceCurrencyId { get; init; }

    [Range(-9999999999, 9999999999)]
    public decimal? OutPrice { get; init; }

    public bool DoDebit { get; init; }
}