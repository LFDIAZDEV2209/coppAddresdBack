namespace CoppAddresd.Community.Metrics;

/// <summary>
/// In-memory channel queue for Community metric events.
/// Non-blocking enqueue; background worker drains the channel.
/// </summary>
public interface ICommunityMetricsQueue
{
    ValueTask EnqueueAsync(ICommunityMetricEvent metricEvent, CancellationToken ct = default);
    IAsyncEnumerable<ICommunityMetricEvent> ReadAllAsync(CancellationToken ct = default);
}