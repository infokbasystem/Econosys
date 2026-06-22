using System.ComponentModel.DataAnnotations;

namespace Econosys.Api.DTOs
{
    public class VarnishDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int? TranslationCode { get; set; }
        public List<EntityTranslationDto> Translations { get; set; } = new();
        public bool Active { get; set; }
        public int? OldDbId { get; set; }
    }

    public class CreateVarnishRequest
    {
        [MaxLength(50)]
        public string? Name { get; set; }

        public int? TranslationCode { get; set; }
        public List<EntityTranslationRequest>? Translations { get; set; }
        public bool Active { get; set; }
        public int? OldDbId { get; set; }
    }

    public class UpdateVarnishRequest
    {
        [MaxLength(50)]
        public string? Name { get; set; }

        public int? TranslationCode { get; set; }
        public List<EntityTranslationRequest>? Translations { get; set; }
        public bool? Active { get; set; }
        public int? OldDbId { get; set; }
    }
}
