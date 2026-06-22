using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Econosys.Api.Models
{
    [Table("CalculationRow")]
    public class CalculationRow
    {
        [Key]
        [Column("lngCalculationRow_ID")]
        public int Id { get; set; }

        [Column("lngCalculation_ID")]
        public int? CalculationId { get; set; }

        [Column("lngEdition")]
        public int? Edition { get; set; }

        [Column("dblCalculationPrice")]
        public double? CalculationPrice { get; set; }

        [Column("dblPurchasePrice")]
        public double? PurchasePrice { get; set; }

        [Column("dblFreight")]
        public double? Freight { get; set; }

        [Column("dblOther")]
        public double? Other { get; set; }

        [Column("dblStorage")]
        public double? Storage { get; set; }

        [Column("dblPercentAddition")]
        public double? PercentAddition { get; set; }

        [Column("dblSalesPrice")]
        public double? SalesPrice { get; set; }

        [Column("intNrOfPalletsPopup")]
        public double? NrOfPalletsPopup { get; set; }

        [Column("intNrOfVehiclesPopup")]
        public double? NrOfVehiclesPopup { get; set; }

        [Column("intNrOfMonthsTotalPopup")]
        public double? NrOfStorageMonths { get; set; }

        [Column("dblFreightPopup")]
        public double? FreightPopup { get; set; }

        [Column("dblFreightExtra10KmPopup")]
        public double? FreightExtra10KmPopup { get; set; }

        [Column("dblFreightUnloadingPopup")]
        public double? FreightUnloadingPopup { get; set; }

        [Column("dblOtherPopup")]
        public double? OtherPopup { get; set; }

        [Column("dblStorageUnloadingPopup")]
        public double? StorageUnloadingPopup { get; set; }

        [Column("dblStoragePopup")]
        public double? StoragePopup { get; set; }

        [Column("dblStorageFreightToCustomerPopup")]
        public double? StorageFreightToCustomerPopup { get; set; }

        [Column("dblStorageInterestPopup")]
        public double? StorageInterestPopup { get; set; }

        [Column("bolArchived")]
        public bool Archived { get; set; }

        [Column("dblFreightCostPerPalletDomesticPopup")]
        public double? FreightCostPerPalletDomesticPopup { get; set; }

        [Column("dblFreightCostPerPalletInternationalPopup")]
        public double? FreightCostPerPalletInternationalPopup { get; set; }

        [Column("dblLoadingCostPerPallet")]
        public double? LoadingCostPerPallet { get; set; }

        [Column("OldDbId")]
        public int? OldDbId { get; set; }

        [Column("ArchivedPurchaseCurrencyRate", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPurchaseCurrencyRate { get; set; }

        [Column("ArchivedSalesCurrencyRate", TypeName = "decimal(13,5)")]
        public decimal? ArchivedSalesCurrencyRate { get; set; }

        [Column("StorageNrOfPallets")]
        public int? StorageNrOfPallets { get; set; }

        [Column("CustomerDeliveryAddressId")]
        public int? CustomerDeliveryAddressId { get; set; }

        [Column("FreightCostPerFtlInternationalPopup", TypeName = "decimal(13,5)")]
        public decimal? FreightCostPerFtlInternationalPopup { get; set; }

        [Column("ArchivedAutoFreightCalc")]
        public bool ArchivedAutoFreightCalc { get; set; }

        [Column("ArchivedAutoFreightPrice")]
        public bool ArchivedAutoFreightPrice { get; set; }

        [Column("ArchivedPalletWidth")]
        public int? ArchivedPalletWidth { get; set; }

        [Column("ArchivedPalletHeight")]
        public int? ArchivedPalletHeight { get; set; }

        [Column("ArchivedPalletLength")]
        public int? ArchivedPalletLength { get; set; }

        [Column("ArchivedPalletIsStackable")]
        public bool ArchivedPalletIsStackable { get; set; }

        [Column("ArchivedPalletType")]
        public int? ArchivedPalletType { get; set; }

        [Column("ArchivedEditionPerPallet")]
        public int? ArchivedEditionPerPallet { get; set; }

        [Column("ArchivedPalletsPerTruck")]
        public int? ArchivedPalletsPerTruck { get; set; }

        [Column("ArchivedFreightPerPallet")]
        public double? ArchivedFreightPerPallet { get; set; }

        [Column("ArchivedPalletCalcFactor", TypeName = "decimal(5,3)")]
        public decimal? ArchivedPalletCalcFactor { get; set; }

        [Column("ArchivedLoadingPerDelivery")]
        public double? ArchivedLoadingPerDelivery { get; set; }

        [Column("ArchivedFreightPerVehicle")]
        public double? ArchivedFreightPerVehicle { get; set; }

        [Column("ArchivedSupplierFactoryId")]
        public int? ArchivedSupplierFactoryId { get; set; }

        [Column("ArchivedCustomerDeliveryAddressId")]
        public int? ArchivedCustomerDeliveryAddressId { get; set; }

        [Column("ArchivedDeliveryAddressCountryCode")]
        [MaxLength(50)]
        public string? ArchivedDeliveryAddressCountryCode { get; set; }

        [Column("ArchivedDeliveryAddressPostalNr")]
        public int? ArchivedDeliveryAddressPostalNr { get; set; }

        [Column("ArchivedLoadingInstruction")]
        [MaxLength(255)]
        public string? ArchivedLoadingInstruction { get; set; }

        [Column("ArchivedFreightNote")]
        [MaxLength(255)]
        public string? ArchivedFreightNote { get; set; }

        [Column("CalcPalletPriceUsed")]
        public bool CalcPalletPriceUsed { get; set; }

        [Column("CalcFtlPriceUsed")]
        public bool CalcFtlPriceUsed { get; set; }

        [Column("FreightTotalPopupPerUnit", TypeName = "decimal(13,5)")]
        public decimal? FreightTotalPopupPerUnit { get; set; }

        [Column("ArchivedPriceListUseAdjustedPalletPlaces")]
        public bool ArchivedPriceListUseAdjustedPalletPlaces { get; set; }

        [Column("ArchivedPriceListPricePerPallet", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListPricePerPallet { get; set; }

        [Column("ArchivedPriceListPriceFTL", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListPriceFtl { get; set; }

        [Column("ArchivedPriceListAdditionSekPerPallet", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListAdditionSekPerPallet { get; set; }

        [Column("ArchivedPriceListSecaMarpolPallet", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListSecaMarpolPallet { get; set; }

        [Column("ArchivedPriceListSecaMarpolFtl", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListSecaMarpolFtl { get; set; }

        [Column("ArchivedPriceListDmtPercentPallet", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListDmtPercentPallet { get; set; }

        [Column("ArchivedPriceListDmtPercentFtl", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListDmtPercentFtl { get; set; }

        [Column("ArchivedPriceListAdditionCurrencyPercentPallet", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListAdditionCurrencyPercentPallet { get; set; }

        [Column("ArchivedPriceListAdditionCurrencyPercentFtl", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListAdditionCurrencyPercentFtl { get; set; }

        [Column("ArchivedPriceListOtherPallet", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListOtherPallet { get; set; }

        [Column("ArchivedPriceListOtherFtl", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListOtherFtl { get; set; }

        [Column("ArchivedPriceListOtherPercentPallet", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListOtherPercentPallet { get; set; }

        [Column("ArchivedPriceListOtherPercentFtl", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListOtherPercentFtl { get; set; }

        [Column("ArchivedPriceListAdditionTotalPercentPallet", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListAdditionTotalPercentPallet { get; set; }

        [Column("ArchivedPriceListAdditionTotalPercentFtl", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListAdditionTotalPercentFtl { get; set; }

        [Column("ArchivedPriceListUnloadingCost", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListUnloadingCost { get; set; }

        [Column("ArchivedPriceListLoadingCost", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListLoadingCost { get; set; }

        [Column("ArchivedPriceListCurrencyId", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListCurrencyId { get; set; }

        [Column("ArchivedPriceListCurrencyRate", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListCurrencyRate { get; set; }

        [Column("ArchivedPriceListSummary")]
        [MaxLength(1000)]
        public string? ArchivedPriceListSummary { get; set; }

        [Column("ArchivedPriceListCurrencyIdFtl", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListCurrencyIdFtl { get; set; }

        [Column("ArchivedPriceListCurrencyRateFtl", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListCurrencyRateFtl { get; set; }

        [Column("ArchivedPriceListPricePerPalletDomestic", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListPricePerPalletDomestic { get; set; }

        [Column("ArchivedPriceListAdditionSekPerPalletDomestic", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListAdditionSekPerPalletDomestic { get; set; }

        [Column("ArchivedPriceListSecaMarpolPalletDomestic", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListSecaMarpolPalletDomestic { get; set; }

        [Column("ArchivedPriceListDmtPercentPalletDomestic", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListDmtPercentPalletDomestic { get; set; }

        [Column("ArchivedPriceListAdditionCurrencyPercentPalletDomestic", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListAdditionCurrencyPercentPalletDomestic { get; set; }

        [Column("ArchivedPriceListOtherPalletDomestic", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListOtherPalletDomestic { get; set; }

        [Column("ArchivedPriceListOtherPercentPalletDomestic", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListOtherPercentPalletDomestic { get; set; }

        [Column("ArchivedPriceListAdditionTotalPercentPalletDomestic", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListAdditionTotalPercentPalletDomestic { get; set; }

        [Column("ArchivedPriceListUnloadingCostDomestic", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListUnloadingCostDomestic { get; set; }

        [Column("ArchivedPriceListLoadingCostDomestic", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListLoadingCostDomestic { get; set; }

        [Column("ArchivedPriceListCurrencyIdDomestic", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListCurrencyIdDomestic { get; set; }

        [Column("ArchivedPriceListCurrencyRateDomestic", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPriceListCurrencyRateDomestic { get; set; }

        [Column("ArchivedCalculatedPriceListColumn")]
        public int? ArchivedCalculatedPriceListColumn { get; set; }

        [Column("ArchivedSupplierEurPalletCost", TypeName = "decimal(13,5)")]
        public decimal? ArchivedSupplierEurPalletCost { get; set; }

        [Column("ArchivedCustomerPalletCost", TypeName = "decimal(13,5)")]
        public decimal? ArchivedCustomerPalletCost { get; set; }

        [Column("ArchivedPalletFormatId")]
        public int? ArchivedPalletFormatId { get; set; }

        [Column("ArchivedPalletFormatIdSupplierOrder")]
        public int? ArchivedPalletFormatIdSupplierOrder { get; set; }

        [Column("ArchivedPalletFormatIdCustomerOrder")]
        public int? ArchivedPalletFormatIdCustomerOrder { get; set; }

        [Column("PalletCalcFactor", TypeName = "decimal(13,5)")]
        public decimal? PalletCalcFactor { get; set; }

        [Column("ArchivedPalletPriceIn", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPalletPriceIn { get; set; }

        [Column("ArchivedPalletPriceOut", TypeName = "decimal(13,5)")]
        public decimal? ArchivedPalletPriceOut { get; set; }

        [Column("PackagingCost", TypeName = "decimal(13,5)")]
        public decimal? PackagingCost { get; set; }

        [Column("PreCalcAdditionPercent", TypeName = "decimal(10,5)")]
        public decimal? PreCalcAdditionPercent { get; set; }

        [Column("Info")]
        [MaxLength(50)]
        public string? Info { get; set; }

        [Column("ArchivedNrOfPerBundle")]
        public int? ArchivedNrOfPerBundle { get; set; }

        [Column("ArchivedNrOfPerBundlePerPallet")]
        public int? ArchivedNrOfPerBundlePerPallet { get; set; }

        [Column("ArchivedNrOfPerOuterPackaging")]
        public int? ArchivedNrOfPerOuterPackaging { get; set; }

        [Column("ArchivedNrOfOuterPackagingPerPallet")]
        public int? ArchivedNrOfOuterPackagingPerPallet { get; set; }

        [Column("ArchivedDoBuntas")]
        public bool ArchivedDoBuntas { get; set; }

        [Column("ArchivedIsYtterforpackning")]
        public bool ArchivedIsYtterforpackning { get; set; }

        public Calculation? Calculation { get; set; }
        public virtual ICollection<CustomerOrder> CustomerOrders { get; set; } = new List<CustomerOrder>();
        public virtual ICollection<SupplierOrder> SupplierOrders { get; set; } = new List<SupplierOrder>();
        public virtual ICollection<QuotationRow> QuotationRows { get; set; } = new List<QuotationRow>();
        public virtual ICollection<OrderCost> OrderCosts { get; set; } = new List<OrderCost>();
        public virtual ICollection<CalculationRowCost> CalculationRowCosts { get; set; } = new List<CalculationRowCost>();
    }
}
