using System.ComponentModel.DataAnnotations;

namespace Econosys.Api.DTOs;

/// <summary>Stored business values and immutable identity of a supplier order's selected calculation row.</summary>
public sealed record CalculationRowEditDto(
    int Id, int? CalculationId, int CompanyId, int? OldDbId, CalculationRowEditValues Values);

/// <summary>Replaces business values without recalculation; the expected row protects against a changed order selection.</summary>
public sealed record UpdateLinkedCalculationRowRequest
{
    [Range(1, int.MaxValue)]
    public int ExpectedRowId { get; init; }
    [Required]
    public CalculationRowEditValues Values { get; init; } = null!;
}

/// <summary>Editable scalar calculation-row fields, including archived snapshots but excluding ownership and parent links.</summary>
public sealed record CalculationRowEditValues
{
    [Range(1, int.MaxValue)]
    public int? Edition { get; init; }
    public double? CalculationPrice { get; init; }
    public double? PurchasePrice { get; init; }
    public double? Freight { get; init; }
    public double? Other { get; init; }
    public double? Storage { get; init; }
    public double? PercentAddition { get; init; }
    public double? SalesPrice { get; init; }
    public double? NrOfPalletsPopup { get; init; }
    public double? NrOfVehiclesPopup { get; init; }
    public double? NrOfStorageMonths { get; init; }
    public double? FreightPopup { get; init; }
    public double? FreightExtra10KmPopup { get; init; }
    public double? FreightUnloadingPopup { get; init; }
    public double? OtherPopup { get; init; }
    public double? StorageUnloadingPopup { get; init; }
    public double? StoragePopup { get; init; }
    public double? StorageFreightToCustomerPopup { get; init; }
    public double? StorageInterestPopup { get; init; }
    public bool Archived { get; init; }
    public double? FreightCostPerPalletDomesticPopup { get; init; }
    public double? FreightCostPerPalletInternationalPopup { get; init; }
    public double? LoadingCostPerPallet { get; init; }
    public decimal? ArchivedPurchaseCurrencyRate { get; init; }
    public decimal? ArchivedSalesCurrencyRate { get; init; }
    [Range(0, int.MaxValue)]
    public int? StorageNrOfPallets { get; init; }
    [Range(1, int.MaxValue)]
    public int? CustomerDeliveryAddressId { get; init; }
    public decimal? FreightCostPerFtlInternationalPopup { get; init; }
    public bool ArchivedAutoFreightCalc { get; init; }
    public bool ArchivedAutoFreightPrice { get; init; }
    public int? ArchivedPalletWidth { get; init; }
    public int? ArchivedPalletHeight { get; init; }
    public int? ArchivedPalletLength { get; init; }
    public bool ArchivedPalletIsStackable { get; init; }
    public int? ArchivedPalletType { get; init; }
    public int? ArchivedEditionPerPallet { get; init; }
    public int? ArchivedPalletsPerTruck { get; init; }
    public double? ArchivedFreightPerPallet { get; init; }
    public decimal? ArchivedPalletCalcFactor { get; init; }
    public double? ArchivedLoadingPerDelivery { get; init; }
    public double? ArchivedFreightPerVehicle { get; init; }
    [Range(1, int.MaxValue)]
    public int? ArchivedSupplierFactoryId { get; init; }
    [Range(1, int.MaxValue)]
    public int? ArchivedCustomerDeliveryAddressId { get; init; }
    [MaxLength(50)]
    public string? ArchivedDeliveryAddressCountryCode { get; init; }
    public int? ArchivedDeliveryAddressPostalNr { get; init; }
    [MaxLength(255)]
    public string? ArchivedLoadingInstruction { get; init; }
    [MaxLength(255)]
    public string? ArchivedFreightNote { get; init; }
    public bool CalcPalletPriceUsed { get; init; }
    public bool CalcFtlPriceUsed { get; init; }
    public decimal? FreightTotalPopupPerUnit { get; init; }
    public bool ArchivedPriceListUseAdjustedPalletPlaces { get; init; }
    public decimal? ArchivedPriceListPricePerPallet { get; init; }
    public decimal? ArchivedPriceListPriceFtl { get; init; }
    public decimal? ArchivedPriceListAdditionSekPerPallet { get; init; }
    public decimal? ArchivedPriceListSecaMarpolPallet { get; init; }
    public decimal? ArchivedPriceListSecaMarpolFtl { get; init; }
    public decimal? ArchivedPriceListDmtPercentPallet { get; init; }
    public decimal? ArchivedPriceListDmtPercentFtl { get; init; }
    public decimal? ArchivedPriceListAdditionCurrencyPercentPallet { get; init; }
    public decimal? ArchivedPriceListAdditionCurrencyPercentFtl { get; init; }
    public decimal? ArchivedPriceListOtherPallet { get; init; }
    public decimal? ArchivedPriceListOtherFtl { get; init; }
    public decimal? ArchivedPriceListOtherPercentPallet { get; init; }
    public decimal? ArchivedPriceListOtherPercentFtl { get; init; }
    public decimal? ArchivedPriceListAdditionTotalPercentPallet { get; init; }
    public decimal? ArchivedPriceListAdditionTotalPercentFtl { get; init; }
    public decimal? ArchivedPriceListUnloadingCost { get; init; }
    public decimal? ArchivedPriceListLoadingCost { get; init; }
    public decimal? ArchivedPriceListCurrencyId { get; init; }
    public decimal? ArchivedPriceListCurrencyRate { get; init; }
    [MaxLength(1000)]
    public string? ArchivedPriceListSummary { get; init; }
    public decimal? ArchivedPriceListCurrencyIdFtl { get; init; }
    public decimal? ArchivedPriceListCurrencyRateFtl { get; init; }
    public decimal? ArchivedPriceListPricePerPalletDomestic { get; init; }
    public decimal? ArchivedPriceListAdditionSekPerPalletDomestic { get; init; }
    public decimal? ArchivedPriceListSecaMarpolPalletDomestic { get; init; }
    public decimal? ArchivedPriceListDmtPercentPalletDomestic { get; init; }
    public decimal? ArchivedPriceListAdditionCurrencyPercentPalletDomestic { get; init; }
    public decimal? ArchivedPriceListOtherPalletDomestic { get; init; }
    public decimal? ArchivedPriceListOtherPercentPalletDomestic { get; init; }
    public decimal? ArchivedPriceListAdditionTotalPercentPalletDomestic { get; init; }
    public decimal? ArchivedPriceListUnloadingCostDomestic { get; init; }
    public decimal? ArchivedPriceListLoadingCostDomestic { get; init; }
    public decimal? ArchivedPriceListCurrencyIdDomestic { get; init; }
    public decimal? ArchivedPriceListCurrencyRateDomestic { get; init; }
    public int? ArchivedCalculatedPriceListColumn { get; init; }
    public decimal? ArchivedSupplierEurPalletCost { get; init; }
    public decimal? ArchivedCustomerPalletCost { get; init; }
    [Range(1, int.MaxValue)]
    public int? ArchivedPalletFormatId { get; init; }
    [Range(1, int.MaxValue)]
    public int? ArchivedPalletFormatIdSupplierOrder { get; init; }
    [Range(1, int.MaxValue)]
    public int? ArchivedPalletFormatIdCustomerOrder { get; init; }
    public decimal? PalletCalcFactor { get; init; }
    public decimal? ArchivedPalletPriceIn { get; init; }
    public decimal? ArchivedPalletPriceOut { get; init; }
    public decimal? PackagingCost { get; init; }
    public decimal? PreCalcAdditionPercent { get; init; }
    [MaxLength(50)]
    public string? Info { get; init; }
    public int? ArchivedNrOfPerBundle { get; init; }
    public int? ArchivedNrOfPerBundlePerPallet { get; init; }
    public int? ArchivedNrOfPerOuterPackaging { get; init; }
    public int? ArchivedNrOfOuterPackagingPerPallet { get; init; }
    public bool ArchivedDoBuntas { get; init; }
    public bool ArchivedIsYtterforpackning { get; init; }
}
