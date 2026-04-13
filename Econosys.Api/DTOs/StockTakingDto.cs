namespace Econosys.Api.DTOs
{
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
}
