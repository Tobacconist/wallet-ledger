namespace Wallet.Infrastructure.Persistence;

internal static class DbClock
{
    // PostgreSQL timestamptz stores microseconds. Truncating up front keeps the value returned in the first
    // response identical to what later reads (and idempotent replays) get back from the database.
    public static DateTimeOffset GetDbUtcNow(this TimeProvider time)
    {
        var now = time.GetUtcNow();
        return now.AddTicks(-(now.Ticks % (TimeSpan.TicksPerMillisecond / 1000)));
    }
}
