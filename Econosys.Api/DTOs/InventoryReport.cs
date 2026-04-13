namespace Econosys.Api.DTOs
{
    public class InventoryReportRequestDto
    {
        public List<int>? InventoryIds { get; set; }
        public DateTime? CalculationDate { get; set; }
    }

    public class InventoryReportResponseDto
    {
        public DateTime CalculationDate { get; set; }
        public decimal CurrentInventoryValue { get; set; }
        public IReadOnlyList<InventoryReportRowDto> Rows { get; set; } = Array.Empty<InventoryReportRowDto>();
    }

    public class InventoryReportRowDto
    {
        public int SupplierOrderId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int InventoryId { get; set; }
        public string InventoryName { get; set; } = string.Empty;
        public double NrOfItems { get; set; }
        public DateTime? LastInventoryDate { get; set; }
        public double CurrentInventoryLevel { get; set; }
        public int CurrentNrOfPallets { get; set; }
        public decimal TotalSalesValue { get; set; }
    }
}