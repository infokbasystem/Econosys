using System.ComponentModel.DataAnnotations;

namespace Econosys.Api.DTOs
{
    public class TranslationItemDto
    {
        public int Id { get; set; }
        public int? TranslationCode { get; set; }
        public string? LangCode { get; set; }
        public string? Translation { get; set; }
        public int? OldDbId { get; set; }
        public string? TranslationHtml { get; set; }
    }

    public class CreateTranslationItemRequest
    {
        public int? TranslationCode { get; set; }

        [MaxLength(50)]
        public string? LangCode { get; set; }

        [MaxLength(255)]
        public string? Translation { get; set; }

        public int? OldDbId { get; set; }

        [MaxLength(8000)]
        public string? TranslationHtml { get; set; }
    }

    public class UpdateTranslationItemRequest
    {
        public int? TranslationCode { get; set; }

        [MaxLength(50)]
        public string? LangCode { get; set; }

        [MaxLength(255)]
        public string? Translation { get; set; }

        public int? OldDbId { get; set; }

        [MaxLength(8000)]
        public string? TranslationHtml { get; set; }
    }

    public class SearchTranslationItemsRequest
    {
        public FilterRequest? Filter { get; set; }
        public PaginationRequest? Pagination { get; set; }
        public List<SortRequest>? OrderBy { get; set; }
    }

    public class BatchUpsertTranslationItemsRequest
    {
        public List<BatchUpsertTranslationItemRequestItem> Items { get; set; } = new();
    }

    public class BatchUpsertTranslationItemRequestItem
    {
        public int? Id { get; set; }
        public int? TranslationCode { get; set; }

        [MaxLength(50)]
        public string? LangCode { get; set; }

        [MaxLength(255)]
        public string? Translation { get; set; }

        [MaxLength(8000)]
        public string? TranslationHtml { get; set; }
    }
}