using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("Calculation")]
    public class Calculation
    {
        [Key]
        [Column("lngCalculation_ID")]
        public int Id { get; set; }

        [Column("lngConnectedToCalculation_ID")]
        public int? ConnectedToCalculationId { get; set; }

        [Column("lngProduct_ID")]
        public int? ProductId { get; set; }

        [Column("lngSupplier_ID")]
        public int? SupplierId { get; set; }

        [Column("lngCustomer_ID")]
        public int? CustomerId { get; set; }

        [Column("strNote")]
        [MaxLength(50)]
        public string? Note { get; set; }

        [Column("lngUnit_ID")]
        public int? UnitId { get; set; }

        [Column("lngCurrentInquiry_ID")]
        public int? CurrentInquiryId { get; set; }

        [Column("lngPalletFormat_ID")]
        public int? PalletFormatId { get; set; }

        [Column("dblFreightPerPallet")]
        public double? FreightPerPallet { get; set; }

        [Column("dblFreightPerVehicle")]
        public double? FreightPerVehicle { get; set; }

        [Column("dblLoadingPerDelivery")]
        public double? LoadingPerDelivery { get; set; }

        [Column("dblLoadingPerPallet")]
        public double? LoadingPerPallet { get; set; }

        [Column("dblStoragePerM2")]
        public double? StoragePerM2 { get; set; }

        [Column("dblInterest")]
        public double? Interest { get; set; }

        [Column("dblFreightFromStockPerPallet")]
        public double? FreightFromStockPerPallet { get; set; }

        [Column("lngPurchaseCurrency_ID")]
        public int? PurchaseCurrencyId { get; set; }

        [Column("dblPurchaseCurrencyRate")]
        public double? PurchaseCurrencyRate { get; set; }

        [Column("lngSalesCurrency_ID")]
        public int? SalesCurrencyId { get; set; }

        [Column("dblSalesCurrencyRate")]
        public double? SalesCurrencyRate { get; set; }

        [Column("dblCurrencyRate")]
        public double? CurrencyRate { get; set; }

        [Column("strLoadingInstruction")]
        [MaxLength(255)]
        public string? LoadingInstruction { get; set; }

        [Column("strCalculationFollowUp")]
        [MaxLength(255)]
        public string? CalculationFollowUp { get; set; }

        [Column("strOtherCostNote")]
        [MaxLength(1000)]
        public string? OtherCostNote { get; set; }

        [Column("lngResponsibleUser_ID")]
        public int? ResponsibleUserId { get; set; }

        [Column("dteCreated")]
        public DateTime? CreatedAt { get; set; }

        [Column("dteEdited")]
        public DateTime? EditedAt { get; set; }

        [Column("lngCreatedBy")]
        public int? CreatedBy { get; set; }

        [Column("lngEditedBy")]
        public int? EditedBy { get; set; }

        [Column("bolCalcStorage")]
        public bool CalcStorage { get; set; }

        [Column("FreightApprovedBy")]
        [MaxLength(255)]
        public string? FreightApprovedBy { get; set; }

        [Column("FreightApprovedDateTime")]
        public DateTime? FreightApprovedDateTime { get; set; }

        [Column("OtherCostApprovedBy")]
        [MaxLength(255)]
        public string? OtherCostApprovedBy { get; set; }

        [Column("OtherCostApprovedDateTime")]
        public DateTime? OtherCostApprovedDateTime { get; set; }

        [Column("StorageApprovedBy")]
        [MaxLength(255)]
        public string? StorageApprovedBy { get; set; }

        [Column("StorageApprovedDateTime")]
        public DateTime? StorageApprovedDateTime { get; set; }

        [Column("LoadingInstructionApprovedBy")]
        [MaxLength(255)]
        public string? LoadingInstructionApprovedBy { get; set; }

        [Column("LoadingInstructionApprovedDateTime")]
        public DateTime? LoadingInstructionApprovedDateTime { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }

        [Column("FreightNote")]
        [MaxLength(255)]
        public string? FreightNote { get; set; }

        [Column("NrOfCalcDecimalsPurchase")]
        public int? NrOfCalcDecimalsPurchase { get; set; }

        [Column("NrOfCalcDecimalsSales")]
        public int? NrOfCalcDecimalsSales { get; set; }

        [Column("PalletIsStackable")]
        public bool PalletIsStackable { get; set; }

        [Column("PalletWidth")]
        public int? PalletWidth { get; set; }

        [Column("PalletHeight")]
        public int? PalletHeight { get; set; }

        [Column("PalletLength")]
        public int? PalletLength { get; set; }

        [Column("EditionPerPallet")]
        public int? EditionPerPallet { get; set; }

        [Column("PalletCalcFactor", TypeName = "decimal(5,3)")]
        public decimal? PalletCalcFactor { get; set; }

        [Column("PalletsPerTruck")]
        public int? PalletsPerTruck { get; set; }

        [Column("PalletType")]
        public int? PalletType { get; set; }

        [Column("AutoFreightCalc")]
        public bool AutoFreightCalc { get; set; }

        [Column("LockOutPrice")]
        public bool LockOutPrice { get; set; }

        [Column("AutoFreightPrice")]
        public bool AutoFreightPrice { get; set; }

        [Column("SupplierFactoryId")]
        public int? SupplierFactoryId { get; set; }

        [Column("CustomerDeliveryAddressId")]
        public int? CustomerDeliveryAddressId { get; set; }

        [Column("DeliveryAddressCountryCode")]
        [MaxLength(50)]
        public string? DeliveryAddressCountryCode { get; set; }

        [Column("DeliveryAddressPostalNr")]
        public int? DeliveryAddressPostalNr { get; set; }

        [Column("BlockOrderCalculationId")]
        public int? BlockOrderCalculationId { get; set; }

        [Column("PalletPriceIn", TypeName = "decimal(13,5)")]
        public decimal? PalletPriceIn { get; set; }

        [Column("PalletPriceOut", TypeName = "decimal(13,5)")]
        public decimal? PalletPriceOut { get; set; }

        [Column("DoDebitPalletPrice")]
        public bool DoDebitPalletPrice { get; set; }

        [Column("PackagingApprovedBy")]
        [MaxLength(255)]
        public string? PackagingApprovedBy { get; set; }

        [Column("PackagingApprovedDateTime")]
        public DateTime? PackagingApprovedDateTime { get; set; }

        [Column("DeactivateFreightCalculation")]
        public bool DeactivateFreightCalculation { get; set; }

        [Column("AutomaticLoadingInstruction")]
        public bool AutomaticLoadingInstruction { get; set; }

        [Column("FreightUpdatedDateTime")]
        public DateTime? FreightUpdatedDateTime { get; set; }

        [Column("NrOfPerBundle")]
        public int? NrOfPerBundle { get; set; }

        [Column("NrOfPerBundlePerPallet")]
        public int? NrOfPerBundlePerPallet { get; set; }

        [Column("NrOfPerOuterPackaging")]
        public int? NrOfPerOuterPackaging { get; set; }

        [Column("NrOfOuterPackagingPerPallet")]
        public int? NrOfOuterPackagingPerPallet { get; set; }

        [Column("DoBuntas")]
        public bool DoBuntas { get; set; }

        [Column("IsYtterforpackning")]
        public bool IsYtterforpackning { get; set; }

        public Product? Product { get; set; }
        public Supplier? Supplier { get; set; }
        public Customer? Customer { get; set; }
        public Unit? Unit { get; set; }
        public PalletFormat? PalletFormat { get; set; }
        public Currency? PurchaseCurrency { get; set; }
        public Currency? SalesCurrency { get; set; }
        public ICollection<CalculationRow> CalculationRows { get; set; } = new List<CalculationRow>();
    }
}
