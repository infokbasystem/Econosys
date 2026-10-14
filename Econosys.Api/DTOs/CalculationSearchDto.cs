namespace Econosys.Api.DTOs;

public sealed record SearchCalculationsRequest
{
    public FilterRequest Filter { get; init; } = new();
    public PaginationRequest Pagination { get; init; } = new();
    public List<SortRequest> OrderBy { get; init; } = new();
}

public sealed record CalculationSearchResultDto(
    int Id,
    string? ProductName,
    string? ProductMaterial,
    string? ProductConstruction,
    string ProductFormat,
    string? SupplierName,
    string? CustomerName,
    DateTimeOffset? CreatedAt,
    DateTimeOffset? EditedAt);