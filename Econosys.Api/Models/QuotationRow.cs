using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("QuotationRow")]
    public class QuotationRow
    {
        [Key]
        [Column("lngQuotationRow_ID")]
        public int Id { get; set; }

        [Column("CompanyId")]
        public int CompanyId { get; set; }

        [Column("lngCalculationRow_ID")]
        public int? CalculationRowId { get; set; }

        [Column("lngQuotation_ID")]
        public int? QuotationId { get; set; }

        [Column("lngEdition")]
        public int? Edition { get; set; }

        [Column("dblPrice")]
        public double? Price { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }

        [ForeignKey(nameof(CalculationRowId))]
        public CalculationRow? CalculationRow { get; set; }

        [ForeignKey(nameof(QuotationId))]
        public Quotation? Quotation { get; set; }
    }
}