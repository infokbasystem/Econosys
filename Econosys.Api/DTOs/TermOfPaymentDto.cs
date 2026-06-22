using System.ComponentModel.DataAnnotations;

namespace Econosys.Api.DTOs
{
    public class TermOfPaymentDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public int? TranslationCode { get; set; }
        public List<EntityTranslationDto> Translations { get; set; } = new();
        public bool Active { get; set; }
        public bool IsDefault { get; set; }
        public int? TypeOf { get; set; }
        public int? PaymentDays { get; set; }
        public int? OldDbId { get; set; }
    }

    public class CreateTermOfPaymentRequest
    {
        [MaxLength(255)]
        public string? Name { get; set; }

        public int? TranslationCode { get; set; }
        public List<EntityTranslationRequest>? Translations { get; set; }
        public bool Active { get; set; }
        public bool IsDefault { get; set; }
        public int? TypeOf { get; set; }
        public int? PaymentDays { get; set; }
    }

    public class UpdateTermOfPaymentRequest
    {
        [MaxLength(255)]
        public string? Name { get; set; }

        public int? TranslationCode { get; set; }
        public List<EntityTranslationRequest>? Translations { get; set; }
        public bool? Active { get; set; }
        public bool? IsDefault { get; set; }
        public int? TypeOf { get; set; }
        public int? PaymentDays { get; set; }
    }
}