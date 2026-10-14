using System.ComponentModel.DataAnnotations;

namespace Econosys.Api.DTOs;

public sealed class RefreshCalculationFreightRequest
{
    public int? CustomerId { get; init; }
    public int? SupplierId { get; init; }
    public int? SupplierFactoryId { get; init; }
    public int? CustomerDeliveryAddressId { get; init; }
    public string? DeliveryAddressCountryCode { get; init; }
    public int? DeliveryAddressPostalNr { get; init; }
    public bool AutoFreightCalc { get; init; }
    public bool PalletIsStackable { get; init; }
    public int? EditionPerPallet { get; init; }
    public int? PalletsPerTruck { get; init; }
    public int? PalletLength { get; init; }
    public int? PalletWidth { get; init; }
    public int? UnitMultiplier { get; init; }
    public double? SalesCurrencyRate { get; init; }
    public int SalesDecimals { get; init; }
    public IReadOnlyList<RefreshCalculationFreightRowRequest> Rows { get; init; } = [];
}

public sealed class RefreshCalculationFreightRowRequest
{
    [MaxLength(100)]
    public string ClientKey { get; init; } = string.Empty;

    [Range(1, int.MaxValue)]
    public int? Id { get; init; }

    [Range(1, int.MaxValue)]
    public int? Edition { get; init; }

    [Range(0, double.MaxValue)]
    public double? NrOfPallets { get; init; }

    [Range(0, double.MaxValue)]
    public double? NrOfVehicles { get; init; }

    [Range(0, double.MaxValue)]
    public decimal? PalletCalcFactor { get; init; }

    [Range(1, int.MaxValue)]
    public int? CustomerDeliveryAddressId { get; init; }
}

public sealed record RefreshCalculationFreightResponse(
    IReadOnlyList<RefreshCalculationFreightRowResult> Rows,
    IReadOnlyList<string> Warnings);

public sealed record RefreshCalculationFreightRowResult(
    string ClientKey,
    int? Id,
    int Edition,
    double NrOfPallets,
    double NrOfVehicles,
    decimal PalletCalcFactor,
    double FreightDomesticPerPallet,
    double FreightInternationalPerPallet,
    decimal FreightPerVehicle,
    double FreightUnloading,
    double FreightTotal,
    decimal FreightPerUnit,
    decimal PreCalcAdditionPercent,
    bool CalcPalletPriceUsed,
    bool CalcFtlPriceUsed);