namespace Econosys.Api.DTOs
{
    public class SupplierOrderWorkflowOptionsDto
    {
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public List<SupplierOrderWorkflowRowDto> CalculationRows { get; set; } = new();
        public List<SupplierOrderWorkflowDocumentDto> Inquiries { get; set; } = new();
        public List<SupplierOrderWorkflowDocumentDto> Quotations { get; set; } = new();
    }

    public class SupplierOrderWorkflowRowDto
    {
        public int Id { get; set; }
        public int? Edition { get; set; }
        public double? PurchasePrice { get; set; }
        public double? SalesPrice { get; set; }
        public double? Storage { get; set; }
    }

    public class SupplierOrderWorkflowDocumentDto
    {
        public int Id { get; set; }
        public string? CustomerName { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? SupplierName { get; set; }
    }

    public class CreateSupplierOrderDraftRequest
    {
        public int CalculationRowId { get; set; }
        public int InquiryId { get; set; }
        public int QuotationId { get; set; }
    }

    public class SupplierOrderWorkflowDraftDto
    {
        public SupplierOrderDto SupplierOrder { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public IReadOnlyList<FilterOptionDto<int>> SupplierFactories { get; set; } = Array.Empty<FilterOptionDto<int>>();
        public IReadOnlyList<PalletFormatDto> PalletFormats { get; set; } = Array.Empty<PalletFormatDto>();
    }
}
