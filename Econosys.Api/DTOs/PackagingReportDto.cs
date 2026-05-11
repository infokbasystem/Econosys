namespace Econosys.Api.DTOs
{
    public enum PackagingMaterialGroupDto
    {
        Well = 1,
        PapperKartong = 2,
        Plast = 3,
        Ovrigt = 4,
    }

    public class PackagingReportRequestDto
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? CustomerId { get; set; }
    }

    public class PackagingReportResponseDto
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public IReadOnlyList<PackagingReportRowDto> Rows { get; set; } = Array.Empty<PackagingReportRowDto>();
    }

    public class PackagingReportRowDto
    {
        public string CustomerName { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public PackagingMaterialGroupDto? MaterialGroup { get; set; }
        public string PackagingFeeCategory { get; set; } = string.Empty;
        public double TotalDeliveredCount { get; set; }
        public double TotalWeight { get; set; }
    }
}
