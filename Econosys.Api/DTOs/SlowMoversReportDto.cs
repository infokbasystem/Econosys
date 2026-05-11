namespace Econosys.Api.DTOs
{
    public class SlowMoversReportResponseDto
    {
        public IReadOnlyList<SlowMoversReportRowDto> Rows { get; set; } = Array.Empty<SlowMoversReportRowDto>();
    }

    public class SlowMoversReportRowDto
    {
        public int InventoryId { get; set; }
        public string DeliveryNr { get; set; } = string.Empty;
        public string CustomerOrderNr { get; set; } = string.Empty;
        public string Customer { get; set; } = string.Empty;
        public string Product { get; set; } = string.Empty;
        public string InventoryName { get; set; } = string.Empty;
        public DateTime OrderedDeliveryTime { get; set; }
        public DateTime StorageDeadline { get; set; }
        public int DaysOverStorageDeadline { get; set; }
    }
}
