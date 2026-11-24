namespace Econosys.Api.DTOs;

/// <summary>A customer order connected to an invoice, with delivery and completion information.</summary>
public sealed record InvoiceCustomerOrderCompletionResponse(
    int Id, string? CustomerOrderNr, string? InvoicingInfo, string? InventoryName, double Edition,
    double Delivered, double Invoiced, double Remaining, bool Completed, DateOnly? CompletedDate);

/// <summary>A requested completion state for an invoice's customer order.</summary>
public sealed record InvoiceCustomerOrderCompletionChange(int Id, bool Completed, DateOnly? CompletedDate);

/// <summary>Completion changes for customer orders connected to an invoice.</summary>
public sealed record UpdateInvoiceCustomerOrdersRequest(IReadOnlyList<InvoiceCustomerOrderCompletionChange> Orders);
