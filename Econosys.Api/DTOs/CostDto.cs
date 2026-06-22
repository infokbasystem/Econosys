namespace Econosys.Api.DTOs
{
    public class CostDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsNrOf { get; set; }
        public bool IsCalculation { get; set; }
        public bool IsSupplier { get; set; }
        public int? AccountDomestic { get; set; }
        public int? AccountEU { get; set; }
        public int? AccountExport { get; set; }
        public int TranslationCodeSupplierOrderUnknown { get; set; }
        public int TranslationCodeSupplierOrderKnown { get; set; }
        public int TranslationCodeQuotationUnknown { get; set; }
        public int TranslationCodeQuotationKnown { get; set; }
        public int TranslationCodeCustomerOrderUnknown { get; set; }
        public int TranslationCodeCustomerOrderKnown { get; set; }
        public int TranslationCodeInvoiceRow { get; set; }
        public bool IsDebitDefault { get; set; }
        public bool DoPrintSupplierOrderDefault { get; set; }
        public bool DoPrintQuotationDefault { get; set; }
        public bool DoPrintCustomerOrderDefault { get; set; }
        public bool IsCustomerDefault { get; set; }
        public bool DoPrintScrapToolsTextOnCustomerOrder { get; set; }
        public bool AddAutomaicIfEconopackIsTransportReponsible { get; set; }
        public decimal? DmtPercent { get; set; }
        public decimal? DmtFixed { get; set; }
        public decimal? ProvisionPercent { get; set; }
        public string CostTypeText { get; set; } = string.Empty;
    }

    public class CreateCostRequest
    {
        [System.ComponentModel.DataAnnotations.MaxLength(50)]
        public string? Name { get; set; }
        public bool IsActive { get; set; }
        public bool IsNrOf { get; set; }
        public bool IsCalculation { get; set; }
        public bool IsSupplier { get; set; }
        public int? AccountDomestic { get; set; }
        public int? AccountEU { get; set; }
        public int? AccountExport { get; set; }
        public int? TranslationCodeSupplierOrderUnknown { get; set; }
        public int? TranslationCodeSupplierOrderKnown { get; set; }
        public int? TranslationCodeQuotationUnknown { get; set; }
        public int? TranslationCodeQuotationKnown { get; set; }
        public int? TranslationCodeCustomerOrderUnknown { get; set; }
        public int? TranslationCodeCustomerOrderKnown { get; set; }
        public int? TranslationCodeInvoiceRow { get; set; }
        public bool IsDebitDefault { get; set; }
        public bool DoPrintSupplierOrderDefault { get; set; }
        public bool DoPrintQuotationDefault { get; set; }
        public bool DoPrintCustomerOrderDefault { get; set; }
        public bool IsCustomerDefault { get; set; }
        public bool DoPrintScrapToolsTextOnCustomerOrder { get; set; }
        public bool AddAutomaicIfEconopackIsTransportReponsible { get; set; }
        public decimal? DmtPercent { get; set; }
        public decimal? DmtFixed { get; set; }
        public decimal? ProvisionPercent { get; set; }
        [System.ComponentModel.DataAnnotations.MaxLength(50)]
        public string? CostTypeText { get; set; }
    }

    public class UpdateCostRequest
    {
        [System.ComponentModel.DataAnnotations.MaxLength(50)]
        public string? Name { get; set; }
        public bool? IsActive { get; set; }
        public bool? IsNrOf { get; set; }
        public bool? IsCalculation { get; set; }
        public bool? IsSupplier { get; set; }
        public int? AccountDomestic { get; set; }
        public int? AccountEU { get; set; }
        public int? AccountExport { get; set; }
        public int? TranslationCodeSupplierOrderUnknown { get; set; }
        public int? TranslationCodeSupplierOrderKnown { get; set; }
        public int? TranslationCodeQuotationUnknown { get; set; }
        public int? TranslationCodeQuotationKnown { get; set; }
        public int? TranslationCodeCustomerOrderUnknown { get; set; }
        public int? TranslationCodeCustomerOrderKnown { get; set; }
        public int? TranslationCodeInvoiceRow { get; set; }
        public bool? IsDebitDefault { get; set; }
        public bool? DoPrintSupplierOrderDefault { get; set; }
        public bool? DoPrintQuotationDefault { get; set; }
        public bool? DoPrintCustomerOrderDefault { get; set; }
        public bool? IsCustomerDefault { get; set; }
        public bool? DoPrintScrapToolsTextOnCustomerOrder { get; set; }
        public bool? AddAutomaicIfEconopackIsTransportReponsible { get; set; }
        public decimal? DmtPercent { get; set; }
        public decimal? DmtFixed { get; set; }
        public decimal? ProvisionPercent { get; set; }
        [System.ComponentModel.DataAnnotations.MaxLength(50)]
        public string? CostTypeText { get; set; }
    }
}