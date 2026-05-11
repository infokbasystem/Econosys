using Econosys.Api.Models;

namespace Econosys.Api.DTOs
{
    public class InvoiceRowDto
    {
        public int Id { get; set; }
        public int? InvoiceId { get; set; }
        public int? SortOrder { get; set; }
        public int? InvoiceRowNumber { get; set; }
        public int? DeliveryToCustomerId { get; set; }
        public int? DeliveryFromStockId { get; set; }
        public int? PriceTypeId { get; set; }
        public string? PriceKey { get; set; }
        public string? Text { get; set; }
        public string? Text2 { get; set; }
        public double? NrOf { get; set; }
        public double? UnitPrice { get; set; }
        public double? Sum { get; set; }
        public bool VatGround { get; set; }
        public bool CompareWithOrder { get; set; }
        public bool Calculate { get; set; }
        public string? AccountNr { get; set; }
        public string? CostCenter { get; set; }
        public int? UnitId { get; set; }
        public int? OldDbId { get; set; }
        public bool DoNotAggregateWithParent { get; set; }
        public decimal? Weight { get; set; }
        public int? OrderCostId { get; set; }
    }

    public class InvoiceAccountRowDto
    {
        public int Id { get; set; }
        public int? InvoiceId { get; set; }
        public string? Text { get; set; }
        public double? NrOf { get; set; }
        public double? UnitPrice { get; set; }
        public double? Sum { get; set; }
        public string? AccountNr { get; set; }
        public string? CostCenter { get; set; }
        public int? OldDbId { get; set; }
    }

    public class InvoiceDto
    {
        public int Id { get; set; }
        public InvoiceTypeCode? InvoiceTypeCode { get; set; }
        public int? CustomerOrderId { get; set; }
        public int? DeliveryToCustomerId { get; set; }
        public int? DeliveryFromStockId { get; set; }
        public int? InvoiceNumber { get; set; }
        public int? CreditingInvoiceNumber { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? Address { get; set; }
        public string? PostalNr { get; set; }
        public string? PostalAddress { get; set; }
        public string? Country { get; set; }
        public string? VatNr { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public string? YourReference { get; set; }
        public string? OurReference { get; set; }
        public string? Product { get; set; }
        public string? Material { get; set; }
        public string? MaterialThickness { get; set; }
        public string? Format { get; set; }
        public string? Color { get; set; }
        public string? Construction { get; set; }
        public short? InvoiceDays { get; set; }
        public string? TermsOfPayment { get; set; }
        public int? SalesCurrencyId { get; set; }
        public int? UnitId { get; set; }
        public DateTime? AccountDate { get; set; }
        public int? JournalNr { get; set; }
        public string? Account { get; set; }
        public string? CostCenter { get; set; }
        public bool InvoiceOk { get; set; }
        public bool EndInvoiced { get; set; }
        public bool Printed { get; set; }
        public DateTime? Created { get; set; }
        public DateTime? Edited { get; set; }
        public int? CreatedBy { get; set; }
        public int? EditedBy { get; set; }
        public string? YourOrderNr { get; set; }
        public bool CollectInvoice { get; set; }
        public string? OurOrderNrFreeInvoice { get; set; }
        public string? TimeOfDelivery { get; set; }
        public string? Address2 { get; set; }
        public bool IsSettled { get; set; }
        public int? OldDbId { get; set; }
        public string? NoteInternal { get; set; }
        public int? LanguageId { get; set; }
        public decimal? SalesCurrencyRate { get; set; }
        public decimal? InventoryCurrencyRate { get; set; }
        public IReadOnlyList<InvoiceRowDto> InvoiceRows { get; set; } = Array.Empty<InvoiceRowDto>();
        public IReadOnlyList<InvoiceAccountRowDto> InvoiceAccountRows { get; set; } = Array.Empty<InvoiceAccountRowDto>();
    }

    public class InvoiceSearchRowDto
    {
        public int Id { get; set; }
        public int? InvoiceNumber { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public DateTime? InvoiceDate { get; set; }
        public DateTime? DueDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string OrderNumbers { get; set; } = string.Empty;
        public string DeliveryDates { get; set; } = string.Empty;
        public decimal SumExVat { get; set; }
        public decimal SumInclVat { get; set; }
        public string CurrencyName { get; set; } = string.Empty;
        public decimal SumInclVatSek { get; set; }
        public bool IsPaid { get; set; }
    }

    public class InvoiceSearchTotalsDto
    {
        public int TotalInvoices { get; set; }
        public int PaidInvoices { get; set; }
        public int UnpaidInvoices { get; set; }
        public decimal SumExVat { get; set; }
        public decimal SumInclVat { get; set; }
        public decimal SumInclVatSek { get; set; }
    }
}
