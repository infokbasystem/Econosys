namespace Econosys.Api.DTOs
{
    public class TransportOrderDeliverySaveDto
    {
        public int Id { get; set; }
        public int SortOrder { get; set; }
        public int? DeliveryFromStockId { get; set; }
        public int? DeliveryToCustomerId { get; set; }
        public int? DeliveryToStockId { get; set; }
        public int? SupplierOrderId { get; set; }
        public string? DeliveryAddressFreeText { get; set; }
        public string? Note { get; set; }
        public decimal? CostCalcFreightInternational { get; set; }
        public decimal? CostCalcFreightDomestic { get; set; }
        public decimal? CostCalcFreightUnloading { get; set; }
        public decimal? CostCalcFreightLoading { get; set; }
        public decimal? CostCalcFreightOther { get; set; }
        public decimal? CostCalcFreightInternationalCost { get; set; }
        public decimal? CostCalcFreightDomesticCost { get; set; }
        public decimal? CostCalcFreightUnloadingCost { get; set; }
        public decimal? CostCalcFreightFtlCost { get; set; }
        public int? ForcedOmlastStatus { get; set; }
        public int? OmlastInventoryId { get; set; }
        public DateTime? OmlastInDate { get; set; }
        public DateTime? OmlastDeliveryDate { get; set; }
        public string? OmlastNote { get; set; }
        public decimal? OmlastPalletPlaces { get; set; }
        public decimal? TotalWeight { get; set; }
        public string? CustomerYourOrderNr { get; set; }
    }

    public class TransportOrderSupplierFactoryRowDto
    {
        public string SupplierName { get; set; } = string.Empty;
        public string FactoryName { get; set; } = string.Empty;
    }

    public class TransportOrderDeliveryCardDto
    {
        public TransportOrderDeliverySaveDto TransportOrderDelivery { get; set; } = new();
        public int Id { get; set; }
        public int? ParentDeliveryId { get; set; }
        public int SupplierOrderId { get; set; }
        public string SupplierOrderNr { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public string CustomerNameAddress { get; set; } = string.Empty;
        public string? DeliveryAddressFreeText { get; set; }
        public string? Note { get; set; }
        public string? CustomerYourOrderNr { get; set; }
        public string SupplierOrderConfirmedDeliveryDate { get; set; } = string.Empty;
        public string ForcedOmlastCaption { get; set; } = "Omlast / direkt";
        public int ForcedOmlastStatus { get; set; }
        public bool IsSlattPallet { get; set; }
        public int NrOfPallets { get; set; }
        public bool PalletIsStackable { get; set; }
        public string PalletInfo { get; set; } = string.Empty;
        public string PalletsLeftToPlace { get; set; } = "0 kvar";
        public string Background { get; set; } = "#2F4E7388";
        public string BorderBrush { get; set; } = "#2F4E73";
        public bool InfoPopupVisible { get; set; }
        public string LogisticsInfoInternal { get; set; } = string.Empty;
    }

    public class TransportOrderDeliveryOverviewDto
    {
        public List<TransportOrderSupplierFactoryRowDto> SupplierFactoryList { get; set; } = new();
        public List<TransportOrderDeliveryCardDto> TransportOrderDeliveryList { get; set; } = new();
    }
}