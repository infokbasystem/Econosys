using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("InventoryTransportCostPriceList")]
    public class InventoryTransportCostPriceList
    {
        [Key]
        public int Id { get; set; }

        public int? InventoryId { get; set; }

        public int? TransportCostPriceListId { get; set; }

        [ForeignKey(nameof(InventoryId))]
        public virtual Inventory? Inventory { get; set; }

        [ForeignKey(nameof(TransportCostPriceListId))]
        public virtual TransportCostPriceList? TransportCostPriceList { get; set; }
    }
}
