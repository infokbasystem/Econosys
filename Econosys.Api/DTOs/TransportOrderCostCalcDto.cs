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
}
