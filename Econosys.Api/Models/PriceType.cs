using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("PriceType")]
    public class PriceType
    {
        [Key]
        [Column("lngPriceType_ID")]
        public int Id { get; set; }

        [Column("strPriceType")]
        [MaxLength(50)]
        public string? Name { get; set; }

        [Column("strDescription")]
        [MaxLength(50)]
        public string? Description { get; set; }

        [Column("lngAccount_ID")]
        public int? AccountId { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }

        [ForeignKey(nameof(AccountId))]
        public Account? Account { get; set; }

        public ICollection<InvoiceRow> InvoiceRows { get; set; } = new List<InvoiceRow>();
    }
}
