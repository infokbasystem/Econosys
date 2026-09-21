namespace Econosys.Api.DTOs
{
    public class TransportOrderTruckPlanPalletDto
    {
        public int Id { get; set; }

        public int TransportOrderDeliveryId { get; set; }

        public int PosCmX { get; set; }

        public int PosCmY { get; set; }

        public int Rotation { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }
    }

    public class TransportOrderTruckPlanResponseDto
    {
        public List<TransportOrderTruckPlanPalletDto> PalletList { get; set; } = new();
    }

    public class TransportOrderTruckPlanSaveRequestDto
    {
        public List<TransportOrderTruckPlanPalletDto> PalletList { get; set; } = new();
    }
}
