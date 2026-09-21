namespace Econosys.Api.DTOs
{
    public class InvoicedArticlesReportRequestDto
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class InvoicedArticlesReportResponseDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public IReadOnlyList<InvoicedArticlesReportRowDto> Rows { get; set; } = Array.Empty<InvoicedArticlesReportRowDto>();
    }

    public class InvoicedArticlesReportRowDto
    {
        public DateTime InvoiceDate { get; set; }
        public int? InvoiceNumber { get; set; }
        public string Customer { get; set; } = string.Empty;
        public string ProductCode { get; set; } = string.Empty;
        public string Product { get; set; } = string.Empty;
        public double Quantity { get; set; }
        public double UnitPrice { get; set; }
        public string Unit { get; set; } = string.Empty;
        public double Sum { get; set; }
    }
}