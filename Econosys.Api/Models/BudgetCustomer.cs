using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("BudgetCustomer")]
    public class BudgetCustomer
    {
        [Key]
        public int Id { get; set; }

        public int? BudgetId { get; set; }

        public int? CustomerId { get; set; }

        [Column(TypeName = "decimal(18,0)")]
        public decimal? TotalSalesBudget { get; set; }

        [Column(TypeName = "decimal(10,5)")]
        public decimal? AdditionBudget { get; set; }

        public int? EmployeeId { get; set; }

        public bool IsRemovedFromBudget { get; set; }

        public virtual Budget? Budget { get; set; }
        public virtual Customer? Customer { get; set; }
        public virtual LegacyUser? Employee { get; set; }
        public virtual ICollection<BudgetCustomerMonth> BudgetCustomerMonths { get; set; } = new List<BudgetCustomerMonth>();
    }
}
