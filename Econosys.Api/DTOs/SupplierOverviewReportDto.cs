namespace Econosys.Api.DTOs
{
    public class SupplierOverviewReportResponseDto
    {
        public DateTime ReportDate { get; set; }
        public IReadOnlyList<SupplierOverviewReportRowDto> Rows { get; set; } = Array.Empty<SupplierOverviewReportRowDto>();
    }

    public class SupplierOverviewReportRowDto
    {
        public int SupplierId { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public SupplierOverviewMetricsDto CurrentYear { get; set; } = new();
        public SupplierOverviewComparisonDto CurrentVsPrevious { get; set; } = new();
        public SupplierOverviewMetricsDto PreviousYtd { get; set; } = new();
        public SupplierOverviewMetricsDto PreviousYear { get; set; } = new();
    }

    public class SupplierOverviewMetricsDto
    {
        public decimal PurchaseValue { get; set; }
        public decimal SalesValue { get; set; }
        public decimal Freight { get; set; }
        public int OrderCount { get; set; }
        public decimal Addition { get; set; }
        public decimal Tb { get; set; }
    }

    public class SupplierOverviewComparisonDto
    {
        public decimal PurchaseValue { get; set; }
        public decimal SalesValue { get; set; }
        public int OrderCount { get; set; }
    }
}