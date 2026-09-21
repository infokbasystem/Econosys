using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("TransportOrderCostCalcSupplierOrderDelivery")]
    public class TransportOrderCostCalcSupplierOrderDelivery
    {
        [Key]
        public int Id { get; set; }

        public int? TransportOrderCostCalcSupplierOrderId { get; set; }

        public int? DeliveryToCustomerId { get; set; }

        public int? DeliveryToStockId { get; set; }

        public int? DeliveryFromStockId { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? NrOfItems { get; set; }

        public int? NrOfPallets { get; set; }

        public int? PalletFormatId { get; set; }

        public bool PalletIsStackable { get; set; }

        public int? PalletWidth { get; set; }

        public int? PalletHeight { get; set; }

        public int? PalletLength { get; set; }

        public int? EditionPerPallet { get; set; }

        [Column(TypeName = "decimal(10,4)")]
        public decimal? PalletCalcFactor { get; set; }

        public bool IsSlattPallet { get; set; }

        public int? ParentDeliveryId { get; set; }

        [ForeignKey(nameof(TransportOrderCostCalcSupplierOrderId))]
        public virtual TransportOrderCostCalcSupplierOrder? TransportOrderCostCalcSupplierOrder { get; set; }

        [ForeignKey(nameof(DeliveryToCustomerId))]
        public virtual DeliveryToCustomer? DeliveryToCustomer { get; set; }

        [ForeignKey(nameof(DeliveryToStockId))]
        public virtual DeliveryToStock? DeliveryToStock { get; set; }

        [ForeignKey(nameof(DeliveryFromStockId))]
        public virtual DeliveryFromStock? DeliveryFromStock { get; set; }

        [ForeignKey(nameof(PalletFormatId))]
        public virtual PalletFormat? PalletFormat { get; set; }
    }
}
