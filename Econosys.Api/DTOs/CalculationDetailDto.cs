namespace Econosys.Api.DTOs;

/// <summary>Saved calculation details. Loading this response never refreshes prices.</summary>
public sealed record CalculationDetailDto
{
    public int Id { get; init; }
    public CalculationProductDto? Product { get; init; }
    public CalculationPartyDto? Supplier { get; init; }
    public CalculationPartyDto? Customer { get; init; }
    public int? UnitId { get; init; }
    public string? UnitName { get; init; }
    public int? UnitMultiplier { get; init; }
    public int? PurchaseCurrencyId { get; init; }
    public string? PurchaseCurrencyName { get; init; }
    public double? PurchaseCurrencyRate { get; init; }
    public int? SalesCurrencyId { get; init; }
    public string? SalesCurrencyName { get; init; }
    public double? SalesCurrencyRate { get; init; }
    public double? CurrencyRate { get; init; }
    public int PurchaseDecimals { get; init; }
    public int SalesDecimals { get; init; }
    public bool LockOutPrice { get; init; }
    public string? CalculationFollowUp { get; init; }
    public string? OtherCostNote { get; init; }
    public string? FreightNote { get; init; }
    public string? LoadingInstruction { get; init; }
    public bool AutomaticLoadingInstruction { get; init; }
    public bool AutoFreightCalc { get; init; }
    public bool AutoFreightPrice { get; init; }
    public bool DeactivateFreightCalculation { get; init; }
    public int? SupplierFactoryId { get; init; }
    public int? CustomerDeliveryAddressId { get; init; }
    public double? FreightPerPallet { get; init; }
    public double? FreightPerVehicle { get; init; }
    public double? LoadingPerDelivery { get; init; }
    public string? DeliveryAddressCountryCode { get; init; }
    public int? DeliveryAddressPostalNr { get; init; }
    public int? PalletFormatId { get; init; }
    public string? PalletFormatName { get; init; }
    public int? PalletLength { get; init; }
    public int? PalletWidth { get; init; }
    public int? PalletHeight { get; init; }
    public int? EditionPerPallet { get; init; }
    public bool PalletIsStackable { get; init; }
    public int? PalletsPerTruck { get; init; }
    public decimal? PalletCalcFactor { get; init; }
    public decimal? PalletPriceIn { get; init; }
    public decimal? PalletPriceOut { get; init; }
    public bool DoDebitPalletPrice { get; init; }
    public bool DoBuntas { get; init; }
    public int? NrOfPerBundle { get; init; }
    public bool IsYtterforpackning { get; init; }
    public int? NrOfPerOuterPackaging { get; init; }
    public bool CalcStorage { get; init; }
    public double? LoadingPerPallet { get; init; }
    public double? StoragePerM2 { get; init; }
    public double? Interest { get; init; }
    public double? FreightFromStockPerPallet { get; init; }
    public DateTimeOffset? CreatedAt { get; init; }
    public string? CreatedByName { get; init; }
    public DateTimeOffset? EditedAt { get; init; }
    public string? EditedByName { get; init; }
    public IReadOnlyList<CalculationApprovalDto> Approvals { get; init; } = [];
    public IReadOnlyList<CalculationDetailRowDto> Rows { get; init; } = [];
}

/// <summary>Product information attached to a calculation.</summary>
public sealed record CalculationProductDto(int Id, string? Name, string? Material, string? Construction,
    string? ProductCode, int? Length, int? Width, int? Height, string? Format,
    string? NoteInternal, string? NoteExternal, string? Colors, string? Varnish, string? VarnishOther,
    int? NetWeightPer1000);

/// <summary>Related supplier or customer navigation target.</summary>
public sealed record CalculationPartyDto(int Id, string? Name);

/// <summary>Saved component approval, independent of row archiving.</summary>
public sealed record CalculationApprovalDto(string Component, string? ApprovedBy, DateTimeOffset? ApprovedAt);

/// <summary>Saved edition prices and server-computed display totals.</summary>
public sealed record CalculationDetailRowDto
{
    public int Id { get; init; }
    public int? Edition { get; init; }
    public bool Archived { get; init; }
    public double? PurchasePrice { get; init; }
    public double? Freight { get; init; }
    public double? Other { get; init; }
    public double? Storage { get; init; }
    public decimal? PackagingCost { get; init; }
    public double? PercentAddition { get; init; }
    public double? SalesPrice { get; init; }
    public decimal? ConvertedPurchasePrice { get; init; }
    public decimal? TotalCost { get; init; }
    public decimal? Contribution { get; init; }
    public decimal? TotalContribution { get; init; }
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
    public int? CustomerDeliveryAddressId { get; init; }
    public int? StorageNrOfPallets { get; init; }
    public double? StorageMonths { get; init; }
    public double? StorageUnloading { get; init; }
    public double? StorageAmount { get; init; }
    public double? StorageInterest { get; init; }
    public double? StorageFreight { get; init; }
    public string? Info { get; init; }
    public string? PriceListSummary { get; init; }
    public IReadOnlyList<CalculationDetailCostDto> Costs { get; init; } = [];
    public IReadOnlyList<CalculationDocumentLinkDto> Documents { get; init; } = [];
}

/// <summary>Saved cost inputs, with their original currency and debit selection.</summary>
public sealed record CalculationDetailCostDto(int Id, string? Name, string? CostTypeText,
    decimal? NrOf, decimal? InPrice, string? CurrencyName, decimal? OutPrice, bool DoDebit,
    int? CostId = null, int? InPriceCurrencyId = null);

/// <summary>Document related to the selected calculation edition.</summary>
public sealed record CalculationDocumentLinkDto(string Type, int Id, string? Number);