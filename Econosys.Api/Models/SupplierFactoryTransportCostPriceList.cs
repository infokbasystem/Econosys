using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("SupplierFactoryTransportCostPriceList")]
    public class SupplierFactoryTransportCostPriceList
    {
        [Key]
        public int Id { get; set; }

        public int? SupplierFactoryId { get; set; }

        public int? TransportCostPriceListId { get; set; }

        [ForeignKey(nameof(SupplierFactoryId))]
        public virtual SupplierFactory? SupplierFactory { get; set; }

        [ForeignKey(nameof(TransportCostPriceListId))]
        public virtual TransportCostPriceList? TransportCostPriceList { get; set; }
    }
}
