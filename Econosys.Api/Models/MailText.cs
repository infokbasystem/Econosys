using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("MailText")]
    public class MailText
    {
        [Key]
        [Column("lngMailText_ID")]
        public int Id { get; set; }

        [Column("CompanyId")]
        public int CompanyId { get; set; }

        [Column("strItem")]
        [MaxLength(50)]
        public string? Item { get; set; }

        [Column("strType")]
        [MaxLength(50)]
        public string? Type { get; set; }

        [Column("lngTranslationCode")]
        public int? TranslationCode { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }
    }
}
