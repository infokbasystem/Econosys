using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("SupplierFactory")]
    public class SupplierFactory
    {
        [Key]
        [Column("Id")]
        public int Id { get; set; }

        [Column("SupplierId")]
        public int? SupplierId { get; set; }

        [Column("Name")]
        [MaxLength(100)]
        public string? Name { get; set; }

        [Column("Address")]
        [MaxLength(200)]
        public string? Address { get; set; }

        [Column("City")]
        [MaxLength(100)]
        public string? City { get; set; }

        [Column("Country")]
        [MaxLength(100)]
        public string? Country { get; set; }

        [Column("IsDefault")]
        public bool IsDefault { get; set; }

        [Column("PostalNr")]
        [MaxLength(50)]
        public string? PostalNr { get; set; }

        [Column("CountryCode")]
        [MaxLength(50)]
        public string? CountryCode { get; set; }

        [Column("PositionId")]
        public int? PositionId { get; set; }

        [Column("AdressExtra")]
        [MaxLength(50)]
        public string? AddressExtra { get; set; }

        [Column("ViaInventoryId")]
        public int? ViaInventoryId { get; set; }

        [ForeignKey(nameof(SupplierId))]
        public virtual Supplier? Supplier { get; set; }

        [ForeignKey(nameof(ViaInventoryId))]
        public virtual Inventory? ViaInventory { get; set; }
    }
}