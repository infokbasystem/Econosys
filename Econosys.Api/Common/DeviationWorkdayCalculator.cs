namespace Econosys.Api.Common
{
    public static class DeviationWorkdayCalculator
    {
        public static int CountElapsedWeekdays(DateTime start, DateTime end)
        {
            var startDate = start.Date;
            var endDate = end.Date;
            if (endDate <= startDate)
            {
                return 0;
            }

            var weekdays = 0;
            for (var date = startDate; date < endDate; date = date.AddDays(1))
            {
                if (date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                {
                    weekdays++;
                }
            }

            return weekdays;
        }
    }
}