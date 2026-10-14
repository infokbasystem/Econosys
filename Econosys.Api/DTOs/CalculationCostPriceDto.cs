namespace Econosys.Api.DTOs;

/// <summary>Registered default prices for a cost, owned by either a customer or a supplier.</summary>
public sealed record CalculationCostPriceDto(
    int CostId,
    int? CustomerId,
    int? SupplierId,
    decimal? DefaultInPrice,
    decimal? DefaultOutPrice,
    bool CalculationAutoAddSupplier);
