namespace Econosys.Api.DTOs
{
    public class CalculationVariableDto
    {
        public int Id { get; set; }
        public double? FreightPerPallet { get; set; }
        public double? FreightPerVehicle { get; set; }
        public double? LoadingPerPallet { get; set; }
        public double? StoragePerM2 { get; set; }
        public short? Interest { get; set; }
        public double? LoadingPerDelivery { get; set; }
        public double? FreightFromStockPerPallet { get; set; }
        public int? TruckLoadingLengthMm { get; set; }
        public int? TruckLoadingWidthMm { get; set; }
    }

    public class UpdateCalculationVariableRequest
    {
        public double? FreightPerPallet { get; set; }
        public double? FreightPerVehicle { get; set; }
        public double? LoadingPerPallet { get; set; }
        public double? StoragePerM2 { get; set; }
        public short? Interest { get; set; }
        public double? LoadingPerDelivery { get; set; }
        public double? FreightFromStockPerPallet { get; set; }
        public int? TruckLoadingLengthMm { get; set; }
        public int? TruckLoadingWidthMm { get; set; }
    }
}
