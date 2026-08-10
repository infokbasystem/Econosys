using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("Inventory")]
    public class Inventory
    {
        [Key]
        [Column("lngInventoryId")]
        public int Id { get; set; }

        [Column("strName")]
        [MaxLength(100)]
        public string? Name { get; set; }

        [Column("lngAccountNr")]
        public int? AccountNr { get; set; }

        public int? OldDbId { get; set; }

        public bool IsInventory { get; set; }

        [MaxLength(50)]
        public string? Email { get; set; }

        public bool IsOmlast { get; set; }

        [MaxLength(50)]
        public string? Address { get; set; }

        public int? PostalNr { get; set; }

        [MaxLength(50)]
        public string? PostalAddress { get; set; }

        [MaxLength(10)]
        public string? CountryCode { get; set; }

        [MaxLength(50)]
        public string? Country { get; set; }

        [MaxLength(50)]
        public string? PostalNrText { get; set; }

        public bool NoInventoryValue { get; set; }

        public int? PositionId { get; set; }

        [Column("AdressExtra")]
        [MaxLength(50)]
        public string? AddressExtra { get; set; }

        public virtual ICollection<SupplierOrder> SupplierOrders { get; set; } = new List<SupplierOrder>();
        public virtual ICollection<SupplierFactory> SupplierFactories { get; set; } = new List<SupplierFactory>();
    }
}
