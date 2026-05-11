using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("DocumentFile")]
    public class DocumentFile
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(500)]
        public string? FileNamePath { get; set; }

        [MaxLength(100)]
        public string? FileType { get; set; }

        public int? DocumentTypeId { get; set; }

        public int? SupplierOrderId { get; set; }

        [MaxLength(500)]
        public string? Name { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        public DateTime? CreatedDateTime { get; set; }

        public int? TransportOrderId { get; set; }

        public int? CreatedByUserId { get; set; }

        public int? CallOffId { get; set; }

        public bool IsAttested { get; set; }

        public DateTime? AttestedDateTime { get; set; }

        [MaxLength(50)]
        public string? AttestedByInitials { get; set; }

        public bool IsPaid { get; set; }

        public DateTime? PaidDateTime { get; set; }

        [MaxLength(50)]
        public string? PaidByInitials { get; set; }

        public int? DeviationId { get; set; }

        public int? ProductId { get; set; }

        public virtual DocumentType? DocumentType { get; set; }
        public virtual SupplierOrder? SupplierOrder { get; set; }
        public virtual TransportOrder? TransportOrder { get; set; }
        public virtual LegacyUser? CreatedByUser { get; set; }
        public virtual CallOff? CallOff { get; set; }
        public virtual Deviation? Deviation { get; set; }
        public virtual Product? Product { get; set; }
        public virtual ICollection<DocumentFileRelation> DocumentFileRelations { get; set; } = new List<DocumentFileRelation>();
    }
}