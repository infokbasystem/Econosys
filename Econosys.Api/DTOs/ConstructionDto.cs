using System.ComponentModel.DataAnnotations;

namespace Econosys.Api.DTOs
{
    public class ConstructionDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int? TranslationCode { get; set; }
        public List<EntityTranslationDto> Translations { get; set; } = new();
        public bool Active { get; set; }
        public int? OldDbId { get; set; }
        public bool? IsPackaging { get; set; }
        public List<ProductLookupDto> Products { get; set; } = new();
    }

    public class ProductLookupDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? ProductCode { get; set; }
    }

    public class CreateConstructionRequest
    {
        [MaxLength(50)]
        public string? Name { get; set; }

        public int? TranslationCode { get; set; }
        public List<EntityTranslationRequest>? Translations { get; set; }
        public bool Active { get; set; }
        public int? OldDbId { get; set; }
        public bool? IsPackaging { get; set; }
    }

    public class UpdateConstructionRequest
    {
        [MaxLength(50)]
        public string? Name { get; set; }

        public int? TranslationCode { get; set; }
        public List<EntityTranslationRequest>? Translations { get; set; }
        public bool? Active { get; set; }
        public int? OldDbId { get; set; }
        public bool? IsPackaging { get; set; }
    }

    public class SearchConstructionsRequest
    {
        public FilterRequest? Filter { get; set; }
        public PaginationRequest? Pagination { get; set; }
        public List<SortRequest>? OrderBy { get; set; }
    }
}
