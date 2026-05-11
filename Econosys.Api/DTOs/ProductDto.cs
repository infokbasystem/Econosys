using System.ComponentModel.DataAnnotations;

namespace Econosys.Api.DTOs
{
    public class ProductDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? NoteInternal { get; set; }
        public string? NoteExternal { get; set; }
        public string? NoteExtra { get; set; }
        public int? MaterialId { get; set; }
        public string? MaterialThickness { get; set; }
        public int? LengthMm { get; set; }
        public int? WidthMm { get; set; }
        public int? HeightMm { get; set; }
        public string? Format { get; set; }
        public string? NrOfColors { get; set; }
        public int? ConstructionId { get; set; }
        public int? VarnishId { get; set; }
        public string? VarnishOther { get; set; }
        public int? NetWeightPer1000 { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? EditedAt { get; set; }
        public int? CreatedBy { get; set; }
        public int? EditedBy { get; set; }
        public string? ProductCode { get; set; }
        public int? OldDbId { get; set; }
        public int? MaterialGroup { get; set; }
        public bool Active { get; set; }
        public bool? IsServicePackaging { get; set; }
        public DateTime? LastSupplierOrderCreated { get; set; }
        public DateTime? LastCustomerOrderCreated { get; set; }
        public ConstructionLookupDto? Construction { get; set; }
    }

    public class ConstructionLookupDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    public class CreateProductRequest
    {
        [MaxLength(50)]
        public string? Name { get; set; }

        [MaxLength(255)]
        public string? NoteInternal { get; set; }

        [MaxLength(255)]
        public string? NoteExternal { get; set; }

        [MaxLength(255)]
        public string? NoteExtra { get; set; }

        public int? MaterialId { get; set; }

        [MaxLength(50)]
        public string? MaterialThickness { get; set; }

        public int? LengthMm { get; set; }
        public int? WidthMm { get; set; }
        public int? HeightMm { get; set; }

        [MaxLength(50)]
        public string? Format { get; set; }

        [MaxLength(255)]
        public string? NrOfColors { get; set; }

        public int? ConstructionId { get; set; }
        public int? VarnishId { get; set; }

        [MaxLength(50)]
        public string? VarnishOther { get; set; }

        public int? NetWeightPer1000 { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? EditedAt { get; set; }
        public int? CreatedBy { get; set; }
        public int? EditedBy { get; set; }

        [MaxLength(50)]
        public string? ProductCode { get; set; }

        public int? OldDbId { get; set; }
        public int? MaterialGroup { get; set; }
        public bool Active { get; set; }
        public bool? IsServicePackaging { get; set; }
        public DateTime? LastSupplierOrderCreated { get; set; }
        public DateTime? LastCustomerOrderCreated { get; set; }
    }

    public class UpdateProductRequest
    {
        [MaxLength(50)]
        public string? Name { get; set; }

        [MaxLength(255)]
        public string? NoteInternal { get; set; }

        [MaxLength(255)]
        public string? NoteExternal { get; set; }

        [MaxLength(255)]
        public string? NoteExtra { get; set; }

        public int? MaterialId { get; set; }

        [MaxLength(50)]
        public string? MaterialThickness { get; set; }

        public int? LengthMm { get; set; }
        public int? WidthMm { get; set; }
        public int? HeightMm { get; set; }

        [MaxLength(50)]
        public string? Format { get; set; }

        [MaxLength(255)]
        public string? NrOfColors { get; set; }

        public int? ConstructionId { get; set; }
        public int? VarnishId { get; set; }

        [MaxLength(50)]
        public string? VarnishOther { get; set; }

        public int? NetWeightPer1000 { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? EditedAt { get; set; }
        public int? CreatedBy { get; set; }
        public int? EditedBy { get; set; }

        [MaxLength(50)]
        public string? ProductCode { get; set; }

        public int? OldDbId { get; set; }
        public int? MaterialGroup { get; set; }
        public bool? Active { get; set; }
        public bool? IsServicePackaging { get; set; }
        public DateTime? LastSupplierOrderCreated { get; set; }
        public DateTime? LastCustomerOrderCreated { get; set; }
    }

    public class SearchProductsRequest
    {
        public FilterRequest? Filter { get; set; }
        public PaginationRequest? Pagination { get; set; }
        public List<SortRequest>? OrderBy { get; set; }
    }
}
