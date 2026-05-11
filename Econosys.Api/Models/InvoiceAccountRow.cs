using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("InvoiceAccountRow")]
    public class InvoiceAccountRow
    {
        [Key]
        [Column("lngInvoiceAccountRow_ID")]
        public int Id { get; set; }

        [Column("lngInvoice_ID")]
        public int? InvoiceId { get; set; }

        [Column("strText")]
        [MaxLength(255)]
        public string? Text { get; set; }

        [Column("dblNrOf")]
        public double? NrOf { get; set; }

        [Column("dblUnitPrice")]
        public double? UnitPrice { get; set; }

        [Column("dblSum")]
        public double? Sum { get; set; }

        [Column("strAccountNr")]
        [MaxLength(50)]
        public string? AccountNr { get; set; }

        [Column("strCostCenter")]
        [MaxLength(50)]
        public string? CostCenter { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }

        [ForeignKey(nameof(InvoiceId))]
        public Invoice? Invoice { get; set; }
    }
}