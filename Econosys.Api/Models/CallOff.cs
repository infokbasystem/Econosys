using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("CallOff")]
    public class CallOff
    {
        [Key]
        public int Id { get; set; }

        public int? ShipperId { get; set; }

        public int? CreatedByUserId { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }

        [Column(TypeName = "smalldatetime")]
        public DateTime? CreatedDateTime { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DeliveryDate { get; set; }

        public bool IsSentToShipper { get; set; }

        [MaxLength(100)]
        public string? Reference { get; set; }

        public bool IsReportedBack { get; set; }

        public int? DeliveryStatus { get; set; }

        public bool DoDebitFreight { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? FreightCostToDebit { get; set; }

        public int? CustomerDeliveryAddressId { get; set; }

        public virtual Shipper? Shipper { get; set; }
        public virtual LegacyUser? CreatedByUser { get; set; }
        public virtual CustomerDeliveryAddress? CustomerDeliveryAddress { get; set; }
        public virtual ICollection<CallOffDelivery> CallOffDeliveries { get; set; } = new List<CallOffDelivery>();

        public virtual ICollection<DocumentFile> DocumentFiles { get; set; } = new List<DocumentFile>();
    }
}