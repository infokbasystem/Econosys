namespace Econosys.Api.DTOs
{
    public class CustomerOrderOrderCostDto
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

    public class UpsertCustomerOrderOrderCostRequest
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

    public class CustomerOrderDto
    {
        public int Id { get; set; }
        public int? QuotationId { get; set; }
        public int? SupplierOrderId { get; set; }
        public string? SupplierOrderNr { get; set; }
        public string? CustomerOrderNr { get; set; }
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
        public string? GoodsMarking { get; set; }
        public int? SalesCurrencyId { get; set; }
        public string? SalesCurrencyName { get; set; }
        public int? UnitId { get; set; }
        public string? UnitName { get; set; }
        public int? SelectedCalculationRowId { get; set; }
        public int? Edition { get; set; }
        public double? SalesPrice { get; set; }
        public bool OrderConfirmationPrinted { get; set; }
        public bool PartOfPrintEdition { get; set; }
        public double? PartOfPrintEditionValue { get; set; }
        public bool PartOfPunchEdition { get; set; }
        public double? PartOfPunchEditionValue { get; set; }
        public bool ChangeOfSheet { get; set; }
        public short? NrOfChangeOfSheet { get; set; }
        public double? ChangeOfSheetValue { get; set; }
        public bool ChangeOfCliche { get; set; }
        public short? NrOfChangeOfCliche { get; set; }
        public double? ChangeOfClichemValue { get; set; }
        public bool ChangeOfColor { get; set; }
        public short? NrOfChangeOfColor { get; set; }
        public double? ChangeOfColorValue { get; set; }
        public bool PMSColor { get; set; }
        public double? PMSColorValue { get; set; }
        public bool EurPallet { get; set; }
        public short? NrOfEurPallet { get; set; }
        public double? EurPalletValue { get; set; }
        public bool OtherCost { get; set; }
        public string? OtherCostDescription { get; set; }
        public double? OtherCostValue { get; set; }
        public DateTime? Created { get; set; }
        public DateTime? Edited { get; set; }
        public int? CreatedBy { get; set; }
        public string? CreatedByUserName { get; set; }
        public int? EditedBy { get; set; }
        public string? EditedByUserName { get; set; }
        public string? YourOrderNr { get; set; }
        public bool Completed { get; set; }
        public string? ProductMessage { get; set; }
        public double? SalesCurrencyRate { get; set; }
        public string? Customer2 { get; set; }
        public string? Address2 { get; set; }
        public int? OldDbId { get; set; }
        public int? ResponsibleUserId { get; set; }
        public DateTime? CompletedDate { get; set; }
        public bool HasFreightCost { get; set; }
        public double? FreightCost { get; set; }
        public bool IsCallOff { get; set; }
        public bool IsReadyForLoading { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public bool DeliveryDateWeekMode { get; set; }
        public string? LogisticsInfoInternal { get; set; }
        public int? CustomerDeliveryAddressId { get; set; }
        public decimal? SupplierPricePerEurPallet { get; set; }
        public int? SupplierPricePerEurPalletCurrencyId { get; set; }
        public decimal? SupplierPricePerEurPalletCurrencyRate { get; set; }
        public int? PalletFormatId { get; set; }
        public bool IsFSC { get; set; }
        public decimal? TotalCostInSalesCurrency { get; set; }
        public string? InvoicingInfo { get; set; }
        public int? CalculationId { get; set; }
        public List<CustomerOrderOrderCostDto> OrderCosts { get; set; } = new();
    }

    public class SearchCustomerOrdersRequest
    {
        public string? SearchTerm { get; set; }
        public FilterRequest? Filter { get; set; }
        public PaginationRequest? Pagination { get; set; }
        public List<SortRequest>? OrderBy { get; set; }
    }

    public class CreateCustomerOrderRequest
    {
        public int? QuotationId { get; set; }
        public int? SupplierOrderId { get; set; }
        public string? CustomerOrderNr { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
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
        public bool IsFSC { get; set; }
        public int? SalesCurrencyId { get; set; }
        public double? SalesCurrencyRate { get; set; }
        public int? UnitId { get; set; }
        public int? Edition { get; set; }
        public double? SalesPrice { get; set; }
        public int? PalletFormatId { get; set; }
        public bool EurPallet { get; set; }
        public List<UpsertCustomerOrderOrderCostRequest> OrderCosts { get; set; } = new();
    }

    public class UpdateCustomerOrderRequest : CreateCustomerOrderRequest
    {
        public int Id { get; set; }
    }
}
