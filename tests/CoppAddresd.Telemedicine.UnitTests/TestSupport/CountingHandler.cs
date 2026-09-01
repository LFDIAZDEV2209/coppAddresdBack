using System.Net;
using System.Text;

namespace CoppAddresd.Telemedicine.UnitTests.TestSupport;

/// <summary>
/// Handler HTTP falso que devuelve un cuerpo fijo y cuenta las llamadas: para
/// verificar que el caché evita repetir introspecciones/llamadas upstream.
/// </summary>
public sealed class CountingHandler(string responseBody) : HttpMessageHandler
{
    public int Calls { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        Calls++;
        return Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            }
        );
    }
}
