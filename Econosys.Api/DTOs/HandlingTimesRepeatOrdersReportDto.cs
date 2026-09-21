namespace Econosys.Api.DTOs
{
    public class HandlingTimesRepeatOrdersYearRowDto
    {
        public int Year { get; set; }
        public int OrderCount { get; set; }
        public decimal? AverageHandlingTimeDays { get; set; }
        public decimal? PercentUnderGoalDays { get; set; }
        public decimal? VarianceVsKpi { get; set; }
    }

    public class HandlingTimesRepeatOrdersReportResponseDto
    {
        public int? GoalNrOfDays { get; set; }
        public decimal? GoalPercentHandledUnderGoalNrOfDays { get; set; }
        public List<HandlingTimesRepeatOrdersYearRowDto> Years { get; set; } = new();
    }

    public class HandlingTimesRepeatOrdersMonthRowDto
    {
        public int MonthNumber { get; set; }
        public int CurrentYearOrderCount { get; set; }
        public decimal? CurrentYearAverageHandlingTimeDays { get; set; }
        public decimal? CurrentYearPercentUnderGoalDays { get; set; }
        public int PreviousYearOrderCount { get; set; }
        public decimal? PreviousYearAverageHandlingTimeDays { get; set; }
    }

    public class HandlingTimesRepeatOrdersMonthlyReportResponseDto
    {
        public int CurrentYear { get; set; }
        public int PreviousYear { get; set; }
        public int? GoalNrOfDays { get; set; }
        public decimal? GoalPercentHandledUnderGoalNrOfDays { get; set; }
        public List<HandlingTimesRepeatOrdersMonthRowDto> Months { get; set; } = new();
    }

    public class HandlingTimesYearlyMonthlyMonthCellDto
    {
        public int MonthNumber { get; set; }
        public int? OrderCount { get; set; }
        public decimal? AverageHandlingTimeDays { get; set; }
    }

    public class HandlingTimesYearlyMonthlyYearRowDto
    {
        public int Year { get; set; }
        public List<HandlingTimesYearlyMonthlyMonthCellDto> Months { get; set; } = new();
        public int TotalOrderCount { get; set; }
        public decimal? TotalAverageHandlingTimeDays { get; set; }
    }

    public class HandlingTimesYearlyMonthlyReportResponseDto
    {
        public List<HandlingTimesYearlyMonthlyYearRowDto> Years { get; set; } = new();
    }
}
