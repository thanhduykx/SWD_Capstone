namespace CPMS.BLL.Services;

public static class ReviewSessionTiming
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public static DateTime GetStartUtc(DateTime sessionDate, int slot)
    {
        TimeOnly startTime = slot switch
        {
            1 => new TimeOnly(7, 30),
            2 => new TimeOnly(9, 45),
            3 => new TimeOnly(12, 30),
            4 => new TimeOnly(14, 45),
            5 => new TimeOnly(17, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(slot), slot, "Review slot must be between 1 and 5.")
        };
        DateTime localStart = DateTime.SpecifyKind(
            sessionDate.Date.Add(startTime.ToTimeSpan()),
            DateTimeKind.Unspecified);
        return new DateTimeOffset(localStart, VietnamOffset).UtcDateTime;
    }

    public static bool HasStarted(DateTime sessionDate, int slot, DateTime utcNow) =>
        utcNow >= GetStartUtc(sessionDate, slot);
}
