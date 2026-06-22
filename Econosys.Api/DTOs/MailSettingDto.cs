using System.ComponentModel.DataAnnotations;

namespace Econosys.Api.DTOs
{
    public class MailSettingDto
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public string? Setting { get; set; }
        public string? Value { get; set; }
        public int? OldDbId { get; set; }
    }

    public class MailSettingsByCompanyDto
    {
        public int CompanyId { get; set; }
        public string? MailWrapper { get; set; }
        public List<MailSettingDto> Settings { get; set; } = new();
    }

    public class UpsertMailSettingRequest
    {
        public int? Id { get; set; }

        [MaxLength(50)]
        public string? Setting { get; set; }

        [MaxLength(255)]
        public string? Value { get; set; }
    }

    public class UpdateMailSettingsRequest
    {
        [MaxLength(5000)]
        public string? MailWrapper { get; set; }

        public List<UpsertMailSettingRequest> Settings { get; set; } = new();
    }
}
