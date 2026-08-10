namespace Econosys.Api.DTOs
{
    public class SupplierOrderOrderCostDto
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

    public class UpsertSupplierOrderOrderCostRequest
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

    public class SupplierOrderDto
    {
        public int Id { get; set; }
        public string? SupplierOrderNr { get; set; }
        public int? BasedOnSupplierOrderId { get; set; }
        public int? InquiryId { get; set; }
        public int? QuotationId { get; set; }
        public int? InquiryOldId { get; set; }
        public int? SupplierId { get; set; }
        public string? SupplierName { get; set; }
        public string? Address { get; set; }
        public string? PostalNr { get; set; }
        public string? PostalAddress { get; set; }
        public string? Country { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
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
        public string? Message { get; set; }
        public string? GoodsMarking { get; set; }
        public string? Preparation { get; set; }
        public int? UnitId { get; set; }
        public string? UnitName { get; set; }
        public int? SelectedCalculationRowId { get; set; }
        public int? Edition { get; set; }
        public double? PurchasePrice { get; set; }
        public int? PurchaseCurrencyId { get; set; }
        public string? PurchaseCurrencyName { get; set; }
        public bool Confirmed { get; set; }
        public bool PuchDrawingAccepted { get; set; }
        public bool PrintBasisAccepted { get; set; }
        public bool HideCustomerInfoOnPrint { get; set; }
        public DateTime? Created { get; set; }
        public DateTime? Edited { get; set; }
        public int? CreatedBy { get; set; }
        public string? CreatedByUserName { get; set; }
        public int? EditedBy { get; set; }
        public string? EditedByUserName { get; set; }
        public string? ProductCode { get; set; }
        public DateTime? ConfirmedDeliveryDate { get; set; }
        public double? PurchaseCurrencyRate { get; set; }
        public string? Customer2 { get; set; }
        public string? Address2 { get; set; }
        public bool OneWayPallet { get; set; }
        public bool EurPallet { get; set; }
        public int? InventoryId { get; set; }
        public string? LoadingInstruction { get; set; }
        public int? OldDbId { get; set; }
        public bool IsNonStopPallet { get; set; }
        public int? SupplierFactoryId { get; set; }
        public string? CustomerOrderNr { get; set; }
        public bool EconopackTransportResponsible { get; set; }
        public bool ConfirmedDeliveryDateWeekMode { get; set; }
        public int? ProducedEdition { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public bool DeliveryDateWeekMode { get; set; }
        public int? CustomerDeliveryAddressId { get; set; }
        public bool IsParcelDelivery { get; set; }
        public int? PalletFormatId { get; set; }
        public int? BlockOrderCalculationId { get; set; }
        public bool HideCustomerNameOnPrint { get; set; }
        public bool HideProductNameOnPrint { get; set; }
        public bool IsFSC { get; set; }
        public int? EditionTakenFromSupplierOrderId { get; set; }
        public DateTime? EmailSentDateTime { get; set; }
        public string? PackagingType { get; set; }
        public int? CalculationId { get; set; }
        public List<SupplierOrderOrderCostDto> OrderCosts { get; set; } = new();
    }

    public class SupplierOrderFormOptionsDto
    {
        public IReadOnlyList<FilterOptionDto<string>> Users { get; set; } = Array.Empty<FilterOptionDto<string>>();
        public IReadOnlyList<CostDto> Costs { get; set; } = Array.Empty<CostDto>();
        public IReadOnlyList<CurrencyDto> Currencies { get; set; } = Array.Empty<CurrencyDto>();
        public IReadOnlyList<PalletFormatDto> PalletFormats { get; set; } = Array.Empty<PalletFormatDto>();
        public IReadOnlyList<FilterOptionDto<int>> SupplierFactories { get; set; } = Array.Empty<FilterOptionDto<int>>();
        public IReadOnlyList<FilterOptionDto<int>> Inventories { get; set; } = Array.Empty<FilterOptionDto<int>>();
    }

    public class SearchSupplierOrdersRequest
    {
        public string? SearchTerm { get; set; }
        public FilterRequest? Filter { get; set; }
        public PaginationRequest? Pagination { get; set; }
        public List<SortRequest>? OrderBy { get; set; }
    }

    public class CreateSupplierOrderRequest
    {
        public string? SupplierOrderNr { get; set; }
        public int? BasedOnSupplierOrderId { get; set; }
        public int? SupplierId { get; set; }
        public int? SupplierFactoryId { get; set; }
        public int? InventoryId { get; set; }
        public string? SupplierName { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerOrderNr { get; set; }
        public string? DeliveryAddressName { get; set; }
        public string? DeliveryAddress { get; set; }
        public string? DeliveryPostalNr { get; set; }
        public string? DeliveryPostalAddress { get; set; }
        public string? DeliveryCountry { get; set; }
        public DateTime? Date { get; set; }
        public string? TimeOfDelivery { get; set; }
        public string? YourReference { get; set; }
        public string? OurReference { get; set; }
        public string? TermsOfDelivery { get; set; }
        public string? TermsOfPayment { get; set; }
        public string? Message { get; set; }
        public string? Product { get; set; }
        public string? Material { get; set; }
        public string? Format { get; set; }
        public string? Color { get; set; }
        public string? Construction { get; set; }
        public string? GoodsMarking { get; set; }
        public bool HideCustomerInfoOnPrint { get; set; }
        public bool HideCustomerNameOnPrint { get; set; }
        public bool HideProductNameOnPrint { get; set; }
        public bool IsFSC { get; set; }
        public bool Confirmed { get; set; }
        public bool EconopackTransportResponsible { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public bool DeliveryDateWeekMode { get; set; }
        public DateTime? ConfirmedDeliveryDate { get; set; }
        public bool ConfirmedDeliveryDateWeekMode { get; set; }
        public string? PackagingType { get; set; }
        public int? PalletFormatId { get; set; }
        public bool EurPallet { get; set; }
        public int? ProducedEdition { get; set; }
        public List<UpsertSupplierOrderOrderCostRequest> OrderCosts { get; set; } = new();
    }

    public class UpdateSupplierOrderRequest : CreateSupplierOrderRequest
    {
        public int Id { get; set; }
    }
}