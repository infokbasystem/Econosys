using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("TransportOrderCostCalc")]
    public class TransportOrderCostCalc
    {
        [Key]
        public int Id { get; set; }

        public int? TransportOrderId { get; set; }

        [Column(TypeName = "varchar(200)")]
        public string? CalcCityFrom { get; set; }

        public int? CalcPostalNrTo { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? ResultInternationalCost { get; set; }

        [Column(TypeName = "varchar(500)")]
        public string? Note { get; set; }

        public int? CreatedAtStatus { get; set; }

        public DateTime? CreatedDateTime { get; set; }

        public bool IsManualCalc { get; set; }

        public bool IsLTL { get; set; }

        public int? CostCalcForcePriceListId { get; set; }

        [ForeignKey(nameof(TransportOrderId))]
        public virtual TransportOrder? TransportOrder { get; set; }

        [ForeignKey(nameof(CostCalcForcePriceListId))]
        public virtual TransportCostPriceList? TransportCostPriceList { get; set; }

        public virtual ICollection<TransportOrderCostCalcSupplierOrder> TransportOrderCostCalcSupplierOrders { get; set; } = new List<TransportOrderCostCalcSupplierOrder>();
    }
}
