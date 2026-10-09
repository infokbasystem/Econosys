namespace Econosys.Api.DTOs
{
    using System.ComponentModel.DataAnnotations;

    public class StockTakingDto
    {
        public int Id { get; set; }
        public DateTime? StockTakingDate { get; set; }
        public int? OldDbId { get; set; }
    }

    public class SearchStockTakingsRequest
    {
        public FilterRequest? Filter { get; set; }
        public PaginationRequest? Pagination { get; set; }
        public List<SortRequest>? OrderBy { get; set; }
    }

    public class StockTakingHistoryDto
    {
        public int Id { get; set; }
        public DateTime? StockTakingDate { get; set; }
        public string InventoryNames { get; set; } = string.Empty;
        public int ItemCount { get; set; }
    }

    public class StockTakingAggregateDto
    {
        public int Id { get; set; }
        public DateTime? StockTakingDate { get; set; }
        public string InventoryNames { get; set; } = string.Empty;
        public List<StockTakingLineDto> Items { get; set; } = new();
    }

    public class StockTakingLineDto
    {
        public int Id { get; set; }
        public int SupplierOrderId { get; set; }
        public string SupplierOrderNr { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public int? Edition { get; set; }
        public string InventoryName { get; set; } = string.Empty;
        public int? CalculatedNrOfItems { get; set; }
        public int? CalculatedNrOfPallets { get; set; }
        public int? NrOfItems { get; set; }
        public int? NrOfPallets { get; set; }
        public int? DiffNrOfItems { get; set; }
        public int? DiffNrOfPallets { get; set; }
        public DateTime? LastStockTakingDate { get; set; }
    }

    public class CalculateStockTakingRequestDto
    {
        [Required]
        public DateTime? StockTakingDate { get; set; }

        [Range(0, int.MaxValue)]
        public int InventoryId { get; set; }
    }

    public class StockTakingDraftDto
    {
        public DateTime StockTakingDate { get; set; }
        public int InventoryId { get; set; }
        public List<StockTakingLineDto> Items { get; set; } = new();
    }

    public class CreateStockTakingRequestDto
    {
        [Required]
        public DateTime? StockTakingDate { get; set; }

        [Required]
        public List<StockTakingCountDto> Items { get; set; } = new();
    }

    public class StockTakingCountDto
    {
        [Range(1, int.MaxValue)]
        public int SupplierOrderId { get; set; }

        [Range(0, int.MaxValue)]
        public int? NrOfItems { get; set; }

        [Range(0, int.MaxValue)]
        public int? NrOfPallets { get; set; }
    }
}
