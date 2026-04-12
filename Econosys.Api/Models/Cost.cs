using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("Cost")]
    public class Cost
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(50)]
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

        [Column(TypeName = "decimal(5,2)")]
        public decimal? ProvisionPercent { get; set; }

        [MaxLength(50)]
        public string CostTypeText { get; set; } = string.Empty;
    }
}
