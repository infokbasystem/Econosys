using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("CustomerOrderEditionAdjustment")]
    public class CustomerOrderEditionAdjustment
    {
        [Key]
        public int Id { get; set; }

        public int? CustomerOrderId { get; set; }

        public int? NrOfItems { get; set; }

        [Column(TypeName = "datetime2(7)")]
        public DateTime? TimeStamp { get; set; }

        public CustomerOrder? CustomerOrder { get; set; }
    }
}