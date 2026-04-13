using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("StockTaking")]
    public class StockTaking
    {
        [Key]
        [Column("lngStockTaking_ID")]
        public int Id { get; set; }

        [Column("dteStockTakingDate")]
        public DateTime? StockTakingDate { get; set; }

        public int? OldDbId { get; set; }
    }
}
