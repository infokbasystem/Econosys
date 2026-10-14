using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    /// <summary>Default in/out price for a cost, registered per customer or per supplier.</summary>
    [Table("CostPrice")]
    public class CostPrice
    {
        [Key]
        public int Id { get; set; }

        public int? CustomerId { get; set; }

        public int? SupplierId { get; set; }

        public int? CostId { get; set; }

        [Column(TypeName = "decimal(10, 2)")]
        public decimal? DefaultInPrice { get; set; }

        [Column(TypeName = "decimal(10, 2)")]
        public decimal? DefaultOutPrice { get; set; }

        public bool CalculationAutoAddSupplier { get; set; }
    }
}
