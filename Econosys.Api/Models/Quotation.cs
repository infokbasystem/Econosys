using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("Quotation")]
    public class Quotation
    {
        [Key]
        [Column("lngQuotation_ID")]
        public int Id { get; set; }

        [Column("CompanyId")]
        public int CompanyId { get; set; }

        [Column("lngCalculation_ID")]
        public int? CalculationId { get; set; }

        [Column("lngInquiry_ID")]
        public int? InquiryId { get; set; }

        [Column("lngCustomer_ID")]
        public int? CustomerId { get; set; }

        [Column("strCustomer")]
        [MaxLength(50)]
        public string? CustomerName { get; set; }

        [Column("strAddress")]
        [MaxLength(255)]
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

        [Column("strDeliveryAddressName")]
        [MaxLength(50)]
        public string? DeliveryAddressName { get; set; }

        [Column("strDeliveryAddress")]
        [MaxLength(255)]
        public string? DeliveryAddress { get; set; }

        [Column("strDeliveryPostalNr")]
        [MaxLength(50)]
        public string? DeliveryPostalNr { get; set; }

        [Column("strDeliveryPostalAddress")]
        [MaxLength(50)]
        public string? DeliveryPostalAddress { get; set; }

        [Column("strDeliveryCountry")]
        [MaxLength(50)]
        public string? DeliveryCountry { get; set; }

        [Column("dteDate")]
        public DateTime? Date { get; set; }

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
        [MaxLength(100)]
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

        [Column("strTimeOfDelivery")]
        [MaxLength(50)]
        public string? TimeOfDelivery { get; set; }

        [Column("strTermsOfDelivery")]
        [MaxLength(255)]
        public string? TermsOfDelivery { get; set; }

        [Column("strTermsOfPayment")]
        [MaxLength(255)]
        public string? TermsOfPayment { get; set; }

        [Column("intPaymentDays")]
        public short? PaymentDays { get; set; }

        [Column("strMessage")]
        [MaxLength(255)]
        public string? Message { get; set; }

        [Column("lngSalesCurrency_ID")]
        public int? SalesCurrencyId { get; set; }

        [Column("lngUnit_ID")]
        public int? UnitId { get; set; }

        [Column("bolPartOfPrintEdition")]
        public bool PartOfPrintEdition { get; set; }

        [Column("dblPartOfPrintEdition")]
        public double? PartOfPrintEditionValue { get; set; }

        [Column("bolPartOfPunchEdition")]
        public bool PartOfPunchEdition { get; set; }

        [Column("dblPartOfPunchEdition")]
        public double? PartOfPunchEditionValue { get; set; }

        [Column("bolChangeOfSheet")]
        public bool ChangeOfSheet { get; set; }

        [Column("intNrOfChangeOfSheet")]
        public short? NrOfChangeOfSheet { get; set; }

        [Column("dblChangeOfSheet")]
        public double? ChangeOfSheetValue { get; set; }

        [Column("bolChangeOfCliche")]
        public bool ChangeOfCliche { get; set; }

        [Column("intNrOfChangeOfCliche")]
        public short? NrOfChangeOfCliche { get; set; }

        [Column("dblChangeOfCliche")]
        public double? ChangeOfClicheValue { get; set; }

        [Column("bolChangeOfColor")]
        public bool ChangeOfColor { get; set; }

        [Column("intNrOfChangeOfColor")]
        public short? NrOfChangeOfColor { get; set; }

        [Column("dblChangeOfColor")]
        public double? ChangeOfColorValue { get; set; }

        [Column("bolPMSColor")]
        public bool PmsColor { get; set; }

        [Column("dblPMSColor")]
        public double? PmsColorValue { get; set; }

        [Column("bolEURPallet")]
        public bool EurPallet { get; set; }

        [Column("intNrOfEURPallet")]
        public short? NrOfEurPallet { get; set; }

        [Column("dblEURPallet")]
        public double? EurPalletValue { get; set; }

        [Column("bolOtherCost")]
        public bool OtherCost { get; set; }

        [Column("strOtherCost")]
        [MaxLength(50)]
        public string? OtherCostName { get; set; }

        [Column("dblOtherCost")]
        public double? OtherCostValue { get; set; }

        [Column("dteCreated")]
        public DateTime? CreatedAt { get; set; }

        [Column("dteEdited")]
        public DateTime? EditedAt { get; set; }

        [Column("lngCreatedBy")]
        public int? CreatedBy { get; set; }

        [Column("lngEditedBy")]
        public int? EditedBy { get; set; }

        [Column("bolPrinted")]
        public bool Printed { get; set; }

        [Column("bolHideDeliveryAddressOnPrint")]
        public bool HideDeliveryAddressOnPrint { get; set; }

        [Column("strProductMessage")]
        [MaxLength(255)]
        public string? ProductMessage { get; set; }

        [Column("strNoteInternal")]
        [MaxLength(255)]
        public string? NoteInternal { get; set; }

        [Column("dblSalesCurrencyRate")]
        public double? SalesCurrencyRate { get; set; }

        [Column("lngPurchaseCurrency_ID")]
        public int? PurchaseCurrencyId { get; set; }

        [Column("dblPurchaseCurrencyRate")]
        public double? PurchaseCurrencyRate { get; set; }

        [Column("strAddress2")]
        [MaxLength(50)]
        public string? Address2 { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }

        [Column("bolAutoGenerated")]
        public bool AutoGenerated { get; set; }

        [Column("CustomerDeliveryAddressId")]
        public int? CustomerDeliveryAddressId { get; set; }

        [Column("PalletFormatId")]
        public int? PalletFormatId { get; set; }

        [Column("IsFSC")]
        public bool IsFsc { get; set; }

        [ForeignKey(nameof(CalculationId))]
        public Calculation? Calculation { get; set; }

        [ForeignKey(nameof(InquiryId))]
        public Inquiry? Inquiry { get; set; }

        [ForeignKey(nameof(CustomerId))]
        public Customer? Customer { get; set; }

        [ForeignKey(nameof(SalesCurrencyId))]
        public Currency? SalesCurrency { get; set; }

        [ForeignKey(nameof(UnitId))]
        public Unit? Unit { get; set; }

        [ForeignKey(nameof(CreatedBy))]
        public LegacyUser? CreatedByUser { get; set; }

        [ForeignKey(nameof(EditedBy))]
        public LegacyUser? EditedByUser { get; set; }

        [ForeignKey(nameof(PurchaseCurrencyId))]
        public Currency? PurchaseCurrency { get; set; }

        [ForeignKey(nameof(CustomerDeliveryAddressId))]
        public CustomerDeliveryAddress? CustomerDeliveryAddress { get; set; }

        [ForeignKey(nameof(PalletFormatId))]
        public PalletFormat? PalletFormat { get; set; }

        public ICollection<QuotationRow> QuotationRows { get; set; } = new List<QuotationRow>();

        public ICollection<OrderCost> OrderCosts { get; set; } = new List<OrderCost>();
    }
}
