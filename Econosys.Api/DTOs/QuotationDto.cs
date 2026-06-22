namespace Econosys.Api.DTOs
{
    public class QuotationOrderCostDto
    {
        public int Id { get; set; }
        public int? CustomerOrderId { get; set; }
        public int? SupplierOrderId { get; set; }
        public int? CostId { get; set; }
        public bool DoDebit { get; set; }
        public decimal? NrOf { get; set; }
        public decimal? InPrice { get; set; }
        public decimal? InPriceAttested { get; set; }
        public decimal? OutPrice { get; set; }
        public decimal? OutPriceSEK { get; set; }
        public decimal? Markup { get; set; }
        public string? Note { get; set; }
        public string? SupplierName { get; set; }
        public bool DoPrintOnQuotation { get; set; }
        public bool DoPrintOnCustomerOrder { get; set; }
        public bool DoPrintOnSupplierOrder { get; set; }
        public bool DoInvoiceSeparately { get; set; }
        public bool DoInvoiceSeparatelyImmediately { get; set; }
        public bool IsCostInvoicedSeparately { get; set; }
        public int? InvoiceId { get; set; }
        public int? InvoiceRowId { get; set; }
    }

    public class UpsertQuotationOrderCostRequest
    {
        public int? Id { get; set; }
        public int? CustomerOrderId { get; set; }
        public int? SupplierOrderId { get; set; }
        public int? CostId { get; set; }
        public bool DoDebit { get; set; }
        public decimal? NrOf { get; set; }
        public decimal? InPrice { get; set; }
        public decimal? InPriceAttested { get; set; }
        public decimal? OutPrice { get; set; }
        public string? Note { get; set; }
        public string? SupplierName { get; set; }
        public bool DoPrintOnQuotation { get; set; }
        public bool DoPrintOnCustomerOrder { get; set; }
        public bool DoPrintOnSupplierOrder { get; set; }
        public bool DoInvoiceSeparately { get; set; }
        public bool DoInvoiceSeparatelyImmediately { get; set; }
        public bool IsCostInvoicedSeparately { get; set; }
    }

    public class QuotationRowDto
    {
        public int Id { get; set; }
        public int? QuotationId { get; set; }
        public int? CalculationRowId { get; set; }
        public int? Edition { get; set; }
        public double? Price { get; set; }
        public string? SalesCurrencyName { get; set; }
        public string? UnitName { get; set; }
        public short? UnitMultiplicator { get; set; }
    }

    public class QuotationDto
    {
        public int Id { get; set; }
        public int CompanyId { get; set; }
        public int? CalculationId { get; set; }
        public int? InquiryId { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? Address { get; set; }
        public string? PostalNr { get; set; }
        public string? PostalAddress { get; set; }
        public string? Country { get; set; }
        public string? DeliveryAddressName { get; set; }
        public string? DeliveryAddress { get; set; }
        public string? DeliveryPostalNr { get; set; }
        public string? DeliveryPostalAddress { get; set; }
        public string? DeliveryCountry { get; set; }
        public DateTime? Date { get; set; }
        public string? YourReference { get; set; }
        public string? OurReference { get; set; }
        public string? Product { get; set; }
        public string? Material { get; set; }
        public string? MaterialThickness { get; set; }
        public string? Format { get; set; }
        public string? Color { get; set; }
        public string? Construction { get; set; }
        public string? TimeOfDelivery { get; set; }
        public string? TermsOfDelivery { get; set; }
        public string? TermsOfPayment { get; set; }
        public short? PaymentDays { get; set; }
        public string? Message { get; set; }
        public int? SalesCurrencyId { get; set; }
        public int? UnitId { get; set; }
        public bool PartOfPrintEdition { get; set; }
        public double? PartOfPrintEditionValue { get; set; }
        public bool PartOfPunchEdition { get; set; }
        public double? PartOfPunchEditionValue { get; set; }
        public bool ChangeOfSheet { get; set; }
        public short? NrOfChangeOfSheet { get; set; }
        public double? ChangeOfSheetValue { get; set; }
        public bool ChangeOfCliche { get; set; }
        public short? NrOfChangeOfCliche { get; set; }
        public double? ChangeOfClicheValue { get; set; }
        public bool ChangeOfColor { get; set; }
        public short? NrOfChangeOfColor { get; set; }
        public double? ChangeOfColorValue { get; set; }
        public bool PmsColor { get; set; }
        public double? PmsColorValue { get; set; }
        public bool EurPallet { get; set; }
        public short? NrOfEurPallet { get; set; }
        public double? EurPalletValue { get; set; }
        public bool OtherCost { get; set; }
        public string? OtherCostName { get; set; }
        public double? OtherCostValue { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? EditedAt { get; set; }
        public int? CreatedBy { get; set; }
        public string? CreatedByUserName { get; set; }
        public int? EditedBy { get; set; }
        public string? EditedByUserName { get; set; }
        public bool Printed { get; set; }
        public bool HideDeliveryAddressOnPrint { get; set; }
        public string? ProductMessage { get; set; }
        public string? NoteInternal { get; set; }
        public double? SalesCurrencyRate { get; set; }
        public int? PurchaseCurrencyId { get; set; }
        public double? PurchaseCurrencyRate { get; set; }
        public string? Address2 { get; set; }
        public bool AutoGenerated { get; set; }
        public int? CustomerDeliveryAddressId { get; set; }
        public int? PalletFormatId { get; set; }
        public bool IsFsc { get; set; }
        public string? CustomerEmail { get; set; }
        public int? SelectedSupplierId { get; set; }
        public string? SelectedSupplierName { get; set; }
        public string? SellerName { get; set; }
        public List<QuotationRowDto> QuotationRows { get; set; } = new();
        public List<QuotationOrderCostDto> OrderCosts { get; set; } = new();
    }

    public class QuotationFormOptionsDto
    {
        public IReadOnlyList<FilterOptionDto<string>> Users { get; set; } = Array.Empty<FilterOptionDto<string>>();
        public IReadOnlyList<CostDto> Costs { get; set; } = Array.Empty<CostDto>();
        public IReadOnlyList<CurrencyDto> Currencies { get; set; } = Array.Empty<CurrencyDto>();
        public IReadOnlyList<PalletFormatDto> PalletFormats { get; set; } = Array.Empty<PalletFormatDto>();
    }

    public class CreateQuotationRequest
    {
        public int CompanyId { get; set; }
        public int? CalculationId { get; set; }
        public int? InquiryId { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? Address { get; set; }
        public string? PostalNr { get; set; }
        public string? PostalAddress { get; set; }
        public string? Country { get; set; }
        public string? DeliveryAddressName { get; set; }
        public string? DeliveryAddress { get; set; }
        public string? DeliveryPostalNr { get; set; }
        public string? DeliveryPostalAddress { get; set; }
        public string? DeliveryCountry { get; set; }
        public DateTime? Date { get; set; }
        public string? YourReference { get; set; }
        public string? OurReference { get; set; }
        public string? Product { get; set; }
        public string? Material { get; set; }
        public string? MaterialThickness { get; set; }
        public string? Format { get; set; }
        public string? Color { get; set; }
        public string? Construction { get; set; }
        public string? TimeOfDelivery { get; set; }
        public string? TermsOfDelivery { get; set; }
        public string? TermsOfPayment { get; set; }
        public short? PaymentDays { get; set; }
        public string? Message { get; set; }
        public int? SalesCurrencyId { get; set; }
        public int? UnitId { get; set; }
        public bool PartOfPrintEdition { get; set; }
        public double? PartOfPrintEditionValue { get; set; }
        public bool PartOfPunchEdition { get; set; }
        public double? PartOfPunchEditionValue { get; set; }
        public bool ChangeOfSheet { get; set; }
        public short? NrOfChangeOfSheet { get; set; }
        public double? ChangeOfSheetValue { get; set; }
        public bool ChangeOfCliche { get; set; }
        public short? NrOfChangeOfCliche { get; set; }
        public double? ChangeOfClicheValue { get; set; }
        public bool ChangeOfColor { get; set; }
        public short? NrOfChangeOfColor { get; set; }
        public double? ChangeOfColorValue { get; set; }
        public bool PmsColor { get; set; }
        public double? PmsColorValue { get; set; }
        public bool EurPallet { get; set; }
        public short? NrOfEurPallet { get; set; }
        public double? EurPalletValue { get; set; }
        public bool OtherCost { get; set; }
        public string? OtherCostName { get; set; }
        public double? OtherCostValue { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? EditedAt { get; set; }
        public int? CreatedBy { get; set; }
        public int? EditedBy { get; set; }
        public bool Printed { get; set; }
        public bool HideDeliveryAddressOnPrint { get; set; }
        public string? ProductMessage { get; set; }
        public string? NoteInternal { get; set; }
        public double? SalesCurrencyRate { get; set; }
        public int? PurchaseCurrencyId { get; set; }
        public double? PurchaseCurrencyRate { get; set; }
        public string? Address2 { get; set; }
        public bool AutoGenerated { get; set; }
        public int? CustomerDeliveryAddressId { get; set; }
        public int? PalletFormatId { get; set; }
        public bool IsFsc { get; set; }
        public List<int> QuotationRowIds { get; set; } = new();
        public List<UpsertQuotationOrderCostRequest> OrderCosts { get; set; } = new();
    }

    public class UpdateQuotationRequest : CreateQuotationRequest
    {
        public int Id { get; set; }
    }

    public class SearchQuotationsRequest
    {
        public string? SearchTerm { get; set; }
        public PaginationRequest? Pagination { get; set; }
        public List<SortRequest>? OrderBy { get; set; }
    }

    public class QuotationSearchItemDto
    {
        public int Id { get; set; }
        public string? Product { get; set; }
        public string? CustomerName { get; set; }
        public string? SupplierName { get; set; }
        public string? Construction { get; set; }
        public string? Material { get; set; }
        public string? Format { get; set; }
        public string? SellerName { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? EditedAt { get; set; }
    }
}