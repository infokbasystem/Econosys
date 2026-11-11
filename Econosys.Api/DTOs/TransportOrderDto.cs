namespace Econosys.Api.DTOs
{
    public class TransportOrderDto
    {
        public int Id { get; set; }
        public int TransportOrderNr { get; set; }
        public string? SenderReference { get; set; }
        public string? Note { get; set; }
        public DateTime? DateCreated { get; set; }
        public DateTime? DateLoading { get; set; }
        public DateTime? DateDelivery { get; set; }
        public int? DeliveryStatus { get; set; }
        public int? ShipperId { get; set; }
        public bool IsSentToShipper { get; set; }
        public bool IsReportedBack { get; set; }
        public int? CreatedByUserId { get; set; }
        public string? CreatedByUserName { get; set; }
        public DateTime? CreatedTimeStamp { get; set; }
        public int? EditedByUserId { get; set; }
        public string? EditedByUserName { get; set; }
        public DateTime? EditedTimeStamp { get; set; }
    }

    public class TransportOrderAggregateDto
    {
        public TransportOrderDto TransportOrder { get; set; } = new();
        public TransportOrderDeliveryLegsResponseDto DeliveryLegs { get; set; } = new();
        public TransportOrderDeliveryOverviewDto DeliveryOverview { get; set; } = new();
        public TransportOrderTruckPlanResponseDto TruckPlan { get; set; } = new();
        public string Version { get; set; } = string.Empty;
        public CallOffRouteCalculationDto? RouteCalculation { get; set; }
        public bool IsRouteMissing { get; set; }
    }

    /// <summary>Delivery rows and ordering to preview without saving the transport order.</summary>
    public sealed record TransportOrderRoutePreviewRequest
    {
        public List<TransportOrderRoutePreviewDelivery> Deliveries { get; init; } = new();
    }

    /// <summary>A transport-order delivery and its proposed sort order.</summary>
    public sealed record TransportOrderRoutePreviewDelivery(int Id, int SortOrder);

    public class SaveTransportOrderAggregateRequest
    {
        public string Version { get; set; } = string.Empty;
        public string? SenderReference { get; set; }
        public string? Note { get; set; }
        public DateTime? DateCreated { get; set; }
        public DateTime? DateLoading { get; set; }
        public DateTime? DateDelivery { get; set; }
        public int? DeliveryStatus { get; set; }
        public int? ShipperId { get; set; }
        public List<TransportOrderTruckPlanPalletDto> PalletList { get; set; } = new();
        public List<TransportOrderDeliverySaveDto> TransportOrderDeliveryList { get; set; } = new();
    }

    public class TransportOrderFormOptionsDto
    {
        public List<FilterOptionDto<int>> Transporters { get; set; } = new();
    }

    public class TransportOrderPdfRecipientOptionsDto
    {
        public List<FilterOptionDto<int>> SupplierFactories { get; set; } = new();
        public List<FilterOptionDto<int>> Inventories { get; set; } = new();
    }
}