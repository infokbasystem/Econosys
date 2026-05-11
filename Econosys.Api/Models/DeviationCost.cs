using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("DeviationCost")]
    public class DeviationCost
    {
        [Key]
        public int Id { get; set; }

        public int? DeviationId { get; set; }

        [MaxLength(100)]
        public string? Text { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? CostSEK { get; set; }

        public virtual Deviation? Deviation { get; set; }
    }
}