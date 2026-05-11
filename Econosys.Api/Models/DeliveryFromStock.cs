using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("DeliveryFromStock")]
    public class DeliveryFromStock
    {
        [Key]
        [Column("lngDeliveryFromStock_ID")]
        public int Id { get; set; }

        [Column("lngCustomerOrder_ID")]
        public int? CustomerOrderId { get; set; }

        [Column("dteDeliveryDate")]
        public DateTime? DeliveryDate { get; set; }

        [Column("lngNrOfItems")]
        public double? NrOfItems { get; set; }

        [Column("bolToInvoice")]
        public bool ToInvoice { get; set; }

        [Column("lngNrOfPallets")]
        public int? NrOfPallets { get; set; }

        [Column("strCallOff")]
        [MaxLength(50)]
        public string? CallOff { get; set; }

        [Column("lngInventoryId")]
        public int? InventoryId { get; set; }

        public int? OldDbId { get; set; }

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

        public int? DeliveryToStockId { get; set; }

        public bool DoNotInvoice { get; set; }

        public bool InfoOk { get; set; }

        [Column(TypeName = "datetime2(0)")]
        public DateTime? WeightMissingEmailSentDateTime { get; set; }

        [Column(TypeName = "datetime2(0)")]
        public DateTime? CostMissingEmailSentDateTime { get; set; }

        public int? NrOfBunt { get; set; }

        public int? NrOfYtterforpackning { get; set; }

        public bool IsPalletInvoicedSeparately { get; set; }

        [ForeignKey(nameof(CustomerOrderId))]
        public CustomerOrder? CustomerOrder { get; set; }

        [ForeignKey(nameof(InventoryId))]
        public Inventory? Inventory { get; set; }

        [ForeignKey(nameof(DeliveryToStockId))]
        public DeliveryToStock? DeliveryToStock { get; set; }

        [ForeignKey(nameof(ParentDeliveryId))]
        public DeliveryFromStock? ParentDelivery { get; set; }

        public ICollection<DeliveryFromStock> ChildDeliveries { get; set; } = new List<DeliveryFromStock>();
        public ICollection<InvoiceRow> InvoiceRows { get; set; } = new List<InvoiceRow>();
    }
}