namespace Econosys.Api.DTOs
{
    public class HandlingTimesDataReportRequestDto
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? SupplierId { get; set; }
        public PaginationRequest? Pagination { get; set; }
        public List<SortRequest>? OrderBy { get; set; }
    }

    public class HandlingTimesDataReportRowDto
    {
        public int SupplierOrderId { get; set; }
        public string SupplierOrderNr { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public DateTime? SupplierOrderCreatedDate { get; set; }
        public DateTime? SupplierOrderSentDateTime { get; set; }
        public int? CustomerOrderId { get; set; }
        public string? CustomerOrderNr { get; set; }
        public DateTime? CustomerOrderCreatedDate { get; set; }
        public DateTime? CustomerOrderSentDateTime { get; set; }
        public decimal? OverrideHandlingTimeDays { get; set; }
        public string? ForceHandlingTimeCountAs { get; set; }
        public decimal? HandlingTimeDays { get; set; }
    }

    public class UpdateSupplierOrderHandlingTimeOverrideRequest
    {
        public decimal? OverrideHandlingTimeDays { get; set; }
        public string? ForceHandlingTimeCountAs { get; set; }
    }
}
