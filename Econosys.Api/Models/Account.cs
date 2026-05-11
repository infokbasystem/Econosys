using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("Account")]
    public class Account
    {
        [Key]
        [Column("lngAccount_ID")]
        public int Id { get; set; }

        [Column("lngAccountType_ID")]
        public int AccountTypeId { get; set; }

        [Column("strAccount")]
        [MaxLength(255)]
        public string? Name { get; set; }

        [Column("lngAccountNr")]
        public int? AccountNr { get; set; }

        [Column("lngAccountNrEU")]
        public int? AccountNrEu { get; set; }

        [Column("lngAccountNrExport")]
        public int? AccountNrExport { get; set; }

        [Column("lngCostCenterNr")]
        public int? CostCenterNr { get; set; }

        [Column("bolProject")]
        public bool Project { get; set; }

        [Column("strInvoicePayMethod_ID")]
        [MaxLength(50)]
        public string? InvoicePayMethodId { get; set; }

        [Column("bolCombo")]
        public bool Combo { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }

        [ForeignKey(nameof(AccountTypeId))]
        public AccountType? AccountType { get; set; }

        public ICollection<PriceType> PriceTypes { get; set; } = new List<PriceType>();
    }
}
