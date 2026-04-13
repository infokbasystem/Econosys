using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("CustomerOrder")]
    public class CustomerOrder
    {
        [Key]
        [Column("lngCustomerOrder_ID")]
        public int Id { get; set; }

        [Column("lngQuotation_ID")]
        public int? QuotationId { get; set; }

        [Column("lngSupplierOrder_ID")]
        public int? SupplierOrderId { get; set; }

        [Column("strCustomerOrder")]
        [MaxLength(50)]
        public string? CustomerOrderNr { get; set; }

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

        [Column("strGoodsMarking")]
        [MaxLength(50)]
        public string? GoodsMarking { get; set; }

        [Column("lngSalesCurrency_ID")]
        public int? SalesCurrencyId { get; set; }

        [Column("lngUnit_ID")]
        public int? UnitId { get; set; }

        [Column("lngSelectedCalculationRow_ID")]
        public int? SelectedCalculationRowId { get; set; }

        [Column("lngEdition")]
        public int? Edition { get; set; }

        [Column("dblSalesPrice")]
        public double? SalesPrice { get; set; }

        [Column("bolOrderConfirmationPrinted")]
        public bool OrderConfirmationPrinted { get; set; }

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
        public double? ChangeOfClichemValue { get; set; }

        [Column("bolChangeOfColor")]
        public bool ChangeOfColor { get; set; }

        [Column("intNrOfChangeOfColor")]
        public short? NrOfChangeOfColor { get; set; }

        [Column("dblChangeOfColor")]
        public double? ChangeOfColorValue { get; set; }

        [Column("bolPMSColor")]
        public bool PMSColor { get; set; }

        [Column("dblPMSColor")]
        public double? PMSColorValue { get; set; }

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
        public string? OtherCostDescription { get; set; }

        [Column("dblOtherCost")]
        public double? OtherCostValue { get; set; }

        [Column("dteCreated")]
        public DateTime? Created { get; set; }

        [Column("dteEdited")]
        public DateTime? Edited { get; set; }

        [Column("lngCreatedBy")]
        public int? CreatedBy { get; set; }

        [Column("lngEditedBy")]
        public int? EditedBy { get; set; }

        [Column("strYourOrderNr")]
        [MaxLength(50)]
        public string? YourOrderNr { get; set; }

        [Column("bolCompleted")]
        public bool Completed { get; set; }

        [Column("strProductMessage")]
        [MaxLength(255)]
        public string? ProductMessage { get; set; }

        [Column("dblSalesCurrencyRate")]
        public double? SalesCurrencyRate { get; set; }

        [Column("strCustomer2")]
        [MaxLength(50)]
        public string? Customer2 { get; set; }

        [Column("strAddress2")]
        [MaxLength(50)]
        public string? Address2 { get; set; }

        public int? OldDbId { get; set; }

        [Column("lngResponsibleUser_ID")]
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

        [Column(TypeName = "decimal(13,5)")]
        public decimal? SupplierPricePerEurPallet { get; set; }

        public int? SupplierPricePerEurPalletCurrencyId { get; set; }

        [Column(TypeName = "decimal(13,5)")]
        public decimal? SupplierPricePerEurPalletCurrencyRate { get; set; }

        public int? PalletFormatId { get; set; }

        public bool IsFSC { get; set; }

        [Column(TypeName = "decimal(18,5)")]
        public decimal? TotalCostInSalesCurrency { get; set; }

        public string? InvoicingInfo { get; set; }

        public int? CalculationId { get; set; }

        public virtual Customer? Customer { get; set; }
        public virtual Currency? SalesCurrency { get; set; }
        public virtual Currency? SupplierPricePerEurPalletCurrency { get; set; }
        public virtual Unit? Unit { get; set; }
        public virtual SupplierOrder? SupplierOrder { get; set; }
    }
}
