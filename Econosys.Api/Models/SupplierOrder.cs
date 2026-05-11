using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("SupplierOrder")]
    public class SupplierOrder
    {
        [Key]
        [Column("lngSupplierOrder_ID")]
        public int Id { get; set; }

        [MaxLength(50)]
        public string? SupplierOrderNr { get; set; }

        [Column("lngBasedOnSupplierOrder_ID")]
        public int? BasedOnSupplierOrderId { get; set; }

        [Column("lngInquiry_ID")]
        public int? InquiryId { get; set; }

        public int? QuotationId { get; set; }

        [Column("lngInquiryOld_ID")]
        public int? InquiryOldId { get; set; }

        [Column("lngSupplier_ID")]
        public int? SupplierId { get; set; }

        [Column("strSupplier")]
        [MaxLength(50)]
        public string? SupplierName { get; set; }

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

        [Column("lngCustomer_ID")]
        public int? CustomerId { get; set; }

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
        [MaxLength(50)]
        public string? TermsOfDelivery { get; set; }

        [Column("strTermsOfPayment")]
        [MaxLength(50)]
        public string? TermsOfPayment { get; set; }

        [Column("strMessage")]
        [MaxLength(255)]
        public string? Message { get; set; }

        [Column("strGoodsMarking")]
        [MaxLength(50)]
        public string? GoodsMarking { get; set; }

        [Column("strPreparation")]
        [MaxLength(50)]
        public string? Preparation { get; set; }

        [Column("lngUnit_ID")]
        public int? UnitId { get; set; }

        [Column("lngSelectedCalculationRow_ID")]
        public int? SelectedCalculationRowId { get; set; }

        [Column("lngEdition")]
        public int? Edition { get; set; }

        [Column("dblPurchasePrice")]
        public double? PurchasePrice { get; set; }

        [Column("lngPurchaseCurrency_ID")]
        public int? PurchaseCurrencyId { get; set; }

        [Column("bolConfirmed")]
        public bool Confirmed { get; set; }

        [Column("bolPuchDrawingAccepted")]
        public bool PuchDrawingAccepted { get; set; }

        [Column("bolPrintBasisAccepted")]
        public bool PrintBasisAccepted { get; set; }

        [Column("bolHideCustomerInfoOnPrint")]
        public bool HideCustomerInfoOnPrint { get; set; }

        [Column("dteCreated")]
        public DateTime? Created { get; set; }

        [Column("dteEdited")]
        public DateTime? Edited { get; set; }

        [Column("lngCreatedBy")]
        public int? CreatedBy { get; set; }

        [Column("lngEditedBy")]
        public int? EditedBy { get; set; }

        [Column("strProductCode")]
        [MaxLength(50)]
        public string? ProductCode { get; set; }

        [Column("dteConfirmedDeliveryDate")]
        public DateTime? ConfirmedDeliveryDate { get; set; }

        [Column("dblPurchaseCurrencyRate")]
        public double? PurchaseCurrencyRate { get; set; }

        [Column("strCustomer2")]
        [MaxLength(50)]
        public string? Customer2 { get; set; }

        [Column("strAddress2")]
        [MaxLength(50)]
        public string? Address2 { get; set; }

        [Column("bolOneWayPallet")]
        public bool OneWayPallet { get; set; }

        [Column("bolEURPallet")]
        public bool EurPallet { get; set; }

        [Column("lngInventoryId")]
        public int? InventoryId { get; set; }

        [Column("LoadingInstruction")]
        [MaxLength(255)]
        public string? LoadingInstruction { get; set; }

        public int? OldDbId { get; set; }

        public bool IsNonStopPallet { get; set; }

        public int? SupplierFactoryId { get; set; }

        [MaxLength(100)]
        public string? CustomerOrderNr { get; set; }

        public bool EconopackTransportResponsible { get; set; }

        public bool ConfirmedDeliveryDateWeekMode { get; set; }

        public int? ProducedEdition { get; set; }

        public DateTime? DeliveryDate { get; set; }

        public bool DeliveryDateWeekMode { get; set; }

        public int? CustomerDeliveryAddressId { get; set; }

        public bool IsParcelDelivery { get; set; }

        public int? PalletFormatId { get; set; }

        public int? BlockOrderCalculationId { get; set; }

        public bool HideCustomerNameOnPrint { get; set; }

        public bool HideProductNameOnPrint { get; set; }

        public bool IsFSC { get; set; }

        public int? EditionTakenFromSupplierOrderId { get; set; }

        public DateTime? EmailSentDateTime { get; set; }

        [MaxLength(50)]
        public string? PackagingType { get; set; }

        public int? CalculationId { get; set; }

        // Navigation properties
        public virtual Customer? Customer { get; set; }
        public virtual Supplier? Supplier { get; set; }
        public virtual Currency? PurchaseCurrency { get; set; }
        public virtual Inventory? Inventory { get; set; }
        public virtual Unit? Unit { get; set; }
        public virtual CalculationRow? SelectedCalculationRow { get; set; }
        public virtual ICollection<CustomerOrder> CustomerOrders { get; set; } = new List<CustomerOrder>();
        public virtual ICollection<StockTakingItem> StockTakingItems { get; set; } = new List<StockTakingItem>();
        public virtual ICollection<DeliveryToCustomer> DeliveryToCustomers { get; set; } = new List<DeliveryToCustomer>();
        public virtual ICollection<DeliveryToStock> DeliveryToStocks { get; set; } = new List<DeliveryToStock>();
        public virtual ICollection<OrderCost> OrderCosts { get; set; } = new List<OrderCost>();
        public virtual ICollection<DocumentFile> DocumentFiles { get; set; } = new List<DocumentFile>();
        public virtual ICollection<DocumentFileRelation> DocumentFileRelations { get; set; } = new List<DocumentFileRelation>();
    }
}