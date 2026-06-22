using System.ComponentModel.DataAnnotations;

namespace Econosys.Api.DTOs
{
    public class TermOfDeliveryDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int? TranslationCode { get; set; }
        public List<EntityTranslationDto> Translations { get; set; } = new();
        public bool Active { get; set; }
        public bool IsDefault { get; set; }
        public int? OldDbId { get; set; }
    }

    public class CreateTermOfDeliveryRequest
    {
        [MaxLength(255)]
        public string? Name { get; set; }

        public int? TranslationCode { get; set; }
        public List<EntityTranslationRequest>? Translations { get; set; }
        public bool Active { get; set; }
        public bool IsDefault { get; set; }
    }

    public class UpdateTermOfDeliveryRequest
    {
        [MaxLength(255)]
        public string? Name { get; set; }

        public int? TranslationCode { get; set; }
        public List<EntityTranslationRequest>? Translations { get; set; }
        public bool? Active { get; set; }
        public bool? IsDefault { get; set; }
    }
}
