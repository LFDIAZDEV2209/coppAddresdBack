using System.Threading.Channels;

namespace CoppAddresd.WhatsApp.Webhook;

/// <summary>La cola acelera el procesamiento; la BD es la fuente durable para replay.</summary>
public sealed class WhatsAppQueue
{
    private readonly Channel<Guid> channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(1024)
        { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
    public void Enqueue(Guid id) => channel.Writer.TryWrite(id);
    public bool TryRead(out Guid id) => channel.Reader.TryRead(out id);
}
