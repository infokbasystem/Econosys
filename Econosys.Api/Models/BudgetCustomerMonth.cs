using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("BudgetCustomerMonth")]
    public class BudgetCustomerMonth
    {
        [Key]
        public int Id { get; set; }

        public int? BudgetCustomerId { get; set; }

        [Column(TypeName = "decimal(18,5)")]
        public decimal? SalesMonth1 { get; set; }

        [Column(TypeName = "decimal(18,5)")]
        public decimal? SalesMonth2 { get; set; }

        [Column(TypeName = "decimal(18,5)")]
        public decimal? SalesMonth3 { get; set; }

        [Column(TypeName = "decimal(18,5)")]
        public decimal? SalesMonth4 { get; set; }

        [Column(TypeName = "decimal(18,5)")]
        public decimal? SalesMonth5 { get; set; }

        [Column(TypeName = "decimal(18,5)")]
        public decimal? SalesMonth6 { get; set; }

        [Column(TypeName = "decimal(18,5)")]
        public decimal? SalesMonth7 { get; set; }

        [Column(TypeName = "decimal(18,5)")]
        public decimal? SalesMonth8 { get; set; }

        [Column(TypeName = "decimal(18,5)")]
        public decimal? SalesMonth9 { get; set; }

        [Column(TypeName = "decimal(18,5)")]
        public decimal? SalesMonth10 { get; set; }

        [Column(TypeName = "decimal(18,5)")]
        public decimal? SalesMonth11 { get; set; }

        [Column(TypeName = "decimal(18,5)")]
        public decimal? SalesMonth12 { get; set; }

        [Column(TypeName = "decimal(10,5)")]
        public decimal? AdditionMonth1 { get; set; }

        [Column(TypeName = "decimal(10,5)")]
        public decimal? AdditionMonth2 { get; set; }

        [Column(TypeName = "decimal(10,5)")]
        public decimal? AdditionMonth3 { get; set; }

        [Column(TypeName = "decimal(10,5)")]
        public decimal? AdditionMonth4 { get; set; }

        [Column(TypeName = "decimal(10,5)")]
        public decimal? AdditionMonth5 { get; set; }

        [Column(TypeName = "decimal(10,5)")]
        public decimal? AdditionMonth6 { get; set; }

        [Column(TypeName = "decimal(10,5)")]
        public decimal? AdditionMonth7 { get; set; }

        [Column(TypeName = "decimal(10,5)")]
        public decimal? AdditionMonth8 { get; set; }

        [Column(TypeName = "decimal(10,5)")]
        public decimal? AdditionMonth9 { get; set; }

        [Column(TypeName = "decimal(10,5)")]
        public decimal? AdditionMonth10 { get; set; }

        [Column(TypeName = "decimal(10,5)")]
        public decimal? AdditionMonth11 { get; set; }

        [Column(TypeName = "decimal(10,5)")]
        public decimal? AdditionMonth12 { get; set; }

        public virtual BudgetCustomer? BudgetCustomer { get; set; }
    }
}
