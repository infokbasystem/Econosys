using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("CalculationRowCost")]
    public class CalculationRowCost
    {
        [Key]
        public int Id { get; set; }

        public int? CalculationRowId { get; set; }

        public int? CostId { get; set; }

        [MaxLength(100)]
        public string? CostTypeText { get; set; }

        [Column(TypeName = "decimal(10, 5)")]
        public decimal? NrOf { get; set; }

        [Column(TypeName = "decimal(15, 5)")]
        public decimal? InPrice { get; set; }

        public int? InPriceCurrencyId { get; set; }

        [Column(TypeName = "decimal(15, 5)")]
        public decimal? OutPrice { get; set; }

        public bool DoDebit { get; set; }

        public CalculationRow? CalculationRow { get; set; }
        public Cost? Cost { get; set; }
        public Currency? InPriceCurrency { get; set; }
    }
}