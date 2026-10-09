namespace Econosys.Api.DTOs
{
    public class CreateNewDeliveryRequest
    {
        public int DeliveryType { get; set; }
        public int CustomerOrderId { get; set; }
        public int? ProducedEdition { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public double? NrOfItems { get; set; }
        public int? NrOfPallets { get; set; }
        public string? CallOff { get; set; }
        public int? InventoryId { get; set; }
        public int? PalletFormatId { get; set; }
        public bool PalletIsStackable { get; set; }
        public int? PalletWidth { get; set; }
        public int? PalletHeight { get; set; }
        public int? PalletLength { get; set; }
        public int? EditionPerPallet { get; set; }
        public decimal? PalletCalcFactor { get; set; }
        public bool IsDelivered { get; set; }
        public double? SlattNrOfItems { get; set; }
        public int? SlattNrOfPallets { get; set; }
        public int? SlattEditionPerPallet { get; set; }
        public List<CreateNewDeliverySlattRequest> SlattDeliveries { get; set; } = new();
        public List<CreateNewDeliveryLegRequest> DeliveryLegs { get; set; } = new();
    }

    public class CreateNewDeliverySlattRequest
    {
        public double NrOfItems { get; set; }
        public int NrOfPallets { get; set; } = 1;
        public int? EditionPerPallet { get; set; }
        public bool IsAutomaticRemainder { get; set; }
    }

    public class CreateNewDeliveryLegRequest
    {
        public int? FromPositionId { get; set; }
        public int? ToPositionId { get; set; }
        public int? LegFromPositionId { get; set; }
        public int? LegToPositionId { get; set; }
        public int? PositionDistanceId { get; set; }
        public decimal? DistanceKm { get; set; }
        public string? TypeOfTransport { get; set; }
        public int SortOrder { get; set; }
        public decimal? LatitudeStart { get; set; }
        public decimal? LongitudeStart { get; set; }
        public decimal? LatitudeEnd { get; set; }
        public decimal? LongitudeEnd { get; set; }
        public string FromPositionName { get; set; } = string.Empty;
        public string FromPositionPostalAddress { get; set; } = string.Empty;
        public string ToPositionName { get; set; } = string.Empty;
        public string ToPositionPostalAddress { get; set; } = string.Empty;
    }

    public class CreateNewDeliveryResponse
    {
        public string Type { get; set; } = string.Empty;
        public int Id { get; set; }
        public int? SlattId { get; set; }
        public List<int> SlattIds { get; set; } = new();
    }

    public class DeliveryPositionDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PostalAddress { get; set; } = string.Empty;
        public decimal? Latitude { get; set; }
        public decimal? Longitude { get; set; }
    }

    public class NewDeliveryOrderInfoDto
    {
        public int CustomerOrderId { get; set; }
        public string CustomerOrderNr { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public DateTime? CustomerOrderDeliveryDate { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int? OrderedEdition { get; set; }
        public bool IsPallet { get; set; }
        public int? CustomerPalletFormatId { get; set; }
        public int? SupplierOrderId { get; set; }
        public string SupplierOrderNr { get; set; } = string.Empty;
        public int? ProducedEdition { get; set; }
        public int? InventoryId { get; set; }
        public bool? InventoryIsInventory { get; set; }
        public int? EditionPerPallet { get; set; }
        public decimal? PalletCalcFactor { get; set; }
        public int? PalletLength { get; set; }
        public int? PalletWidth { get; set; }
        public int? PalletHeight { get; set; }
        public bool PalletIsStackable { get; set; }
        public bool CalculationAutoFreightCalc { get; set; }
        public string PackagingType { get; set; } = string.Empty;
        public int? NrOfPerBundle { get; set; }
        public int? NrOfPerOuterPackaging { get; set; }
        public int? CustomerDeliveryAddressId { get; set; }
    }
}
