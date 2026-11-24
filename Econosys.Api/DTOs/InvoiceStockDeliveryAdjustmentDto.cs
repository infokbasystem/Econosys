namespace Econosys.Api.DTOs;

/// <summary>A proposed change to a stock delivery's quantity.</summary>
public sealed record InvoiceStockDeliveryAdjustmentResponse(
    int DeliveryId, double CurrentQuantity, double CreditedQuantity, double NewQuantity);

/// <summary>The delivery changes reviewed by the user.</summary>
public sealed record ApplyInvoiceStockDeliveryAdjustmentsRequest(
    IReadOnlyList<InvoiceStockDeliveryAdjustmentResponse> Deliveries);
