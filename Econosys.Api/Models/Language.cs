using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("Lang")]
    public class Language
    {
        [Key]
        [Column("lngLanguage_ID")]
        public int Id { get; set; }

        [Column("CompanyId")]
        public int CompanyId { get; set; }

        [Column("strLanguage")]
        [MaxLength(50)]
        public string? Name { get; set; }

        [Column("strLangCode")]
        [MaxLength(50)]
        public string? LanguageCode { get; set; }

        [Column("intColumn")]
        public short? Column { get; set; }

        [Column("bolDefault")]
        public bool IsDefault { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }

        public virtual ICollection<Customer> Customers { get; set; } = new List<Customer>();
        public virtual ICollection<Supplier> Suppliers { get; set; } = new List<Supplier>();
        public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    }
}
