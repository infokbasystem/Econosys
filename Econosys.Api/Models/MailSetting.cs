using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("MailSetting")]
    public class MailSetting
    {
        [Key]
        [Column("lngMailSetting_ID")]
        public int Id { get; set; }

        [Column("CompanyId")]
        public int CompanyId { get; set; }

        [Column("strSetting")]
        [MaxLength(50)]
        public string? Setting { get; set; }

        [Column("strValue")]
        [MaxLength(255)]
        public string? Value { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }

        [Column("MailWrapper", TypeName = "varchar(5000)")]
        [MaxLength(5000)]
        public string? MailWrapper { get; set; }
    }
}
