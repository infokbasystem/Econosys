using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("TransportCostPriceList")]
    public class TransportCostPriceList
    {
        [Key]
        public int Id { get; set; }

        [Column(TypeName = "varchar(200)")]
        public string? Name { get; set; }

        [Column(TypeName = "varchar(200)")]
        public string? ImportName { get; set; }

        [Column(TypeName = "decimal(10,4)")]
        public decimal? SecaMarpolPallet { get; set; }

        [Column(TypeName = "decimal(10,4)")]
        public decimal? DmtPercentPallet { get; set; }

        [Column(TypeName = "decimal(10,4)")]
        public decimal? AdditionCurrencyPercentPallet { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? OtherPallet { get; set; }

        [Column(TypeName = "decimal(10,4)")]
        public decimal? OtherPercentPallet { get; set; }

        [Column(TypeName = "decimal(10,4)")]
        public decimal? AdditionTotalPercentPallet { get; set; }

        public int? CurrencyId { get; set; }

        [Column(TypeName = "decimal(10,4)")]
        public decimal? SecaMarpolFtl { get; set; }

        [Column(TypeName = "decimal(10,4)")]
        public decimal? DmtPercentFtl { get; set; }

        [Column(TypeName = "decimal(10,4)")]
        public decimal? AdditionCurrencyPercentFtl { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? OtherFtl { get; set; }

        [Column(TypeName = "decimal(10,4)")]
        public decimal? OtherPercentFtl { get; set; }

        [Column(TypeName = "decimal(10,4)")]
        public decimal? AdditionTotalPercentFtl { get; set; }

        public bool IsStafflad { get; set; }

        public DateTime? LastImportDateTime { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? LoadingCost { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? UnloadingCost { get; set; }

        [Column(TypeName = "decimal(10,4)")]
        public decimal? PreCalcAdditionPercent { get; set; }

        [ForeignKey(nameof(CurrencyId))]
        public virtual Currency? Currency { get; set; }

        public virtual ICollection<TransportCostPriceListData> TransportCostPriceListData { get; set; } = new List<TransportCostPriceListData>();

        public virtual ICollection<TransportOrderCostCalc> TransportOrderCostCalcs { get; set; } = new List<TransportOrderCostCalc>();

        public virtual ICollection<SupplierFactoryTransportCostPriceList> SupplierFactoryTransportCostPriceLists { get; set; } = new List<SupplierFactoryTransportCostPriceList>();

        public virtual ICollection<InventoryTransportCostPriceList> InventoryTransportCostPriceLists { get; set; } = new List<InventoryTransportCostPriceList>();
    }
}
