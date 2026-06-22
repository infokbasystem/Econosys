namespace Econosys.Api.DTOs
{
    public class CustomerDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? SortName { get; set; }
        public string? Address { get; set; }
        public string? PostalNr { get; set; }
        public string? PostalAddress { get; set; }
        public string? VisitingAddress { get; set; }
        public string? Reference { get; set; }
        public string? Telephone1 { get; set; }
        public string? Telephone2 { get; set; }
        public string? Telephone3 { get; set; }
        public string? Fax { get; set; }
        public string? Note { get; set; }
        public string? LoadingInstruction { get; set; }
        public int? ResponsibleUserId { get; set; }
        public bool VAT { get; set; }
        public bool Active { get; set; }
        public string? Country { get; set; }
        public string? TermsOfDelivery { get; set; }
        public string? TermsOfPayment { get; set; }
        public short? PaymentDays { get; set; }
        public int? LanguageId { get; set; }
        public string? VATNr { get; set; }
        public string? Email { get; set; }
        public int? QuotationCommunicationTypeId { get; set; }
        public int? CustomerOrderCommunicationTypeId { get; set; }
        public int? InvoiceCommunicationTypeId { get; set; }
        public string? OrgNr { get; set; }
        public bool EU { get; set; }
        public bool Export { get; set; }
        public int? CreditLimit { get; set; }
        public int? CurrencyId { get; set; }
        public string? ExternalKey { get; set; }
        public short? VATRate { get; set; }
        public string? Address2 { get; set; }
        public bool OneWayPallet { get; set; }
        public bool EURPallet { get; set; }
        public int? OldDbId { get; set; }
        public bool SpecialUnitHandling { get; set; }
        public bool PrintForDocumentScanning { get; set; }
        public decimal? PricePerEurPallet { get; set; }
        public bool LockOrder { get; set; }
        public DateTime? LastCustomerOrderDate { get; set; }
        public int? AccountNrAccountsReceivable { get; set; }
        public int? AccountNrEarnings { get; set; }
        public int? SupportEmployeeId { get; set; }
        public string? LastActivity { get; set; }
        public int? CountryId { get; set; }
        public string? Category { get; set; }
        public bool IsProspect { get; set; }
        public bool InvoicePalletsSeparately { get; set; }
        public bool InvoiceCostsSeparately { get; set; }
        public bool InvoiceCostsSeparatelyImmediately { get; set; }
        public DateOnly? BudgetCountAsNewUntilMonth { get; set; }
        public bool InvoiceRowsInProductNameOrder { get; set; }
        public string? OrderNrPrefix { get; set; }
    }

    public class CustomerDetailsDto
    {
        public CustomerDto Customer { get; set; } = new();
        public List<CustomerDeliveryAddressDto> DeliveryAddresses { get; set; } = new();
        public List<CustomerContactPersonDto> ContactPersons { get; set; } = new();
    }

    public class CustomerDeliveryAddressDto
    {
        public int? Id { get; set; }
        public string? Name { get; set; }
        public string? Address { get; set; }
        public string? PostalNr { get; set; }
        public string? PostalAddress { get; set; }
        public string? Country { get; set; }
    }

    public class CustomerContactPersonDto
    {
        public int Id { get; set; }
        public string? CustomerContactPersonName { get; set; }
        public string? ContactPerson { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Telephone { get; set; }
        public string? Cellphone { get; set; }
        public bool MailQuotation { get; set; }
        public bool MailCustomerOrder { get; set; }
        public bool MailInvoice { get; set; }
        public bool MailTransportOrder { get; set; }
        public bool MailGeneralInfo { get; set; }
        public bool MailCallOffConfirmation { get; set; }
        public string? Title { get; set; }
        public int? OldDbId { get; set; }
    }

    public class CreateCustomerRequest
    {
        public string? Name { get; set; }
        public string? SortName { get; set; }
        public string? Address { get; set; }
        public string? PostalNr { get; set; }
        public string? PostalAddress { get; set; }
        public string? VisitingAddress { get; set; }
        public string? Reference { get; set; }
        public string? Telephone1 { get; set; }
        public string? Telephone2 { get; set; }
        public string? Telephone3 { get; set; }
        public string? Fax { get; set; }
        public string? Note { get; set; }
        public string? LoadingInstruction { get; set; }
        public int? ResponsibleUserId { get; set; }
        public bool VAT { get; set; }
        public bool Active { get; set; }
        public string? Country { get; set; }
        public string? TermsOfDelivery { get; set; }
        public string? TermsOfPayment { get; set; }
        public short? PaymentDays { get; set; }
        public int? LanguageId { get; set; }
        public string? VATNr { get; set; }
        public string? Email { get; set; }
        public int? QuotationCommunicationTypeId { get; set; }
        public int? CustomerOrderCommunicationTypeId { get; set; }
        public int? InvoiceCommunicationTypeId { get; set; }
        public string? OrgNr { get; set; }
        public bool EU { get; set; }
        public bool Export { get; set; }
        public int? CreditLimit { get; set; }
        public int? CurrencyId { get; set; }
        public string? ExternalKey { get; set; }
        public short? VATRate { get; set; }
        public string? Address2 { get; set; }
        public bool OneWayPallet { get; set; }
        public bool EURPallet { get; set; }
        public int? OldDbId { get; set; }
        public bool SpecialUnitHandling { get; set; }
        public bool PrintForDocumentScanning { get; set; }
        public decimal? PricePerEurPallet { get; set; }
        public bool LockOrder { get; set; }
        public DateTime? LastCustomerOrderDate { get; set; }
        public int? AccountNrAccountsReceivable { get; set; }
        public int? AccountNrEarnings { get; set; }
        public int? SupportEmployeeId { get; set; }
        public string? LastActivity { get; set; }
        public int? CountryId { get; set; }
        public string? Category { get; set; }
        public bool IsProspect { get; set; }
        public bool InvoicePalletsSeparately { get; set; }
        public bool InvoiceCostsSeparately { get; set; }
        public bool InvoiceCostsSeparatelyImmediately { get; set; }
        public DateOnly? BudgetCountAsNewUntilMonth { get; set; }
        public bool InvoiceRowsInProductNameOrder { get; set; }
        public string? OrderNrPrefix { get; set; }
    }

    public class UpdateCustomerRequest
    {
        public string? Name { get; set; }
        public string? SortName { get; set; }
        public string? Address { get; set; }
        public string? PostalNr { get; set; }
        public string? PostalAddress { get; set; }
        public string? VisitingAddress { get; set; }
        public string? Reference { get; set; }
        public string? Telephone1 { get; set; }
        public string? Telephone2 { get; set; }
        public string? Telephone3 { get; set; }
        public string? Fax { get; set; }
        public string? Note { get; set; }
        public string? LoadingInstruction { get; set; }
        public int? ResponsibleUserId { get; set; }
        public bool? VAT { get; set; }
        public bool? Active { get; set; }
        public string? Country { get; set; }
        public string? TermsOfDelivery { get; set; }
        public string? TermsOfPayment { get; set; }
        public short? PaymentDays { get; set; }
        public int? LanguageId { get; set; }
        public string? VATNr { get; set; }
        public string? Email { get; set; }
        public int? QuotationCommunicationTypeId { get; set; }
        public int? CustomerOrderCommunicationTypeId { get; set; }
        public int? InvoiceCommunicationTypeId { get; set; }
        public string? OrgNr { get; set; }
        public bool? EU { get; set; }
        public bool? Export { get; set; }
        public int? CreditLimit { get; set; }
        public int? CurrencyId { get; set; }
        public string? ExternalKey { get; set; }
        public short? VATRate { get; set; }
        public string? Address2 { get; set; }
        public bool? OneWayPallet { get; set; }
        public bool? EURPallet { get; set; }
        public int? OldDbId { get; set; }
        public bool? SpecialUnitHandling { get; set; }
        public bool? PrintForDocumentScanning { get; set; }
        public decimal? PricePerEurPallet { get; set; }
        public bool? LockOrder { get; set; }
        public DateTime? LastCustomerOrderDate { get; set; }
        public int? AccountNrAccountsReceivable { get; set; }
        public int? AccountNrEarnings { get; set; }
        public int? SupportEmployeeId { get; set; }
        public string? LastActivity { get; set; }
        public int? CountryId { get; set; }
        public string? Category { get; set; }
        public bool? IsProspect { get; set; }
        public bool? InvoicePalletsSeparately { get; set; }
        public bool? InvoiceCostsSeparately { get; set; }
        public bool? InvoiceCostsSeparatelyImmediately { get; set; }
        public DateOnly? BudgetCountAsNewUntilMonth { get; set; }
        public bool? InvoiceRowsInProductNameOrder { get; set; }
        public string? OrderNrPrefix { get; set; }
    }

    public class SearchCustomersRequest
    {
        public string? SearchTerm { get; set; }
        public bool? Active { get; set; }
        public bool? IsProspect { get; set; }
        public int? CountryId { get; set; }
        public string? Category { get; set; }
        public string? SortBy { get; set; }
        public bool SortDescending { get; set; }
        public PaginationRequest? Pagination { get; set; }
    }
}
