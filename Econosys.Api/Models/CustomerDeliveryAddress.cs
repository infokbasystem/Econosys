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
        [MaxLength(255)]
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

        [Column("strPalletRegistrationNr")]
        [MaxLength(50)]
        public string? PalletRegistrationNr { get; set; }

        [Column("bolDefault")]
        public bool IsDefault { get; set; }

        [Column("Address2")]
        [MaxLength(255)]
        public string? Address2 { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }

        [Column("PostalNr")]
        public int? PostalNrValue { get; set; }

        [Column("CountryCode")]
        [MaxLength(10)]
        public string? CountryCode { get; set; }

        [Column("LogisticsInfoInternal")]
        [MaxLength(255)]
        public string? LogisticsInfoInternal { get; set; }

        [Column("InventoryId")]
        public int? InventoryId { get; set; }

        [Column("NextTransportDeliveryAddressId")]
        public int? NextTransportDeliveryAddressId { get; set; }

        [Column("PositionId")]
        public int? PositionId { get; set; }

        [Column("AdressExtra")]
        [MaxLength(50)]
        public string? AddressExtra { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public virtual Customer? Customer { get; set; }

        public virtual ICollection<CallOff> CallOffs { get; set; } = new List<CallOff>();
        public virtual ICollection<Quotation> Quotations { get; set; } = new List<Quotation>();
    }
}
