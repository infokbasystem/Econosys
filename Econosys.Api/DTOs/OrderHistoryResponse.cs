namespace Econosys.Api.DTOs;

/// <summary>Chronological order movements and their running balances.</summary>
public sealed record OrderHistoryResponse(
    bool ShowStockBalance,
    IReadOnlyList<OrderHistoryRowResponse> Rows);

/// <summary>An invoice associated with a history movement.</summary>
public sealed record OrderHistoryInvoiceResponse(int Id, int? Number);

/// <summary>A production, delivery, stocktaking, or credit event in an order's history.</summary>
public sealed record OrderHistoryRowResponse
{
    public int Id { get; init; }
    public required string Type { get; init; }
    public required string TypeName { get; init; }
    public DateTimeOffset? Date { get; init; }
    public string Status { get; init; } = "";
    public string To { get; init; } = "";
    public double Quantity { get; init; }
    public double ProductionBalance { get; init; }
    public double StockBalance { get; init; }
    public int? TransportOrderId { get; init; }
    public int? TransportOrderNr { get; init; }
    public int? CallOffId { get; init; }
    public int? StockTakingId { get; init; }
    public IReadOnlyList<OrderHistoryInvoiceResponse> Invoices { get; init; } = [];
}
