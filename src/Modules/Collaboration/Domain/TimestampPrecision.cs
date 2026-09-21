namespace Collaboration.Domain;

/// <summary>PostgreSQL `timestamptz` keeps microseconds; a .NET tick is 100 ns. Instants are cut to the microsecond before
/// they are validated, hashed or stored so all three agree on what "the same instant" means.</summary>
internal static class TimestampPrecision
{
    private const long TicksPerMicrosecond = 10;

    public static DateTimeOffset Truncate(DateTimeOffset value)
    {
        var utcTicks = value.UtcTicks;
        return new DateTimeOffset(utcTicks - utcTicks % TicksPerMicrosecond, TimeSpan.Zero);
    }
}
