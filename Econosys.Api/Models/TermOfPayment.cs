using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("TermsOfPayment")]
    public class TermOfPayment
    {
        [Key]
        [Column("lngTermsOfPayment_ID")]
        public int Id { get; set; }

        [Column("strTermsOfPayment")]
        [MaxLength(255)]
        public string? Name { get; set; }

        [Column("lngTranslationCode")]
        public int? TranslationCode { get; set; }

        [Column("bolActive")]
        public bool Active { get; set; }

        [Column("bolDefault")]
        public bool IsDefault { get; set; }

        [Column("intTypeOf")]
        public int? TypeOf { get; set; }

        [Column("intPaymentDays")]
        public int? PaymentDays { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }
    }
}