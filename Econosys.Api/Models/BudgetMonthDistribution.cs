using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("BudgetMonthDistribution")]
    public class BudgetMonthDistribution
    {
        [Key]
        public int Id { get; set; }

        [Column(TypeName = "decimal(10,8)")]
        public decimal? Month1 { get; set; }

        [Column(TypeName = "decimal(10,8)")]
        public decimal? Month2 { get; set; }

        [Column(TypeName = "decimal(10,8)")]
        public decimal? Month3 { get; set; }

        [Column(TypeName = "decimal(10,8)")]
        public decimal? Month4 { get; set; }

        [Column(TypeName = "decimal(10,8)")]
        public decimal? Month5 { get; set; }

        [Column(TypeName = "decimal(10,8)")]
        public decimal? Month6 { get; set; }

        [Column(TypeName = "decimal(10,8)")]
        public decimal? Month7 { get; set; }

        [Column(TypeName = "decimal(10,8)")]
        public decimal? Month8 { get; set; }

        [Column(TypeName = "decimal(10,8)")]
        public decimal? Month9 { get; set; }

        [Column(TypeName = "decimal(10,8)")]
        public decimal? Month10 { get; set; }

        [Column(TypeName = "decimal(10,8)")]
        public decimal? Month11 { get; set; }

        [Column(TypeName = "decimal(10,8)")]
        public decimal? Month12 { get; set; }
    }
}