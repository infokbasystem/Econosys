using Econosys.Api.Common;
using Xunit;

namespace Econosys.Api.Tests;

public class DeviationWorkdayCalculatorTests
{
    [Theory]
    [InlineData(2026, 10, 23, 2026, 10, 26, 1)]
    [InlineData(2026, 10, 19, 2026, 10, 26, 5)]
    [InlineData(2026, 10, 19, 2026, 10, 19, 0)]
    [InlineData(2026, 10, 26, 2026, 10, 23, 0)]
    public void CountElapsedWeekdaysExcludesWeekendsAndDoesNotCountEndDate(
        int startYear,
        int startMonth,
        int startDay,
        int endYear,
        int endMonth,
        int endDay,
        int expected)
    {
        var start = new DateTime(startYear, startMonth, startDay);
        var end = new DateTime(endYear, endMonth, endDay);

        Assert.Equal(expected, DeviationWorkdayCalculator.CountElapsedWeekdays(start, end));
    }
}