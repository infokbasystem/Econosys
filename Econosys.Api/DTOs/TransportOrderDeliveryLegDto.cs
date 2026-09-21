namespace Econosys.Api.DTOs
{
    public class TransportOrderDeliveryLegDto
    {
        public int Id { get; set; }
        public int? TransportOrderId { get; set; }
        public int? CallOffId { get; set; }
        public int? DeliveryToStockId { get; set; }
        public int? DeliveryToCustomerId { get; set; }
        public int? DeliveryFromStockId { get; set; }
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
        public string HelppropFromPositionName { get; set; } = string.Empty;
        public string HelppropFromPositionPostalAddress { get; set; } = string.Empty;
        public string HelppropToPositionName { get; set; } = string.Empty;
        public string HelppropToPositionPostalAddress { get; set; } = string.Empty;
        public string HelppropLegFromPositionName { get; set; } = string.Empty;
        public string HelppropLegFromPositionPostalAddress { get; set; } = string.Empty;
        public string HelppropLegToPositionName { get; set; } = string.Empty;
        public string HelppropLegToPositionPostalAddress { get; set; } = string.Empty;
    }

    public class TransportOrderDeliveryLegsResponseDto
    {
        public List<TransportOrderDeliveryLegDto> DeliveryLegGroupedList { get; set; } = new();
        public List<TransportOrderDeliveryLegDto> DistributionDeliveryLegGroupedList { get; set; } = new();
        public List<List<TransportOrderDeliveryLegDto>> DistributionLegList { get; set; } = new();
    }
}