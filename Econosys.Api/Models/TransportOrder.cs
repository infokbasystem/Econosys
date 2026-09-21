using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("TransportOrder")]
    public class TransportOrder
    {
        [Key]
        public int Id { get; set; }

        public int TransportOrderNr { get; set; }

        [MaxLength(255)]
        public string? SenderReference { get; set; }

        public DateTime? DateCreated { get; set; }

        public DateTime? DateLoading { get; set; }

        public DateTime? DateDelivery { get; set; }

        [Column("DeliveryStatus")]
        public int? DeliveryStatus { get; set; }

        [MaxLength(1000)]
        public string? Note { get; set; }

        public int? ShipperId { get; set; }

        public bool IsSentToShipper { get; set; }

        public bool IsReportedBack { get; set; }

        public int? CreatedByUserId { get; set; }

        public DateTime? CreatedTimeStamp { get; set; }

        public int? EditedByUserId { get; set; }

        public DateTime? EditedTimeStamp { get; set; }

        public virtual Shipper? Shipper { get; set; }

        public virtual LegacyUser? CreatedByUser { get; set; }

        public virtual LegacyUser? EditedByUser { get; set; }

        public virtual ICollection<DocumentFile> DocumentFiles { get; set; } = new List<DocumentFile>();
    }
}