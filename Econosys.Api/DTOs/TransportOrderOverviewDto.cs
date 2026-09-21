namespace Econosys.Api.DTOs
{
    public class TransportOrderOverviewActiveRowDto
    {
        public int Id { get; set; }
        public int TransportOrderNr { get; set; }
        public string TransporterName { get; set; } = string.Empty;
        public string LoadingCities { get; set; } = string.Empty;
        public string UnloadingCities { get; set; } = string.Empty;
    }

    public class TransportOrderOverviewReportBackRowDto
    {
        public int Id { get; set; }
        public int TransportOrderNr { get; set; }
        public string TransporterName { get; set; } = string.Empty;
        public bool HasNotAttestedTransportInvoices { get; set; }
    }

    public class TransportOrderOverviewPlanningRowDto
    {
        public int Id { get; set; }
        public int TransportOrderNr { get; set; }
        public string TransporterName { get; set; } = string.Empty;
        public string LoadingCities { get; set; } = string.Empty;
        public string UnloadingCities { get; set; } = string.Empty;
        public string FilledInfo { get; set; } = string.Empty;
    }

    public class TransportOrderOverviewTopTableRowDto
    {
        public int CustomerOrderId { get; set; }
        public int? SupplierOrderId { get; set; }
        public string CustomerOrderNr { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string SupplierCity { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerCity { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string DeliveryTime { get; set; } = string.Empty;
        public string ConfirmedDeliveryTime { get; set; } = string.Empty;
        public int? EditionOrdered { get; set; }
        public int? EditionProduced { get; set; }
        public int? EditionCustomerOrder { get; set; }
        public double? Rest { get; set; }
        public double? Pallets { get; set; }
        public string PalletFormat { get; set; } = string.Empty;
        public bool IsPlanned { get; set; }
        public bool HasLogisticsInfo { get; set; }
        public string LogisticsInfoInternal { get; set; } = string.Empty;
    }

    public class TransportOrderOverviewOrderInfoDto
    {
        public int CustomerOrderId { get; set; }
        public string CustomerOrderNr { get; set; } = string.Empty;
        public int? SupplierOrderId { get; set; }
        public int? OrderedEdition { get; set; }
        public int? ProducedEdition { get; set; }
        public bool IsCompleted { get; set; }
    }

    public class SaveTransportOrderOverviewOrderInfoDto
    {
        public int? ProducedEdition { get; set; }
        public bool IsCompleted { get; set; }
    }
}
