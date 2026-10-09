using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("CustomerEmployeeAllocation")]
    public class CustomerEmployeeAllocation
    {
        [Key]
        public int Id { get; set; }

        public int? CustomerId { get; set; }

        public int? EmployeeId { get; set; }

        public DateTime? AllocateFromDate { get; set; }

        public DateTime? AllocatedDoneDate { get; set; }
    }
}
