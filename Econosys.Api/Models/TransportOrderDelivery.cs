using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("TransportOrderDelivery")]
    public class TransportOrderDelivery
    {
        [Key]
        public int Id { get; set; }

        public int? TransportOrderId { get; set; }

        public int? SortOrder { get; set; }

        public int? DeliveryFromStockId { get; set; }

        public int? DeliveryToCustomerId { get; set; }

        public int? DeliveryToStockId { get; set; }

        public int? SupplierOrderId { get; set; }

        [Column(TypeName = "varchar(500)")]
        public string? DeliveryAddressFreeText { get; set; }

        [Column(TypeName = "varchar(200)")]
        public string? Note { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? CostCalcFreightInternational { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? CostCalcFreightDomestic { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? CostCalcFreightUnloading { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? CostCalcFreightLoading { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? CostCalcFreightOther { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? CostCalcFreightInternationalCost { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? CostCalcFreightDomesticCost { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? CostCalcFreightUnloadingCost { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? CostCalcFreightFtlCost { get; set; }

        public int? ForcedOmlastStatus { get; set; }

        public int? OmlastInventoryId { get; set; }

        [Column(TypeName = "date")]
        public DateTime? OmlastInDate { get; set; }

        [Column(TypeName = "date")]
        public DateTime? OmlastDeliveryDate { get; set; }

        [Column(TypeName = "varchar(500)")]
        public string? OmlastNote { get; set; }

        [Column(TypeName = "decimal(10,4)")]
        public decimal? OmlastPalletPlaces { get; set; }

        [Column(TypeName = "decimal(10,4)")]
        public decimal? TotalWeight { get; set; }

        [Column(TypeName = "varchar(100)")]
        public string? CustomerYourOrderNr { get; set; }

        [ForeignKey(nameof(TransportOrderId))]
        public virtual TransportOrder? TransportOrder { get; set; }

        [ForeignKey(nameof(DeliveryFromStockId))]
        public virtual DeliveryFromStock? DeliveryFromStock { get; set; }

        [ForeignKey(nameof(DeliveryToCustomerId))]
        public virtual DeliveryToCustomer? DeliveryToCustomer { get; set; }

        [ForeignKey(nameof(DeliveryToStockId))]
        public virtual DeliveryToStock? DeliveryToStock { get; set; }

        [ForeignKey(nameof(SupplierOrderId))]
        public virtual SupplierOrder? SupplierOrder { get; set; }

        [ForeignKey(nameof(OmlastInventoryId))]
        public virtual Inventory? OmlastInventory { get; set; }

        public virtual ICollection<TransportOrderDeliveryPallet> TransportOrderDeliveryPallets { get; set; } = new List<TransportOrderDeliveryPallet>();
    }
}
