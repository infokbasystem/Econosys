using System.ComponentModel.DataAnnotations;

namespace Econosys.Api.DTOs
{
    public class CompanySettingsDto
    {
        public int CompanyId { get; set; }
        public int? SettingsId { get; set; }

        public string? CompanyName { get; set; }
        public string? Address { get; set; }
        public string? PostalAddress { get; set; }
        public string? ZipCode { get; set; }
        public string? Telephone1 { get; set; }
        public string? Telephone2 { get; set; }
        public string? Fax1 { get; set; }
        public string? Fax2 { get; set; }
        public string? Bank { get; set; }
        public string? BIC { get; set; }
        public string? IBAN { get; set; }
        public string? BG { get; set; }
        public string? PG { get; set; }
        public string? VATNr { get; set; }
        public int? CompanyOldDbId { get; set; }
        public string? InvoiceMailSWE { get; set; }
        public string? InvoiceMailENG { get; set; }
        public string? DocumentFileBasePath { get; set; }
        public string? Email { get; set; }
        public string? Web { get; set; }
        public string? MailWrapper { get; set; }
        public string? GoogleApiKey { get; set; }

        public short? NrOfInquiryAnswerDays { get; set; }
        public string? VATInfo { get; set; }
        public string? SettingsCompany { get; set; }
        public int? InvoiceLastNr { get; set; }
        public string? DefaultCustomerMessage { get; set; }
        public string? EUText { get; set; }
        public string? ExportText { get; set; }
        public int? SettingsOldDbId { get; set; }
    }

    public class CreateCompanySettingsRequest
    {
        [MaxLength(50)]
        public string? CompanyName { get; set; }

        [MaxLength(100)]
        public string? Address { get; set; }

        [MaxLength(100)]
        public string? PostalAddress { get; set; }

        [MaxLength(50)]
        public string? ZipCode { get; set; }

        [MaxLength(50)]
        public string? Telephone1 { get; set; }

        [MaxLength(50)]
        public string? Telephone2 { get; set; }

        [MaxLength(50)]
        public string? Fax1 { get; set; }

        [MaxLength(50)]
        public string? Fax2 { get; set; }

        [MaxLength(50)]
        public string? Bank { get; set; }

        [MaxLength(50)]
        public string? BIC { get; set; }

        [MaxLength(50)]
        public string? IBAN { get; set; }

        [MaxLength(50)]
        public string? BG { get; set; }

        [MaxLength(50)]
        public string? PG { get; set; }

        [MaxLength(100)]
        public string? VATNr { get; set; }

        public int? CompanyOldDbId { get; set; }

        [MaxLength(200)]
        public string? InvoiceMailSWE { get; set; }

        [MaxLength(200)]
        public string? InvoiceMailENG { get; set; }

        [MaxLength(1000)]
        public string? DocumentFileBasePath { get; set; }

        [MaxLength(200)]
        public string? Email { get; set; }

        [MaxLength(200)]
        public string? Web { get; set; }

        [MaxLength(5000)]
        public string? MailWrapper { get; set; }

        [MaxLength(500)]
        public string? GoogleApiKey { get; set; }

        public short? NrOfInquiryAnswerDays { get; set; }

        [MaxLength(255)]
        public string? VATInfo { get; set; }

        [MaxLength(50)]
        public string? SettingsCompany { get; set; }

        public int? InvoiceLastNr { get; set; }

        [MaxLength(255)]
        public string? DefaultCustomerMessage { get; set; }

        [MaxLength(255)]
        public string? EUText { get; set; }

        [MaxLength(255)]
        public string? ExportText { get; set; }

        public int? SettingsOldDbId { get; set; }
    }

    public class UpdateCompanySettingsRequest
    {
        [MaxLength(50)]
        public string? CompanyName { get; set; }

        [MaxLength(100)]
        public string? Address { get; set; }

        [MaxLength(100)]
        public string? PostalAddress { get; set; }

        [MaxLength(50)]
        public string? ZipCode { get; set; }

        [MaxLength(50)]
        public string? Telephone1 { get; set; }

        [MaxLength(50)]
        public string? Telephone2 { get; set; }

        [MaxLength(50)]
        public string? Fax1 { get; set; }

        [MaxLength(50)]
        public string? Fax2 { get; set; }

        [MaxLength(50)]
        public string? Bank { get; set; }

        [MaxLength(50)]
        public string? BIC { get; set; }

        [MaxLength(50)]
        public string? IBAN { get; set; }

        [MaxLength(50)]
        public string? BG { get; set; }

        [MaxLength(50)]
        public string? PG { get; set; }

        [MaxLength(100)]
        public string? VATNr { get; set; }

        public int? CompanyOldDbId { get; set; }

        [MaxLength(200)]
        public string? InvoiceMailSWE { get; set; }

        [MaxLength(200)]
        public string? InvoiceMailENG { get; set; }

        [MaxLength(1000)]
        public string? DocumentFileBasePath { get; set; }

        [MaxLength(200)]
        public string? Email { get; set; }

        [MaxLength(200)]
        public string? Web { get; set; }

        [MaxLength(5000)]
        public string? MailWrapper { get; set; }

        [MaxLength(500)]
        public string? GoogleApiKey { get; set; }

        public short? NrOfInquiryAnswerDays { get; set; }

        [MaxLength(255)]
        public string? VATInfo { get; set; }

        [MaxLength(50)]
        public string? SettingsCompany { get; set; }

        public int? InvoiceLastNr { get; set; }

        [MaxLength(255)]
        public string? DefaultCustomerMessage { get; set; }

        [MaxLength(255)]
        public string? EUText { get; set; }

        [MaxLength(255)]
        public string? ExportText { get; set; }

        public int? SettingsOldDbId { get; set; }
    }
}