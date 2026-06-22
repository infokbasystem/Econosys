namespace Econosys.Api.Common;

public static class SwedishTime
{
    private static readonly TimeZoneInfo SwedenTimeZone = ResolveSwedenTimeZone();

    public static DateTime Now => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, SwedenTimeZone);

    public static DateTime Today => Now.Date;

    private static TimeZoneInfo ResolveSwedenTimeZone()
    {
        var timezoneIds = new[]
        {
            "Europe/Stockholm",
            "W. Europe Standard Time",
        };

        foreach (var timezoneId in timezoneIds)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        throw new InvalidOperationException("Could not resolve Sweden timezone on this host.");
    }
}
