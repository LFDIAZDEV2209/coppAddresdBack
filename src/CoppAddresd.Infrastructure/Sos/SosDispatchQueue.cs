using System.Threading.Channels;
using CoppAddresd.Application.Interfaces;

namespace CoppAddresd.Infrastructure.Sos;

/// <summary>
/// Cola de despacho SOS (Channel delimitado, mismo patrón que las colas de
/// métricas CQRS del repo). El outbox durable son las filas de dedupe
/// comprometidas en la transacción de activación; la cola solo acelera el
/// procesamiento en caliente (el sweep periódico del procesador recupera
/// cualquier alerta que quedara pendiente por caída del proceso).
/// </summary>
public sealed class SosDispatchQueue : ISosDispatchQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(
        new BoundedChannelOptions(1_000)
        {
            // SOS es crítica: nunca se descarta el más nuevo (una alerta
            // perdida en la cola se recupera por el sweep del procesador,
            // pero si el backlog crece preferimos intentar mantenerlas
            // todas en orden de llegada).
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        }
    );

    public ValueTask EnqueueAsync(Guid alertId, CancellationToken ct = default)
    {
        _channel.Writer.TryWrite(alertId);
        return ValueTask.CompletedTask;
    }

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct = default) =>
        _channel.Reader.ReadAllAsync(ct);
}
