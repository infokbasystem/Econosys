using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("StockTakingItem")]
    public class StockTakingItem
    {
        [Key]
        [Column("lngStockTakingItem_ID")]
        public int Id { get; set; }

        [Column("lngStockTaking_ID")]
        public int? StockTakingId { get; set; }

        [Column("lngSupplierOrder_ID")]
        public int? SupplierOrderId { get; set; }

        [Column("lngNrOfItems")]
        public int? NrOfItems { get; set; }

        [Column("lngNrPallets")]
        public int? NrOfPallets { get; set; }

        [Column("lngDiffNrOfItems")]
        public int? DiffNrOfItems { get; set; }

        [Column("lngDiffNrOfPallets")]
        public int? DiffNrOfPallets { get; set; }

        public int? OldDbId { get; set; }

        [ForeignKey(nameof(StockTakingId))]
        public StockTaking? StockTaking { get; set; }

        [ForeignKey(nameof(SupplierOrderId))]
        public SupplierOrder? SupplierOrder { get; set; }
    }
}