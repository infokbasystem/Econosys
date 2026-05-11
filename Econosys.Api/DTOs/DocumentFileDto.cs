using System.ComponentModel.DataAnnotations;

namespace Econosys.Api.DTOs
{
    public class DocumentFileDto
    {
        public int Id { get; set; }
        public string? FileNamePath { get; set; }
        public string? FileType { get; set; }
        public int? DocumentTypeId { get; set; }
        public int? SupplierOrderId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public DateTime? CreatedDateTime { get; set; }
        public int? TransportOrderId { get; set; }
        public int? CreatedByUserId { get; set; }
        public string? CreatedByUserName { get; set; }
        public int? CallOffId { get; set; }
        public bool IsAttested { get; set; }
        public DateTime? AttestedDateTime { get; set; }
        public string? AttestedByInitials { get; set; }
        public bool IsPaid { get; set; }
        public DateTime? PaidDateTime { get; set; }
        public string? PaidByInitials { get; set; }
        public int? DeviationId { get; set; }
        public int? ProductId { get; set; }
    }

    public class CreateDocumentFileRequest
    {
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
    }

    public class UpdateDocumentFileRequest
    {
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
        public bool? IsAttested { get; set; }
        public DateTime? AttestedDateTime { get; set; }

        [MaxLength(50)]
        public string? AttestedByInitials { get; set; }

        public bool? IsPaid { get; set; }
        public DateTime? PaidDateTime { get; set; }

        [MaxLength(50)]
        public string? PaidByInitials { get; set; }

        public int? DeviationId { get; set; }
        public int? ProductId { get; set; }
    }
}
