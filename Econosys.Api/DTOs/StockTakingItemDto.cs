namespace Econosys.Api.DTOs
{
    public class StockTakingItemDto
    {
        public int Id { get; set; }
        public int? StockTakingId { get; set; }
        public int? SupplierOrderId { get; set; }
        public int? NrOfItems { get; set; }
        public int? NrOfPallets { get; set; }
        public int? DiffNrOfItems { get; set; }
        public int? DiffNrOfPallets { get; set; }
        public int? OldDbId { get; set; }
    }

    public class SearchStockTakingItemsRequest
    {
        public FilterRequest? Filter { get; set; }
        public PaginationRequest? Pagination { get; set; }
        public List<SortRequest>? OrderBy { get; set; }
    }
}