namespace Econosys.Api.DTOs
{
    public class CallOffOverviewTopTableRowDto
    {
        public int CustomerOrderId { get; set; }
        public string CustomerOrderNr { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerCity { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public DateTime? DeliveryDate { get; set; }
        public string DeliveryTime { get; set; } = string.Empty;
        public DateTime? CalculationDate { get; set; }
        public bool IsCalculationDateOverdue { get; set; }
        public string InventoryName { get; set; } = string.Empty;
        public int? EditionProduced { get; set; }
        public int? EditionCustomerOrder { get; set; }
        public double StockBalanceNow { get; set; }
        public double StockBalance { get; set; }
        public string PalletsNow { get; set; } = string.Empty;
        public string Pallets { get; set; } = string.Empty;
        public string PalletFormat { get; set; } = string.Empty;
        public bool HasLogisticsInfo { get; set; }
        public string LogisticsInfoInternal { get; set; } = string.Empty;
        public bool IsPlanned { get; set; }
        public bool IsOnActiveTransportOrder { get; set; }
    }

    public class CallOffOverviewActiveRowDto
    {
        public int Id { get; set; }
        public bool IsSentToShipper { get; set; }
        public string ShipperName { get; set; } = string.Empty;
        public DateTime? DeliveryDate { get; set; }
        public string CustomerOrderNrs { get; set; } = string.Empty;
        public string CustomerNames { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
    }
}