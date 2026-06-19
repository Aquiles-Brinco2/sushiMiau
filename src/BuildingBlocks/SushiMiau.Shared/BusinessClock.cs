namespace SushiMiau.Shared;

public static class BusinessClock
{
    private static readonly TimeZoneInfo RestaurantTimeZone = ResolveTimeZone();

    public static DateTimeOffset Now => TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, RestaurantTimeZone);

    public static string Today => Now.ToString("yyyy-MM-dd");

    public static string DateKey(DateTimeOffset value) =>
        TimeZoneInfo.ConvertTime(value, RestaurantTimeZone).ToString("yyyy-MM-dd");

    public static DateTimeOffset FromLocal(DateTime value)
    {
        var local = DateTime.SpecifyKind(value, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, RestaurantTimeZone.GetUtcOffset(local));
    }

    private static TimeZoneInfo ResolveTimeZone()
    {
        foreach (var id in new[] { "America/La_Paz", "SA Western Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone("Bolivia", TimeSpan.FromHours(-4), "Bolivia", "Bolivia");
    }
}
