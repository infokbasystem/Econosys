namespace Econosys.Api.DTOs
{
    public class RevenuePerOrderReportRequestDto
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? SellerId { get; set; }
        public string? OrderNumber { get; set; }
        public string? ProductName { get; set; }
        public string? SupplierName { get; set; }
        public string? CustomerName { get; set; }
        public bool IncludeInactiveOrders { get; set; }
        public PaginationRequest? Pagination { get; set; }
        public List<SortRequest>? OrderBy { get; set; }
    }

    public class RevenuePerOrderReportRowDto
    {
        public int OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public DateTime? CreatedAt { get; set; }
        public int? SellerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string Construction { get; set; } = string.Empty;
        public string MaterialType { get; set; } = string.Empty;
        public string Seller { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public decimal RevenueSek { get; set; }
        public decimal TotalTbSek { get; set; }
        public decimal? MarkupPercent { get; set; }
        public bool IsActive { get; set; }
    }

    public class RevenuePerOrderFilterOptionsDto
    {
        public IReadOnlyList<FilterOptionDto<int>> Sellers { get; set; } = Array.Empty<FilterOptionDto<int>>();
    }
}
