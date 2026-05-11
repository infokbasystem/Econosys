namespace Econosys.Api.DTOs
{
    public class NonDeliveredWarehouseOrdersReportRequestDto
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public PaginationRequest? Pagination { get; set; }
        public List<SortRequest>? OrderBy { get; set; }
    }

    public class NonDeliveredWarehouseOrdersReportRowDto
    {
        public int Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public DateTime? SupplierOrderCreatedAt { get; set; }
        public DateTime? SupplierOrderDeliveryDate { get; set; }
        public bool SupplierOrderDeliveryDateWeekMode { get; set; }
        public int Edition { get; set; }
    }
}