using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("DeliveryToStock")]
    public class DeliveryToStock
    {
        [Key]
        [Column("lngDeliveryToStock_ID")]
        public int Id { get; set; }

        [Column("lngSupplierOrder_ID")]
        public int? SupplierOrderId { get; set; }

        [Column("dteDeliveryDate")]
        public DateTime? DeliveryDate { get; set; }

        [Column("lngNrOfItems")]
        public double? NrOfItems { get; set; }

        [Column("lngNrOfPallets")]
        public int? NrOfPallets { get; set; }

        [Column("strDeliveryNr")]
        [MaxLength(50)]
        public string? DeliveryNr { get; set; }

        [Column("lngInventoryId")]
        public int? InventoryId { get; set; }

        public int? OldDbId { get; set; }

        [MaxLength(50)]
        public string? SpcsRefNr { get; set; }

        public int? SupplierInvoiceNr { get; set; }

        [Column(TypeName = "decimal(19,2)")]
        public decimal? SupplierInvoiceCost { get; set; }

        public int? DeliveryStatus { get; set; }

        public int? PalletFormatId { get; set; }

        public bool PalletIsStackable { get; set; }

        public int? PalletWidth { get; set; }

        public int? PalletHeight { get; set; }

        public int? PalletLength { get; set; }

        public int? EditionPerPallet { get; set; }

        [Column(TypeName = "decimal(5,3)")]
        public decimal? PalletCalcFactor { get; set; }

        public bool IsSlattPallet { get; set; }

        public int? ParentDeliveryId { get; set; }

        public bool InfoOk { get; set; }

        public bool IsAdjustment { get; set; }

        public int? AdjustedFromDeliveryId { get; set; }

        [Column(TypeName = "datetime2(0)")]
        public DateTime? WeightMissingEmailSentDateTime { get; set; }

        [Column(TypeName = "datetime2(0)")]
        public DateTime? CostMissingEmailSentDateTime { get; set; }

        [ForeignKey(nameof(SupplierOrderId))]
        public SupplierOrder? SupplierOrder { get; set; }

        [ForeignKey(nameof(InventoryId))]
        public Inventory? Inventory { get; set; }

        [ForeignKey(nameof(ParentDeliveryId))]
        public DeliveryToStock? ParentDelivery { get; set; }

        public ICollection<DeliveryToStock> ChildDeliveries { get; set; } = new List<DeliveryToStock>();
    }
}