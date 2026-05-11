namespace Econosys.Api.DTOs
{
    public class PalletFollowupReportRequestDto
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? CustomerId { get; set; }
    }

    public class PalletFollowupReportResponseDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public IReadOnlyList<PalletFollowupReportRowDto> Rows { get; set; } = Array.Empty<PalletFollowupReportRowDto>();
    }

    public class PalletFollowupReportRowDto
    {
        public int DeliveryId { get; set; }
        public string Supplier { get; set; } = string.Empty;
        public string PalletType { get; set; } = string.Empty;
        public int NrOfPallets { get; set; }
        public decimal? SekPerPalletIn { get; set; }
        public decimal? SekPerPalletOut { get; set; }
        public decimal? SekPerPalletDiff { get; set; }
        public decimal? SumDiff { get; set; }
        public string Customer { get; set; } = string.Empty;
        public string CustomerOrderNr { get; set; } = string.Empty;
    }
}
