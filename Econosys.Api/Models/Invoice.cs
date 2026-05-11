using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("Invoice")]
    public class Invoice
    {
        [Key]
        [Column("lngInvoice_ID")]
        public int Id { get; set; }

        [Column("lngInvoiceType_ID")]
        public InvoiceTypeCode? InvoiceTypeCode { get; set; }

        [Column("lngCustomerOrder_ID")]
        public int? CustomerOrderId { get; set; }

        [Column("lngDeliveryToCustomer_ID")]
        public int? DeliveryToCustomerId { get; set; }

        [Column("lngDeliveryFromStock_ID")]
        public int? DeliveryFromStockId { get; set; }

        [Column("lngInvoiceNr")]
        public int? InvoiceNumber { get; set; }

        [Column("lngCreditingInvoiceNr")]
        public int? CreditingInvoiceNumber { get; set; }

        [Column("lngCustomer_ID")]
        public int? CustomerId { get; set; }

        [Column("strCustomer")]
        [MaxLength(50)]
        public string? CustomerName { get; set; }

        [Column("strAddress")]
        [MaxLength(50)]
        public string? Address { get; set; }

        [Column("strPostalNr")]
        [MaxLength(50)]
        public string? PostalNr { get; set; }

        [Column("strPostalAddress")]
        [MaxLength(50)]
        public string? PostalAddress { get; set; }

        [Column("strCountry")]
        [MaxLength(50)]
        public string? Country { get; set; }

        [Column("strVATNr")]
        [MaxLength(20)]
        public string? VatNr { get; set; }

        [Column("dteInvoiceDate")]
        public DateTime? InvoiceDate { get; set; }

        [Column("strYourReference")]
        [MaxLength(50)]
        public string? YourReference { get; set; }

        [Column("strOurReference")]
        [MaxLength(50)]
        public string? OurReference { get; set; }

        [Column("strProduct")]
        [MaxLength(50)]
        public string? Product { get; set; }

        [Column("strMaterial")]
        [MaxLength(50)]
        public string? Material { get; set; }

        [Column("strMaterialThickness")]
        [MaxLength(50)]
        public string? MaterialThickness { get; set; }

        [Column("strFormat")]
        [MaxLength(50)]
        public string? Format { get; set; }

        [Column("strColor")]
        [MaxLength(50)]
        public string? Color { get; set; }

        [Column("strConstruction")]
        [MaxLength(50)]
        public string? Construction { get; set; }

        [Column("intInvoiceDays")]
        public short? InvoiceDays { get; set; }

        [Column("strTermsOfPayment")]
        [MaxLength(255)]
        public string? TermsOfPayment { get; set; }

        [Column("lngSalesCurrency_ID")]
        public int? SalesCurrencyId { get; set; }

        [Column("lngUnit_ID")]
        public int? UnitId { get; set; }

        [Column("dteAccountDate")]
        public DateTime? AccountDate { get; set; }

        [Column("lngJournalNr")]
        public int? JournalNr { get; set; }

        [Column("strAccount")]
        [MaxLength(50)]
        public string? Account { get; set; }

        [Column("strCostCenter")]
        [MaxLength(50)]
        public string? CostCenter { get; set; }

        [Column("bolInvoiceOK")]
        public bool InvoiceOk { get; set; }

        [Column("bolEndInvoiced")]
        public bool EndInvoiced { get; set; }

        [Column("bolPrinted")]
        public bool Printed { get; set; }

        [Column("dteCreated")]
        public DateTime? Created { get; set; }

        [Column("dteEdited")]
        public DateTime? Edited { get; set; }

        [Column("lngCreatedBy")]
        public int? CreatedBy { get; set; }

        [Column("lngEditedBy")]
        public int? EditedBy { get; set; }

        [Column("strYourOrderNr")]
        [MaxLength(300)]
        public string? YourOrderNr { get; set; }

        [Column("bolCollectInvoice")]
        public bool CollectInvoice { get; set; }

        [Column("strOurOrderNrFreeInvoice")]
        [MaxLength(300)]
        public string? OurOrderNrFreeInvoice { get; set; }

        [Column("strTimeOfDelivery")]
        [MaxLength(50)]
        public string? TimeOfDelivery { get; set; }

        [Column("strAddress2")]
        [MaxLength(50)]
        public string? Address2 { get; set; }

        [Column("IsSettled")]
        public bool IsSettled { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }

        [Column("NoteInternal")]
        [MaxLength(255)]
        public string? NoteInternal { get; set; }

        [Column("LanguageId")]
        public int? LanguageId { get; set; }

        [Column("SalesCurrencyRate", TypeName = "decimal(10,5)")]
        public decimal? SalesCurrencyRate { get; set; }

        [Column("InventoryCurrencyRate", TypeName = "decimal(10,5)")]
        public decimal? InventoryCurrencyRate { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public Customer? Customer { get; set; }

        [ForeignKey(nameof(SalesCurrencyId))]
        public Currency? SalesCurrency { get; set; }

        [ForeignKey(nameof(UnitId))]
        public Unit? Unit { get; set; }

        [ForeignKey(nameof(LanguageId))]
        public Language? Language { get; set; }

        public ICollection<InvoiceRow> InvoiceRows { get; set; } = new List<InvoiceRow>();
        public ICollection<InvoiceAccountRow> InvoiceAccountRows { get; set; } = new List<InvoiceAccountRow>();
    }
}
