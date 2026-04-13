using System.ComponentModel.DataAnnotations;

namespace Econosys.Api.DTOs
{
    public class CurrencyDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int? TranslationCode { get; set; }
        public bool IsDefault { get; set; }
        public double? RateToSek { get; set; }
        public bool Active { get; set; }
        public double? RateStockValue { get; set; }
        public int? OldDbId { get; set; }
        public string? SpcsKey { get; set; }
        public int? WarningTolerancePercent { get; set; }
    }

    public class CreateCurrencyRequest
    {
        [MaxLength(50)]
        public string? Name { get; set; }

        public int? TranslationCode { get; set; }
        public bool IsDefault { get; set; }
        public double? RateToSek { get; set; }
        public bool Active { get; set; }
        public double? RateStockValue { get; set; }
        public int? OldDbId { get; set; }

        [MaxLength(4)]
        public string? SpcsKey { get; set; }

        public int? WarningTolerancePercent { get; set; }
    }

    public class UpdateCurrencyRequest
    {
        [MaxLength(50)]
        public string? Name { get; set; }

        public int? TranslationCode { get; set; }
        public bool? IsDefault { get; set; }
        public double? RateToSek { get; set; }
        public bool? Active { get; set; }
        public double? RateStockValue { get; set; }
        public int? OldDbId { get; set; }

        [MaxLength(4)]
        public string? SpcsKey { get; set; }

        public int? WarningTolerancePercent { get; set; }
    }

    public class SearchCurrenciesRequest
    {
        public FilterRequest? Filter { get; set; }
        public PaginationRequest? Pagination { get; set; }
        public List<SortRequest>? OrderBy { get; set; }
    }
}