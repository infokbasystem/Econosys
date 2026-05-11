namespace Econosys.Api.DTOs
{
    public class MonthlyOrderCostsReportRequestDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public List<int>? CostIds { get; set; }
    }

    public class MonthlyOrderCostsReportResponseDto
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public IReadOnlyList<MonthlyOrderCostsReportRowDto> Rows { get; set; } = Array.Empty<MonthlyOrderCostsReportRowDto>();
    }

    public class MonthlyOrderCostsReportRowDto
    {
        public string Cost { get; set; } = string.Empty;
        public string Currency { get; set; } = string.Empty;
        public decimal PurchasePrice { get; set; }
        public decimal? PurchasePriceAttested { get; set; }
        public decimal? SalesPrice { get; set; }
        public bool ShouldInvoice { get; set; }
        public int? InvoiceNumber { get; set; }
        public string Supplier { get; set; } = string.Empty;
        public DateTime OrderCreatedAt { get; set; }
        public string Customer { get; set; } = string.Empty;
        public string Responsible { get; set; } = string.Empty;
        public int OrderNumber { get; set; }
        public string Product { get; set; } = string.Empty;
        public decimal? PurchasePriceSEK { get; set; }
        public decimal? PurchasePriceAttestedSEK { get; set; }
        public decimal? SalesPriceSEK { get; set; }
        public decimal? Markup { get; set; }
    }
}
