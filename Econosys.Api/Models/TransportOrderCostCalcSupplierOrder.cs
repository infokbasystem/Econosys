using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("TransportOrderCostCalcSupplierOrder")]
    public class TransportOrderCostCalcSupplierOrder
    {
        [Key]
        public int Id { get; set; }

        public int? TransportOrderCostCalcId { get; set; }

        public int? SupplierOrderId { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? CaclulationInternationalCost { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? CaclulationDomesticCost { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? CaclulationFtl { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? CaclulationUnloading { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? CaclulationTotal { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? CaclulationUsedTotal { get; set; }

        [Column(TypeName = "varchar(500)")]
        public string? CalculationCalcInfo { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? ToInternationalCost { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? TransportOrderDomesticCost { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? TransportOrderLoading { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? TransportOrderUnloading { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? TransportOrderOther { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? TransportOrderTotal { get; set; }

        [Column(TypeName = "varchar(500)")]
        public string? TransportOrderCalcInfo { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? ResultInternationalCost { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? ResultDomesticCost { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? ResultTotal { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? ResultOther { get; set; }

        [Column(TypeName = "varchar(500)")]
        public string? ResultNote { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? DiffTransportOrderCaclulation { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? DiffResultCaclulation { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? DiffResultTransportOrder { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? TotalNrOfItems { get; set; }

        public int? TotalNrOfPallets { get; set; }

        public int? TotalNrOfPalletPlaces { get; set; }

        [Column(TypeName = "decimal(10,4)")]
        public decimal? TotalPalletArea { get; set; }

        [Column(TypeName = "decimal(10,4)")]
        public decimal? PalletFactor { get; set; }

        [ForeignKey(nameof(TransportOrderCostCalcId))]
        public virtual TransportOrderCostCalc? TransportOrderCostCalc { get; set; }

        [ForeignKey(nameof(SupplierOrderId))]
        public virtual SupplierOrder? SupplierOrder { get; set; }

        public virtual ICollection<TransportOrderCostCalcSupplierOrderDelivery> TransportOrderCostCalcSupplierOrderDeliveries { get; set; } = new List<TransportOrderCostCalcSupplierOrderDelivery>();
    }
}
