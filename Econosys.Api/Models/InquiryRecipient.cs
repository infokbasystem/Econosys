using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("InquiryRecipient")]
    public class InquiryRecipient
    {
        [Key]
        [Column("lngInquiryRecipient_ID")]
        public int Id { get; set; }

        [Column("CompanyId")]
        public int CompanyId { get; set; }

        [Column("lngInquiry_ID")]
        public int? InquiryId { get; set; }

        [Column("lngSupplier_ID")]
        public int? SupplierId { get; set; }

        [Column("strYourRef")]
        [MaxLength(255)]
        public string? YourRef { get; set; }

        [Column("strTermsOfDelivery")]
        [MaxLength(255)]
        public string? TermsOfDelivery { get; set; }

        [Column("strTermsOfPayment")]
        [MaxLength(255)]
        public string? TermsOfPayment { get; set; }

        [Column("lngLanguage_ID")]
        public int? LanguageId { get; set; }

        [Column("strNote")]
        [MaxLength(255)]
        public string? Note { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }

        public virtual Inquiry? Inquiry { get; set; }
        public virtual Supplier? Supplier { get; set; }
        public virtual Language? Language { get; set; }
    }
}