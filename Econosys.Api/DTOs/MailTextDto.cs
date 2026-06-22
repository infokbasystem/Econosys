using System.ComponentModel.DataAnnotations;

namespace Econosys.Api.DTOs
{
    public class MailTextDto
    {
        public int Id { get; set; }
        public int? SubjectId { get; set; }
        public int? BodyId { get; set; }
        public int CompanyId { get; set; }
        public string Item { get; set; } = string.Empty;
        public string SubjectType { get; set; } = string.Empty;
        public string BodyType { get; set; } = string.Empty;
        public int? SubjectTranslationCode { get; set; }
        public int? BodyTranslationCode { get; set; }
        public List<EntityTranslationDto> SubjectTranslations { get; set; } = new();
        public List<EntityTranslationDto> BodyTranslations { get; set; } = new();
    }

    public class CreateMailTextRequest
    {
        [MaxLength(50)]
        public string? Item { get; set; }

        public int? CompanyId { get; set; }
        public List<EntityTranslationRequest>? SubjectTranslations { get; set; }
        public List<EntityTranslationRequest>? BodyTranslations { get; set; }
    }

    public class UpdateMailTextRequest
    {
        [MaxLength(50)]
        public string? Item { get; set; }

        public List<EntityTranslationRequest>? SubjectTranslations { get; set; }
        public List<EntityTranslationRequest>? BodyTranslations { get; set; }
    }
}
