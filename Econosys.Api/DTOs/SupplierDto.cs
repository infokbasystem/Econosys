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

    public class SupplierDetailsDto
    {
        public SupplierDto Supplier { get; set; } = new();
        public List<SupplierContactPersonDto> ContactPersons { get; set; } = new();
        public List<SupplierFactoryDto> Factories { get; set; } = new();
        public List<FilterOptionDto<int>> Languages { get; set; } = new();
    }

    public class SupplierFormOptionsDto
    {
        public IReadOnlyList<CurrencyDto> Currencies { get; set; } = Array.Empty<CurrencyDto>();
        public IReadOnlyList<FilterOptionDto<string>> TermsOfDelivery { get; set; } = Array.Empty<FilterOptionDto<string>>();
        public IReadOnlyList<FilterOptionDto<string>> TermsOfPayment { get; set; } = Array.Empty<FilterOptionDto<string>>();
        public IReadOnlyList<FilterOptionDto<int>> Languages { get; set; } = Array.Empty<FilterOptionDto<int>>();
        public IReadOnlyList<FilterOptionDto<int>> InquiryCommunicationTypes { get; set; } = Array.Empty<FilterOptionDto<int>>();
        public IReadOnlyList<FilterOptionDto<int>> SupplierOrderCommunicationTypes { get; set; } = Array.Empty<FilterOptionDto<int>>();
    }

    public class SupplierContactPersonDto
    {
        public int Id { get; set; }
        public string? SupplierContactPersonName { get; set; }
        public string? ContactPerson { get; set; }
        public string? Name { get; set; }
        public string? Email { get; set; }
        public string? Telephone { get; set; }
        public string? Cellphone { get; set; }
        public bool MailInquiry { get; set; }
        public bool MailSupplierOrder { get; set; }
        public bool DoMailTransportOrder { get; set; }
        public string? Title { get; set; }
        public int? OldDbId { get; set; }
    }

    public class SupplierFactoryDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? Address { get; set; }
        public string? PostalNr { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? CountryCode { get; set; }
        public string? AddressExtra { get; set; }
        public bool IsDefault { get; set; }
        public int? PositionId { get; set; }
        public int? ViaInventoryId { get; set; }
    }
}
