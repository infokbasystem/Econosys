using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("CustomerDeliveryAddress")]
    public class CustomerDeliveryAddress
    {
        [Key]
        [Column("lngCustomerDeliveryAddress_ID")]
        public int Id { get; set; }

        [Column("lngCustomer_ID")]
        public int? CustomerId { get; set; }

        [Column("strCustomerDeliveryAddress")]
        [MaxLength(50)]
        public string? Name { get; set; }

        [Column("strAddress")]
        [MaxLength(50)]
        public string? Address { get; set; }

        [Column("strPostalNr")]
        [MaxLength(50)]
        public string? PostalNr { get; set; }

        [Column("strPostalAddress")]
        [MaxLength(50)]
        public string? PostalAddress { get; set; }

        [Column("strCountry")]
        [MaxLength(50)]
        public string? Country { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public virtual Customer? Customer { get; set; }

        public virtual ICollection<CallOff> CallOffs { get; set; } = new List<CallOff>();
        public virtual ICollection<Quotation> Quotations { get; set; } = new List<Quotation>();
    }
}
