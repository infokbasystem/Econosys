using System.ComponentModel.DataAnnotations;

namespace Econosys.Api.DTOs
{
    public class EntityTranslationDto
    {
        public string LangCode { get; set; } = string.Empty;
        public string? Translation { get; set; }
        public string? TranslationHtml { get; set; }
    }

    public class EntityTranslationRequest
    {
        [MaxLength(50)]
        public string? LangCode { get; set; }

        [MaxLength(255)]
        public string? Translation { get; set; }

        [MaxLength(8000)]
        public string? TranslationHtml { get; set; }
    }
}