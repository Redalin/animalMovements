using AnimalMovements.Domain.Abstractions;

namespace AnimalMovements.Infrastructure.Time;

public sealed class NzSystemClock : INzClock
{
    private static readonly TimeZoneInfo NzTimeZone = ResolveNzTimeZone();

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateOnly NzToday
    {
        get
        {
            var nzNow = TimeZoneInfo.ConvertTime(UtcNow, NzTimeZone);
            return DateOnly.FromDateTime(nzNow.DateTime);
        }
    }

    private static TimeZoneInfo ResolveNzTimeZone()
    {
        foreach (var id in new[] { "Pacific/Auckland", "New Zealand Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        throw new InvalidOperationException("Could not resolve New Zealand timezone on this host.");
    }
}
