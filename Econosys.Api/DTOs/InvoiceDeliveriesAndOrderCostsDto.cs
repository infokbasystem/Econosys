namespace Econosys.Api.DTOs
{
    public class InvoiceDeliveriesAndOrderCostsGroupedCustomerDto
    {
        public int? CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerInvoicingInfo { get; set; }
        public IReadOnlyList<InvoiceDeliveriesAndOrderCostsDeliveryDto> Deliveries { get; set; } = Array.Empty<InvoiceDeliveriesAndOrderCostsDeliveryDto>();
        public IReadOnlyList<InvoiceDeliveriesAndOrderCostsOrderCostDto> OrderCosts { get; set; } = Array.Empty<InvoiceDeliveriesAndOrderCostsOrderCostDto>();
        public IReadOnlyList<InvoiceDeliveriesAndOrderCostsSupplierOrderDto> SupplierOrders { get; set; } = Array.Empty<InvoiceDeliveriesAndOrderCostsSupplierOrderDto>();
    }

    public class InvoiceDeliveriesAndOrderCostsDeliveryDto
    {
        public string Type { get; set; } = string.Empty;
        public int DeliveryId { get; set; }
        public int ParentDeliveryId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public DateTime? DeliveryDate { get; set; }
        public int? CallOffId { get; set; }
        public string? CallOffNr { get; set; }
        public int? CustomerOrderId { get; set; }
        public string? CustomerOrderNr { get; set; }
        public string? CustomersOwnOrderNr { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? PurchasePrice { get; set; }
        public string? PurchasePriceCurrencyName { get; set; }
        public short? UnitMultiplicator { get; set; }
        public int? WeightPer1000 { get; set; }
        public int NrOfNotInvoicedPallets { get; set; }
        public int NrOfNotInvoicedOrderCosts { get; set; }
        public bool HasMissingWeight { get; set; }
        public bool IsWeightMissingEmailSent { get; set; }  
        public bool HasMissingCostSalesPrice { get; set; }
        public bool IsCostMissingEmailSent { get; set; }
    }

    public class InvoiceDeliveriesAndOrderCostsOrderCostDto
    {
        public string Type { get; set; } = string.Empty;
        public int Id { get; set; }
        public int? CostId { get; set; }
        public string CostName { get; set; } = string.Empty;
        public int? CustomerOrderId { get; set; }
        public string? CustomerOrderNr { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? PurchasePrice { get; set; }
        public string? PurchasePriceCurrencyName { get; set; }
        public bool HasMissingCostSalesPrice { get; set; }
    }

    public class InvoiceDeliveriesAndOrderCostsSupplierOrderDto
    {
        public int SupplierOrderId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string? SupplierOrderNr { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? PurchasePrice { get; set; }
        public string? PurchasePriceCurrencyName { get; set; }
        public short? UnitMultiplicator { get; set; }
        public int? WeightPer1000 { get; set; }
        public bool HasMissingCostSalesPrice { get; set; }
    }

    public class InvoiceDeliveriesAndOrderCostsListRowDto
    {
        public string Type { get; set; } = string.Empty;
        public int? CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerInvoicingInfo { get; set; }
        public int? DeliveryId { get; set; }
        public int? ParentDeliveryId{ get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public DateTime? DeliveryDate { get; set; }
        public int? CallOffId { get; set; }
        public string? CallOffNr { get; set; }
        public int? CustomerOrderId { get; set; }
        public string? CustomerOrderNr { get; set; }
        public string? CustomersOwnOrderNr { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? PurchasePrice { get; set; }
        public string? PurchasePriceCurrencyName { get; set; }
        public short? UnitMultiplicator { get; set; }
        public int? WeightPer1000 { get; set; }
        public int? NrOfNotInvoicedPallets { get; set; }
        public int? NrOfNotInvoicedOrderCosts { get; set; }
        public bool? HasMissingWeight { get; set; }
        public bool? IsWeightMissingEmailSent { get; set; }
        public bool? HasMissingCostSalesPrice { get; set; }
        public bool? IsCostMissingEmailSent { get; set; }
        public int? OrderCostId { get; set; }
        public int? CostId { get; set; }
        public string? CostName { get; set; }
        public int? SupplierOrderId { get; set; }
        public string? SupplierOrderNr { get; set; }
    }
}
