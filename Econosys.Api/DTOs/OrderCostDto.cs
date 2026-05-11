namespace Econosys.Api.DTOs
{
    public class OrderCostDto
    {
        public int Id { get; set; }
        public int? QuotationId { get; set; }
        public int? QuotationRowId { get; set; }
        public int? CalculationRowId { get; set; }
        public int? CustomerOrderId { get; set; }
        public int? SupplierOrderId { get; set; }
        public int? CostId { get; set; }
        public decimal? NrOf { get; set; }
        public decimal? InPrice { get; set; }
        public int? InPriceCurrencyId { get; set; }
        public decimal? OutPrice { get; set; }
        public bool DoDebit { get; set; }
        public string? Note { get; set; }
        public int? SupplierId { get; set; }
        public bool DoPrintOnCustomerOrder { get; set; }
        public bool DoPrintOnSupplierOrder { get; set; }
        public bool DoPrintOnQuotation { get; set; }
        public string? SupplierName { get; set; }
        public decimal? InPriceAttested { get; set; }
        public int? CreatedById { get; set; }
        public DateTime? CreatedDateTime { get; set; }
        public int? EditedById { get; set; }
        public DateTime? EditedDateTime { get; set; }
        public decimal? AttestedInPriceCurrencyRate { get; set; }
        public decimal? AttestedOutPriceCurrencyRate { get; set; }
        public string? AtttestedBySignature { get; set; }
        public DateTime? AttestedDateTime { get; set; }
        public bool DoInvoiceSeparately { get; set; }
        public bool DoInvoiceSeparatelyImmediately { get; set; }
        public bool IsCostInvoicedSeparately { get; set; }
        public decimal? InPriceSEK { get; set; }
        public decimal? InPriceAttestedSEK { get; set; }
        public decimal? OutPriceSEK { get; set; }
        public decimal? Markup { get; set; }
    }

    public class CreateOrderCostRequest
    {
        public int? QuotationId { get; set; }
        public int? QuotationRowId { get; set; }
        public int? CalculationRowId { get; set; }
        public int? CustomerOrderId { get; set; }
        public int? SupplierOrderId { get; set; }
        public int? CostId { get; set; }
        public decimal? NrOf { get; set; }
        public decimal? InPrice { get; set; }
        public int? InPriceCurrencyId { get; set; }
        public decimal? OutPrice { get; set; }
        public bool DoDebit { get; set; }
        public string? Note { get; set; }
        public int? SupplierId { get; set; }
        public bool DoPrintOnCustomerOrder { get; set; }
        public bool DoPrintOnSupplierOrder { get; set; }
        public bool DoPrintOnQuotation { get; set; }
        public string? SupplierName { get; set; }
        public decimal? InPriceAttested { get; set; }
        public int? CreatedById { get; set; }
        public DateTime? CreatedDateTime { get; set; }
        public int? EditedById { get; set; }
        public DateTime? EditedDateTime { get; set; }
        public decimal? AttestedInPriceCurrencyRate { get; set; }
        public decimal? AttestedOutPriceCurrencyRate { get; set; }
        public string? AtttestedBySignature { get; set; }
        public DateTime? AttestedDateTime { get; set; }
        public bool DoInvoiceSeparately { get; set; }
        public bool DoInvoiceSeparatelyImmediately { get; set; }
        public bool IsCostInvoicedSeparately { get; set; }
    }

    public class UpdateOrderCostRequest
    {
        public int? QuotationId { get; set; }
        public int? QuotationRowId { get; set; }
        public int? CalculationRowId { get; set; }
        public int? CustomerOrderId { get; set; }
        public int? SupplierOrderId { get; set; }
        public int? CostId { get; set; }
        public decimal? NrOf { get; set; }
        public decimal? InPrice { get; set; }
        public int? InPriceCurrencyId { get; set; }
        public decimal? OutPrice { get; set; }
        public bool? DoDebit { get; set; }
        public string? Note { get; set; }
        public int? SupplierId { get; set; }
        public bool? DoPrintOnCustomerOrder { get; set; }
        public bool? DoPrintOnSupplierOrder { get; set; }
        public bool? DoPrintOnQuotation { get; set; }
        public string? SupplierName { get; set; }
        public decimal? InPriceAttested { get; set; }
        public int? CreatedById { get; set; }
        public DateTime? CreatedDateTime { get; set; }
        public int? EditedById { get; set; }
        public DateTime? EditedDateTime { get; set; }
        public decimal? AttestedInPriceCurrencyRate { get; set; }
        public decimal? AttestedOutPriceCurrencyRate { get; set; }
        public string? AtttestedBySignature { get; set; }
        public DateTime? AttestedDateTime { get; set; }
        public bool? DoInvoiceSeparately { get; set; }
        public bool? DoInvoiceSeparatelyImmediately { get; set; }
        public bool? IsCostInvoicedSeparately { get; set; }
    }

    public class SearchOrderCostsRequest
    {
        public FilterRequest? Filter { get; set; }
        public PaginationRequest? Pagination { get; set; }
        public List<SortRequest>? OrderBy { get; set; }
    }
}
