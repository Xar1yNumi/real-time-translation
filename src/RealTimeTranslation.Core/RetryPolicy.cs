namespace RealTimeTranslation.Core;

public sealed class RetryPolicy
{
    public RetryPolicy(IEnumerable<TimeSpan> delays)
    {
        ArgumentNullException.ThrowIfNull(delays);
        Delays = Array.AsReadOnly(delays.ToArray());
    }

    public static RetryPolicy NetworkDefault { get; } = new([
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4)
    ]);

    public IReadOnlyList<TimeSpan> Delays { get; }
}
