namespace Econosys.Api.DTOs
{
    public class HandlingTimesCreatedByRowDto
    {
        public int? UserId { get; set; }
        public string UserLabel { get; set; } = string.Empty;
        public int TotalSupplierOrderCount { get; set; }
        public int NewOrderCount { get; set; }
        public int RepeatOrderCount { get; set; }
        public decimal? NewOrderSharePercent { get; set; }
        public decimal? AverageRepeatHandlingDays { get; set; }
        public decimal? RepeatPercentUnderGoalDays { get; set; }
    }

    public class HandlingTimesCreatedByReportResponseDto
    {
        public int Year { get; set; }
        public List<int> AvailableYears { get; set; } = new();
        public int? GoalNrOfDays { get; set; }
        public List<HandlingTimesCreatedByRowDto> Rows { get; set; } = new();
        public HandlingTimesCreatedByRowDto? Total { get; set; }
    }
}
