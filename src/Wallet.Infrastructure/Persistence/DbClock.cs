namespace Wallet.Infrastructure.Persistence;

internal static class DbClock
{
    // timestamptz keeps microseconds; truncate so the first response matches later reads.
    public static DateTimeOffset GetDbUtcNow(this TimeProvider time)
    {
        var now = time.GetUtcNow();
        return now.AddTicks(-(now.Ticks % (TimeSpan.TicksPerMillisecond / 1000)));
    }
}
