using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("User")]
    public class LegacyUser
    {
        [Key]
        [Column("lngUser_ID")]
        public int Id { get; set; }

        [Column("strUser")]
        [MaxLength(100)]
        public string? Name { get; set; }

        public virtual ICollection<Customer> ResponsibleForCustomers { get; set; } = new List<Customer>();
        public virtual ICollection<Customer> SupportingCustomers { get; set; } = new List<Customer>();
        public virtual ICollection<CustomerOrder> ResponsibleForCustomerOrders { get; set; } = new List<CustomerOrder>();
        public virtual ICollection<Deviation> CreatedDeviations { get; set; } = new List<Deviation>();
        public virtual ICollection<Deviation> EditedDeviations { get; set; } = new List<Deviation>();
        public virtual ICollection<Deviation> ResponsibleForDeviations { get; set; } = new List<Deviation>();
        public virtual ICollection<DocumentFile> CreatedDocumentFiles { get; set; } = new List<DocumentFile>();
    }
}
