namespace Econosys.Api.DTOs
{
    public class CallOffDto
    {
        public int Id { get; set; }
        public int? ShipperId { get; set; }
        public string? Reference { get; set; }
        public string? Note { get; set; }
        public DateTime? CreatedDateTime { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public int? DeliveryStatus { get; set; }
        public bool IsSentToShipper { get; set; }
        public bool DoDebitFreight { get; set; }
        public decimal? FreightCostToDebit { get; set; }
        public int? CustomerDeliveryAddressId { get; set; }
        public int? CreatedByUserId { get; set; }
        public string? CreatedByUserName { get; set; }
    }

    public class CallOffDeliveryRowDto
    {
        public int Id { get; set; }
        public int? DeliveryFromStockId { get; set; }
        public int SortOrder { get; set; }
        public string? Note { get; set; }
        public string? DeliveryAddressFreeText { get; set; }
        public decimal? NrOfPalletPlaces { get; set; }
        public string CustomerOrderNr { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string InventoryName { get; set; } = string.Empty;
        public int NrOfPallets { get; set; }
        public string PalletFormatName { get; set; } = string.Empty;
        public string KolliFormat { get; set; } = string.Empty;
    }

    public class CallOffDeliveryAddressOptionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string PostalNr { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
    }

    public class CallOffAggregateDto
    {
        public CallOffDto CallOff { get; set; } = new();
        public List<CallOffDeliveryRowDto> CallOffDeliveryList { get; set; } = new();
        public List<CallOffDeliveryAddressOptionDto> DeliveryAddressOptions { get; set; } = new();
        public TransportOrderDeliveryLegsResponseDto DeliveryLegs { get; set; } = new();
        public string Version { get; set; } = string.Empty;
        /// <summary>Outcome of the leg recalculation triggered by this request, if any.</summary>
        public CallOffRouteCalculationDto? RouteCalculation { get; set; }
        /// <summary>True when the call-off has deliveries but no stored transport legs (for example after a failed calculation).</summary>
        public bool IsRouteMissing { get; set; }
    }

    /// <summary>Outcome of a call-off transport-leg calculation: Calculated, Empty, Partial, Failed or Stale.</summary>
    public class CallOffRouteCalculationDto
    {
        public string Status { get; set; } = string.Empty;
        public string? Message { get; set; }
    }

    /// <summary>Transport legs together with the outcome of their calculation.</summary>
    public class CallOffRouteResponseDto
    {
        public TransportOrderDeliveryLegsResponseDto DeliveryLegs { get; set; } = new();
        public CallOffRouteCalculationDto RouteCalculation { get; set; } = new();
    }

    /// <summary>Unsaved call-off state to preview transport legs for.</summary>
    public class CallOffRoutePreviewRequest
    {
        public int? CustomerDeliveryAddressId { get; set; }
        public List<CallOffRoutePreviewDeliveryDto> Deliveries { get; set; } = new();
    }

    /// <summary>Either an existing stock delivery or an unsaved planned delivery.</summary>
    public class CallOffRoutePreviewDeliveryDto
    {
        public int? DeliveryFromStockId { get; set; }
        public AddCallOffDeliveryFromStockRequest? NewDelivery { get; set; }
    }

    /// <summary>Result of deleting a delivery from a call-off.</summary>
    public class CallOffDeliveryDeletedResponseDto
    {
        public string Version { get; set; } = string.Empty;
        public TransportOrderDeliveryLegsResponseDto DeliveryLegs { get; set; } = new();
        public CallOffRouteCalculationDto? RouteCalculation { get; set; }
    }

    public class SaveCallOffDeliveryDto
    {
        public int Id { get; set; }
        public string? Note { get; set; }
        public decimal? NrOfPalletPlaces { get; set; }
        // Only used when Id is 0, to link a delivery created via /calloff/deliveries that hasn't been saved yet.
        public int? DeliveryFromStockId { get; set; }
        public AddCallOffDeliveryFromStockRequest? NewDelivery { get; set; }
    }

    public class SaveCallOffAggregateRequest
    {
        public string Version { get; set; } = string.Empty;
        public int? ShipperId { get; set; }
        public string? Reference { get; set; }
        public string? Note { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public int? DeliveryStatus { get; set; }
        public bool IsSentToShipper { get; set; }
        public bool DoDebitFreight { get; set; }
        public decimal? FreightCostToDebit { get; set; }
        public int? CustomerDeliveryAddressId { get; set; }
        public List<SaveCallOffDeliveryDto> CallOffDeliveryList { get; set; } = new();
    }

    public class CallOffFormOptionsDto
    {
        public List<FilterOptionDto<int>> Shippers { get; set; } = new();
    }

    public class CallOffDeliveryCandidateGroupDto
    {
        public int? EditionPerPallet { get; set; }
        public double NrOf { get; set; }
        public double NrOfNow { get; set; }
        public int NrOfPallets { get; set; }
        public int NrOfPalletsNow { get; set; }
        public int? InventoryId { get; set; }
        public string InventoryName { get; set; } = string.Empty;
        public int? PalletLength { get; set; }
        public int? PalletWidth { get; set; }
        public int? PalletHeight { get; set; }
        public bool PalletIsStackable { get; set; }
        public decimal? PalletCalcFactor { get; set; }
    }

    public class CallOffDeliveryCandidateResponseDto
    {
        public int CustomerOrderId { get; set; }
        public string CustomerOrderNr { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int? OrderedQty { get; set; }
        public List<CallOffDeliveryCandidateGroupDto> Groups { get; set; } = new();
    }

    public class DeliveryFromStockDetailsDto
    {
        public int Id { get; set; }
        public int? CustomerOrderId { get; set; }
        public string CustomerOrderNr { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int? OrderedQty { get; set; }
        public int? EditionPerPallet { get; set; }
        public int? NrOf { get; set; }
        public int? NrOfPallets { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public double? NrOfItems { get; set; }
        public string? CallOff { get; set; }
        public int? InventoryId { get; set; }
        public string InventoryName { get; set; } = string.Empty;
        public int? PalletLength { get; set; }
        public int? PalletWidth { get; set; }
        public int? PalletHeight { get; set; }
        public bool PalletIsStackable { get; set; }
        public decimal? PalletCalcFactor { get; set; }
        public bool IsDelivered { get; set; }
    }

    public class AddCallOffDeliveryFromStockRequest
    {
        public int CustomerOrderId { get; set; }
        public int? EditionPerPallet { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public double? NrOfItems { get; set; }
        public int? NrOfPallets { get; set; }
        public string? CallOff { get; set; }
        public int? InventoryId { get; set; }
        public int? PalletLength { get; set; }
        public int? PalletWidth { get; set; }
        public int? PalletHeight { get; set; }
        public bool PalletIsStackable { get; set; }
        public decimal? PalletCalcFactor { get; set; }
        public bool IsDelivered { get; set; }
    }
}
