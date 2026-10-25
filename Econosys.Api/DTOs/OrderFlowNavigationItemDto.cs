namespace Econosys.Api.DTOs;

/// <summary>A linked record in the grouped order flow. DisplayDate uses the legacy yyMMdd format.</summary>
public sealed record OrderFlowNavigationItemDto(
    string Type, int Id, string? Number, string? Name,
    string? SupplierName, string? CustomerName, string? DisplayDate);
