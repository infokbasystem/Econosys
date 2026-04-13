namespace Econosys.Api.DTOs
{
    public class SupplierDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? SortName { get; set; }
        public string? Reference { get; set; }
        public string? ContactPerson { get; set; }
        public string? Address { get; set; }
        public string? PostalNr { get; set; }
        public string? PostalAddress { get; set; }
        public string? VisitingAddress { get; set; }
        public string? Country { get; set; }
        public string? Telephone1 { get; set; }
        public string? Telephone2 { get; set; }
        public string? Telephone3 { get; set; }
        public string? Fax { get; set; }
        public string? Note { get; set; }
        public bool Active { get; set; }
        public int? LanguageId { get; set; }
        public string? TermsOfDelivery { get; set; }
        public string? TermsOfPayment { get; set; }
        public string? Email { get; set; }
        public int? InquiryCommunicationTypeId { get; set; }
        public int? SupplierOrderCommunicationTypeId { get; set; }
        public string? CostCenter { get; set; }
        public int? CurrencyId { get; set; }
        public string? Address2 { get; set; }
        public int? OldDbId { get; set; }
        public bool EconopackTransportResponsible { get; set; }
        public bool PrintForDocumentScanning { get; set; }
        public decimal? PricePerEurPallet { get; set; }
        public bool FscDefault { get; set; }
        public int? SupplierOrderTemplateNr { get; set; }
    }
}
