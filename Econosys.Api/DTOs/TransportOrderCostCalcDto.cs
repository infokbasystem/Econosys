namespace Econosys.Api.DTOs
{
    public class TransportOrderCostCalcDto
    {
        public int Id { get; set; }
        public int? TransportOrderId { get; set; }
        public string? CalcCityFrom { get; set; }
        public int? CalcPostalNrTo { get; set; }
        public decimal? ResultInternationalCost { get; set; }
        public string? Note { get; set; }
        public int? CreatedAtStatus { get; set; }
        public DateTime? CreatedDateTime { get; set; }
        public bool IsManualCalc { get; set; }
        public bool IsLTL { get; set; }
        public int? CostCalcForcePriceListId { get; set; }
        public List<TransportOrderCostCalcSupplierOrderDto> SupplierOrders { get; set; } = new();
    }

    public class TransportOrderCostCalcSupplierOrderDto
    {
        public int Id { get; set; }
        public int? TransportOrderCostCalcId { get; set; }
        public int? SupplierOrderId { get; set; }
        public decimal? CaclulationInternationalCost { get; set; }
        public decimal? CaclulationDomesticCost { get; set; }
        public decimal? CaclulationFtl { get; set; }
        public decimal? CaclulationUnloading { get; set; }
        public decimal? CaclulationTotal { get; set; }
        public decimal? CaclulationUsedTotal { get; set; }
        public string? CalculationCalcInfo { get; set; }
        public decimal? ToInternationalCost { get; set; }
        public decimal? TransportOrderDomesticCost { get; set; }
        public decimal? TransportOrderLoading { get; set; }
        public decimal? TransportOrderUnloading { get; set; }
        public decimal? TransportOrderOther { get; set; }
        public decimal? TransportOrderTotal { get; set; }
        public string? TransportOrderCalcInfo { get; set; }
        public decimal? ResultInternationalCost { get; set; }
        public decimal? ResultDomesticCost { get; set; }
        public decimal? ResultTotal { get; set; }
        public decimal? ResultOther { get; set; }
        public string? ResultNote { get; set; }
        public decimal? DiffTransportOrderCaclulation { get; set; }
        public decimal? DiffResultCaclulation { get; set; }
        public decimal? DiffResultTransportOrder { get; set; }
        public decimal? TotalNrOfItems { get; set; }
        public int? TotalNrOfPallets { get; set; }
        public int? TotalNrOfPalletPlaces { get; set; }
        public decimal? TotalPalletArea { get; set; }
        public decimal? PalletFactor { get; set; }
        public List<TransportOrderCostCalcSupplierOrderDeliveryDto> Deliveries { get; set; } = new();
    }

    public class TransportOrderCostCalcSupplierOrderDeliveryDto
    {
        public int Id { get; set; }
        public int? TransportOrderCostCalcSupplierOrderId { get; set; }
        public int? DeliveryToCustomerId { get; set; }
        public int? DeliveryToStockId { get; set; }
        public int? DeliveryFromStockId { get; set; }
        public decimal? NrOfItems { get; set; }
        public int? NrOfPallets { get; set; }
        public int? PalletFormatId { get; set; }
        public bool PalletIsStackable { get; set; }
        public int? PalletWidth { get; set; }
        public int? PalletHeight { get; set; }
        public int? PalletLength { get; set; }
        public int? EditionPerPallet { get; set; }
        public decimal? PalletCalcFactor { get; set; }
        public bool IsSlattPallet { get; set; }
        public int? ParentDeliveryId { get; set; }
    }

    public class UpdateTransportOrderCostCalcRequest
    {
        public bool IsManualCalc { get; set; }
        public bool IsLTL { get; set; }
        public string? Note { get; set; }
        public string? CalcCityFrom { get; set; }
        public int? CalcPostalNrTo { get; set; }
        public int? CostCalcForcePriceListId { get; set; }
        public decimal? ToInternationalCost { get; set; }
        public decimal? ResultInternationalCost { get; set; }
        public List<UpdateTransportOrderCostCalcSupplierOrderRequest> SupplierOrders { get; set; } = new();
    }

    public class UpdateTransportOrderCostCalcSupplierOrderRequest
    {
        public int Id { get; set; }
        public decimal? ToInternationalCost { get; set; }
        public decimal? TransportOrderDomesticCost { get; set; }
        public decimal? TransportOrderLoading { get; set; }
        public decimal? TransportOrderUnloading { get; set; }
        public decimal? TransportOrderOther { get; set; }
        public decimal? ResultDomesticCost { get; set; }
        public decimal? ResultOther { get; set; }
        public string? ResultNote { get; set; }
    }

    public class TransportCostPriceListOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class TransportCostPriceListDto
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public string? ImportName { get; set; }
        public decimal? SecaMarpolPallet { get; set; }
        public decimal? DmtPercentPallet { get; set; }
        public decimal? AdditionCurrencyPercentPallet { get; set; }
        public decimal? OtherPallet { get; set; }
        public decimal? OtherPercentPallet { get; set; }
        public decimal? AdditionTotalPercentPallet { get; set; }
        public int? CurrencyId { get; set; }
        public decimal? SecaMarpolFtl { get; set; }
        public decimal? DmtPercentFtl { get; set; }
        public decimal? AdditionCurrencyPercentFtl { get; set; }
        public decimal? OtherFtl { get; set; }
        public decimal? OtherPercentFtl { get; set; }
        public decimal? AdditionTotalPercentFtl { get; set; }
        public bool IsStafflad { get; set; }
        public DateTime? LastImportDateTime { get; set; }
        public decimal? LoadingCost { get; set; }
        public decimal? UnloadingCost { get; set; }
        public decimal? PreCalcAdditionPercent { get; set; }
        public List<TransportCostPriceListDataDto> PriceListData { get; set; } = new();
    }

    public class TransportCostPriceListDataDto
    {
        public int Id { get; set; }
        public int? TransportCostPriceListId { get; set; }
        public string? CountryCode { get; set; }
        public int? PostalNrFrom { get; set; }
        public int? PostalNrTo { get; set; }
        public string? Transhipment { get; set; }
        public decimal? AdditionSekPerPallet { get; set; }
        public decimal? P1 { get; set; }
        public decimal? P2 { get; set; }
        public decimal? P3 { get; set; }
        public decimal? P4 { get; set; }
        public decimal? P5 { get; set; }
        public decimal? P6 { get; set; }
        public decimal? P7 { get; set; }
        public decimal? P8 { get; set; }
        public decimal? P9 { get; set; }
        public decimal? P10 { get; set; }
        public decimal? P11 { get; set; }
        public decimal? P12 { get; set; }
        public decimal? P13 { get; set; }
        public decimal? P14 { get; set; }
        public decimal? P15 { get; set; }
        public decimal? P16 { get; set; }
        public decimal? P17 { get; set; }
        public decimal? P18 { get; set; }
        public decimal? P19 { get; set; }
        public decimal? P20 { get; set; }
        public decimal? P21 { get; set; }
        public decimal? P22 { get; set; }
        public decimal? P23 { get; set; }
        public decimal? P24 { get; set; }
        public decimal? P25 { get; set; }
        public decimal? P26 { get; set; }
        public decimal? P27 { get; set; }
        public decimal? P28 { get; set; }
        public decimal? P29 { get; set; }
        public decimal? P30 { get; set; }
        public decimal? P31 { get; set; }
        public decimal? P32 { get; set; }
        public decimal? PFTL { get; set; }
    }
}
