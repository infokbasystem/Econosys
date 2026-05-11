namespace Econosys.Api.DTOs
{
    public class NonInvoicedOrderReportRequestDto
    {
        public int? SellerId { get; set; }
        public string? OurReferenceId { get; set; }
        public int? CustomerId { get; set; }
        public int? SupplierId { get; set; }
        public PaginationRequest? Pagination { get; set; }
        public List<SortRequest>? OrderBy { get; set; }
    }

    public class NonInvoicedOrderReportRowDto
    {
        public int Id { get; set; }
        public string CustomerOrderNr { get; set; } = string.Empty;
        public string Seller { get; set; } = string.Empty;
        public string OurReference { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public DateTime? CreatedAt { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public bool DeliveryDateWeekMode { get; set; }
        public int Edition { get; set; }
        public int EditionLeft { get; set; }
        public decimal OrderValueSek { get; set; }
        public decimal OrderValueLeftSek { get; set; }
        public decimal? MarkupPercent { get; set; }
        public decimal TotalTbSek { get; set; }
        public decimal TotalTbLeftSek { get; set; }
        public bool IsInventory { get; set; }
    }

    public class NonInvoicedOrderFilterOptionsDto
    {
        public IReadOnlyList<FilterOptionDto<int>> Sellers { get; set; } = Array.Empty<FilterOptionDto<int>>();
        public IReadOnlyList<FilterOptionDto<string>> OurReferences { get; set; } = Array.Empty<FilterOptionDto<string>>();
        public IReadOnlyList<FilterOptionDto<int>> Customers { get; set; } = Array.Empty<FilterOptionDto<int>>();
        public IReadOnlyList<FilterOptionDto<int>> Suppliers { get; set; } = Array.Empty<FilterOptionDto<int>>();
    }

    public class NonInvoicedOrderReportTotalsDto
    {
        public decimal OrderValueSek { get; set; }
        public decimal OrderValueLeftSek { get; set; }
        public decimal TotalTbSek { get; set; }
        public decimal TotalTbLeftSek { get; set; }
    }
}