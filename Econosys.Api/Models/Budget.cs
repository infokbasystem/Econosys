using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("Budget")]
    public class Budget
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(50)]
        public string? Name { get; set; }

        public int? Year { get; set; }

        public bool IsActive { get; set; }

        [Column(TypeName = "decimal(10,5)")]
        public decimal? DistributionMonth1 { get; set; }

        [Column(TypeName = "decimal(10,5)")]
        public decimal? DistributionMonth2 { get; set; }

        [Column(TypeName = "decimal(10,5)")]
        public decimal? DistributionMonth3 { get; set; }

        [Column(TypeName = "decimal(10,5)")]
        public decimal? DistributionMonth4 { get; set; }

        [Column(TypeName = "decimal(10,5)")]
        public decimal? DistributionMonth5 { get; set; }

        [Column(TypeName = "decimal(10,5)")]
        public decimal? DistributionMonth6 { get; set; }

        [Column(TypeName = "decimal(10,5)")]
        public decimal? DistributionMonth7 { get; set; }

        [Column(TypeName = "decimal(10,5)")]
        public decimal? DistributionMonth8 { get; set; }

        [Column(TypeName = "decimal(10,5)")]
        public decimal? DistributionMonth9 { get; set; }

        [Column(TypeName = "decimal(10,5)")]
        public decimal? DistributionMonth10 { get; set; }

        [Column(TypeName = "decimal(10,5)")]
        public decimal? DistributionMonth11 { get; set; }

        [Column(TypeName = "decimal(10,5)")]
        public decimal? DistributionMonth12 { get; set; }

        public bool IsLocked { get; set; }

        // DB column name is misspelled in the legacy schema.
        [Column("CompaceBudgetId")]
        public int? CompareBudgetId { get; set; }

        public int? ComparePrevYear { get; set; }

        public int? ComparePrevPrevYear { get; set; }

        public bool IsOwned { get; set; }

        [Column(TypeName = "smalldatetime")]
        public DateTime? OwnedDateTime { get; set; }

        [MaxLength(50)]
        public string? OwnedByUserName { get; set; }

        public int? OwnedByUserId { get; set; }

        public virtual LegacyUser? OwnedByUser { get; set; }
        public virtual ICollection<BudgetCustomer> BudgetCustomers { get; set; } = new List<BudgetCustomer>();
    }
}
