using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("TermsOfDelivery")]
    public class TermOfDelivery
    {
        [Key]
        [Column("lngTermsOfDelivery_ID")]
        public int Id { get; set; }

        [Column("strTermsOfDelivery")]
        [MaxLength(255)]
        public string? Name { get; set; }

        [Column("lngTranslationCode")]
        public int? TranslationCode { get; set; }

        [Column("bolActive")]
        public bool Active { get; set; }

        [Column("bolDefault")]
        public bool IsDefault { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }
    }
}
