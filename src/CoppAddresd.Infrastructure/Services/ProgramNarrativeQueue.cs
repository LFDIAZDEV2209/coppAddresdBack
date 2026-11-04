using System.Threading.Channels;
using System.Collections.Concurrent;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Infrastructure.Services;

/// <summary>Cola en memoria acotada; un consumidor, múltiples productores y ninguna espera en el submit.</summary>
public sealed class ProgramNarrativeQueue(IOptions<ProgramAssignmentSettings> settings) : IProgramNarrativeQueue
{
    private readonly ConcurrentDictionary<Guid, byte> _pending = new();
    private readonly Channel<ProgramNarrativeWork> _channel = Channel.CreateBounded<ProgramNarrativeWork>(new BoundedChannelOptions(
        Math.Max(1, settings.Value.NarrativeQueueCapacity))
    {
        SingleReader = true, SingleWriter = false, FullMode = BoundedChannelFullMode.Wait,
    });
    public bool TryEnqueue(Guid recommendationId, bool regenerate = false)
    {
        if (!_pending.TryAdd(recommendationId, 0)) return true;
        if (_channel.Writer.TryWrite(new(recommendationId, regenerate))) return true;
        _pending.TryRemove(recommendationId, out _);
        return false;
    }
    public void Complete(Guid recommendationId) => _pending.TryRemove(recommendationId, out _);
    public IAsyncEnumerable<ProgramNarrativeWork> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}
