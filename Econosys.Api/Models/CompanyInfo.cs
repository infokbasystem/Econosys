using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("CompanyInfo")]
    public class CompanyInfo
    {
        [Key]
        [Column("CompanyId")]
        public int CompanyId { get; set; }

        [Column("strCompanyName")]
        [MaxLength(50)]
        public string? CompanyName { get; set; }

        [Column("strAddress")]
        [MaxLength(100)]
        public string? Address { get; set; }

        [Column("strPostalAddress")]
        [MaxLength(100)]
        public string? PostalAddress { get; set; }

        [Column("strZipCode")]
        [MaxLength(50)]
        public string? ZipCode { get; set; }

        [Column("strTelephone1")]
        [MaxLength(50)]
        public string? Telephone1 { get; set; }

        [Column("strTelephone2")]
        [MaxLength(50)]
        public string? Telephone2 { get; set; }

        [Column("strFax1")]
        [MaxLength(50)]
        public string? Fax1 { get; set; }

        [Column("strFax2")]
        [MaxLength(50)]
        public string? Fax2 { get; set; }

        [Column("strBank")]
        [MaxLength(50)]
        public string? Bank { get; set; }

        [Column("strBIC")]
        [MaxLength(50)]
        public string? BIC { get; set; }

        [Column("strIBAN")]
        [MaxLength(50)]
        public string? IBAN { get; set; }

        [Column("strBG")]
        [MaxLength(50)]
        public string? BG { get; set; }

        [Column("strPG")]
        [MaxLength(50)]
        public string? PG { get; set; }

        [Column("strVATNr")]
        [MaxLength(100)]
        public string? VATNr { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }

        [Column("InvoiceMailSWE")]
        [MaxLength(200)]
        public string? InvoiceMailSWE { get; set; }

        [Column("InvoiceMailENG")]
        [MaxLength(200)]
        public string? InvoiceMailENG { get; set; }

        [Column("DocumentFileBasePath")]
        [MaxLength(1000)]
        public string? DocumentFileBasePath { get; set; }

        [Column("Email")]
        [MaxLength(200)]
        public string? Email { get; set; }

        [Column("Web")]
        [MaxLength(200)]
        public string? Web { get; set; }

        [Column("MailWrapper")]
        [MaxLength(5000)]
        public string? MailWrapper { get; set; }

        [Column("GoogleApiKey")]
        [MaxLength(500)]
        public string? GoogleApiKey { get; set; }

        public virtual ICollection<Setting> Settings { get; set; } = new List<Setting>();
    }
}