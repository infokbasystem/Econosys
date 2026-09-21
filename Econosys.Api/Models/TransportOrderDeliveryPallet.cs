using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("TransportOrderDeliveryPallet")]
    public class TransportOrderDeliveryPallet
    {
        [Key]
        public int Id { get; set; }

        public int? TransportOrderDeliveryId { get; set; }

        public int PosCmX { get; set; }

        public int PosCmY { get; set; }

        public int Rotation { get; set; }

        public int Height { get; set; }

        public int Width { get; set; }

        [ForeignKey(nameof(TransportOrderDeliveryId))]
        public virtual TransportOrderDelivery? TransportOrderDelivery { get; set; }
    }
}
