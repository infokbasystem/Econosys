using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("DeliveryLeg")]
    public class DeliveryLeg
    {
        [Key]
        public int Id { get; set; }

        public int? TransportOrderId { get; set; }
        public int? CallOffId { get; set; }
        public int? DeliveryToStockId { get; set; }
        public int? DeliveryToCustomerId { get; set; }
        public int? DeliveryFromStockId { get; set; }
        public int? FromPositionId { get; set; }
        public int? ToPositionId { get; set; }
        public int? PositionDistanceId { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? DistanceKm { get; set; }

        [Column(TypeName = "varchar(50)")]
        public string? TypeOfTransport { get; set; }

        public int SortOrder { get; set; }

        [Column(TypeName = "decimal(12,8)")]
        public decimal? LatitudeStart { get; set; }

        [Column(TypeName = "decimal(12,8)")]
        public decimal? LongitudeStart { get; set; }

        [Column(TypeName = "decimal(12,8)")]
        public decimal? LatitudeEnd { get; set; }

        [Column(TypeName = "decimal(12,8)")]
        public decimal? LongitudeEnd { get; set; }

        public int? FromPositionIdLeg { get; set; }
        public int? ToPositionIdLeg { get; set; }

        [ForeignKey(nameof(TransportOrderId))]
        public virtual TransportOrder? TransportOrder { get; set; }

        [ForeignKey(nameof(FromPositionId))]
        public virtual Position? FromPosition { get; set; }

        [ForeignKey(nameof(ToPositionId))]
        public virtual Position? ToPosition { get; set; }

        [ForeignKey(nameof(FromPositionIdLeg))]
        public virtual Position? LegFromPosition { get; set; }

        [ForeignKey(nameof(ToPositionIdLeg))]
        public virtual Position? LegToPosition { get; set; }
    }
}