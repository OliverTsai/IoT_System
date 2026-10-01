namespace IoTMonitor.Api.Tests.Infrastructure;

public sealed class AdjustableTimeProvider(DateTimeOffset initialUtcNow) : TimeProvider
{
    private DateTimeOffset _utcNow = initialUtcNow;

    public override DateTimeOffset GetUtcNow()
    {
        return _utcNow;
    }

    public void Advance(TimeSpan amount)
    {
        _utcNow = _utcNow.Add(amount);
    }
}
