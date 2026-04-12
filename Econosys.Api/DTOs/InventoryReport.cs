using System.ComponentModel.DataAnnotations;

namespace Econosys.Api.DTOs
{

    public class InventoryReportRequest 
    {
        public FilterRequest? Filter { get; set; }
        public PaginationRequest? Pagination { get; set; }
        public List<SortRequest>? OrderBy { get; set; }
    }       

    public class InventoryReportResponse
    {
        public DateTime CalcDate { get; set; } = DateTime.UtcNow;
        public string WarehouseName { get; set; } = string.Empty;
        public int TotalRows { get; set; }
        public decimal? TotalPurchaseValue { get; set; }
        public int? TotalQuantity { get; set; }
        public PagedResultDto<InventoryReportRow> PagedRows { get; set; } = new();
    }

    public class InventoryReportRow
    {
        public string WarehouseName { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int? Quantity { get; set; }
        public decimal? PurchaseValue { get; set; }  
        public decimal? Pallets { get; set; }
        public DateTime? LastInventoried { get; set; }
    }
}