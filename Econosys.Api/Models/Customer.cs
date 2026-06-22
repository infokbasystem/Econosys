using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("Customer")]
    public class Customer
    {
        [Key]
        [Column("lngCustomer_ID")]
        public int Id { get; set; }

        [Column("strCustomer")]
        [MaxLength(50)]
        public string? Name { get; set; }

        [Column("strSortName")]
        [MaxLength(50)]
        public string? SortName { get; set; }

        [Column("strAddress")]
        [MaxLength(50)]
        public string? Address { get; set; }

        [Column("strPostalNr")]
        [MaxLength(50)]
        public string? PostalNr { get; set; }

        [Column("strPostalAddress")]
        [MaxLength(50)]
        public string? PostalAddress { get; set; }

        [Column("strVisitingAddress")]
        [MaxLength(50)]
        public string? VisitingAddress { get; set; }

        [Column("strReference")]
        [MaxLength(50)]
        public string? Reference { get; set; }

        [Column("strTelephone1")]
        [MaxLength(50)]
        public string? Telephone1 { get; set; }

        [Column("strTelephone2")]
        [MaxLength(50)]
        public string? Telephone2 { get; set; }

        [Column("strTelephone3")]
        [MaxLength(50)]
        public string? Telephone3 { get; set; }

        [Column("strFax")]
        [MaxLength(50)]
        public string? Fax { get; set; }

        [Column("strNote")]
        [MaxLength(255)]
        public string? Note { get; set; }

        [Column("strLoadingInstruction")]
        [MaxLength(255)]
        public string? LoadingInstruction { get; set; }

        [Column("lngResponsibleUser_ID")]
        public int? ResponsibleUserId { get; set; }

        [Column("bolVAT")]
        public bool VAT { get; set; }

        [Column("bolActive")]
        public bool Active { get; set; }

        [Column("strCountry")]
        [MaxLength(50)]
        public string? Country { get; set; }

        [Column("strTermsOfDelivery")]
        [MaxLength(255)]
        public string? TermsOfDelivery { get; set; }

        [Column("strTermsOfPayment")]
        [MaxLength(255)]
        public string? TermsOfPayment { get; set; }

        [Column("intPaymentDays")]
        public short? PaymentDays { get; set; }

        [Column("lngLanguage_ID")]
        public int? LanguageId { get; set; }

        [Column("strVATNr")]
        [MaxLength(20)]
        public string? VATNr { get; set; }

        [Column("strEmail")]
        [MaxLength(100)]
        public string? Email { get; set; }

        [Column("lngQuotationCommunicationType_ID")]
        public int? QuotationCommunicationTypeId { get; set; }

        [Column("lngCustomerOrderCommunicationType_ID")]
        public int? CustomerOrderCommunicationTypeId { get; set; }

        [Column("lngInvoiceCommunicationType_ID")]
        public int? InvoiceCommunicationTypeId { get; set; }

        [Column("strOrgNr")]
        [MaxLength(50)]
        public string? OrgNr { get; set; }

        [Column("bolEU")]
        public bool EU { get; set; }

        [Column("bolExport")]
        public bool Export { get; set; }

        [Column("lngCreditLimit")]
        public int? CreditLimit { get; set; }

        [Column("lngCurrency_ID")]
        public int? CurrencyId { get; set; }

        [Column("strExternalKey")]
        [MaxLength(50)]
        public string? ExternalKey { get; set; }

        [Column("intVATRate")]
        public short? VATRate { get; set; }

        [Column("strAddress2")]
        [MaxLength(50)]
        public string? Address2 { get; set; }

        [Column("bolOneWayPallet")]
        public bool OneWayPallet { get; set; }

        [Column("bolEURPallet")]
        public bool EURPallet { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }

        [Column("SpecialUnitHandling")]
        public bool SpecialUnitHandling { get; set; }

        [Column("PrintForDocumentScanning")]
        public bool PrintForDocumentScanning { get; set; }

        [Column("PricePerEurPallet")]
        public decimal? PricePerEurPallet { get; set; }

        [Column("LockOrder")]
        public bool LockOrder { get; set; }

        [Column("LastCustomerOrderDate")]
        public DateTime? LastCustomerOrderDate { get; set; }

        [Column("AccountNrAccountsReceivable")]
        public int? AccountNrAccountsReceivable { get; set; }

        [Column("AccountNrEarnings")]
        public int? AccountNrEarnings { get; set; }

        [Column("SupportEmployeeId")]
        public int? SupportEmployeeId { get; set; }

        [Column("LastActivity")]
        [MaxLength(250)]
        public string? LastActivity { get; set; }

        [Column("CountryID")]
        public int? CountryId { get; set; }

        [Column("Category")]
        [MaxLength(50)]
        public string? Category { get; set; }

        [Column("IsProspect")]
        public bool IsProspect { get; set; }

        [Column("InvoicePalletsSeparately")]
        public bool InvoicePalletsSeparately { get; set; }

        [Column("InvoiceCostsSeparately")]
        public bool InvoiceCostsSeparately { get; set; }

        [Column("InvoiceCostsSeparatelyImmediately")]
        public bool InvoiceCostsSeparatelyImmediately { get; set; }

        [Column("BudgetCountAsNewUntilMonth")]
        public DateOnly? BudgetCountAsNewUntilMonth { get; set; }

        [Column("InvoiceRowsInProductNameOrder")]
        public bool InvoiceRowsInProductNameOrder { get; set; }

        [Column("OrderNrPrefix")]
        [MaxLength(50)]
        public string? OrderNrPrefix { get; set; }

        // Navigation properties
        public virtual Language? Language { get; set; }
        public virtual Currency? Currency { get; set; }
        public virtual LegacyUser? ResponsibleUser { get; set; }
        public virtual LegacyUser? SupportEmployee { get; set; }
        public virtual ICollection<CustomerOrder> CustomerOrders { get; set; } = new List<CustomerOrder>();
        public virtual ICollection<SupplierOrder> SupplierOrders { get; set; } = new List<SupplierOrder>();
        public virtual ICollection<CustomerContactPerson> CustomerContactPersons { get; set; } = new List<CustomerContactPerson>();
        public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
        public virtual ICollection<Calculation> Calculations { get; set; } = new List<Calculation>();
        public virtual ICollection<Deviation> Deviations { get; set; } = new List<Deviation>();
        public virtual ICollection<Quotation> Quotations { get; set; } = new List<Quotation>();
        public virtual ICollection<CustomerDeliveryAddress> DeliveryAddresses { get; set; } = new List<CustomerDeliveryAddress>();
        public virtual ICollection<PalletFormatPrice> PalletFormatPrices { get; set; } = new List<PalletFormatPrice>();
    }
}
