using Twilio.Clients;
using Twilio.Http;

namespace CoppAddresd.UnitTests;

/// <summary>
/// Doble de <see cref="ITwilioRestClient"/> para los tests de
/// <c>TwilioOtpService</c>: NO realiza llamadas reales a Twilio ni depende de
/// Internet. El test controla la respuesta (o la excepción) que el SDK debe
/// interpretar a través de un delegado.
/// </summary>
internal sealed class FakeTwilioRestClient : ITwilioRestClient
{
    private readonly Func<Request, Task<Response>> _handler;

    public FakeTwilioRestClient(Func<Request, Task<Response>> handler)
    {
        _handler = handler;
    }

    public string AccountSid => "AC_test";

    public string Region => null!;

    public Twilio.Http.HttpClient HttpClient => null!;

    public Response Request(Request request)
        => throw new NotSupportedException("Los tests usan el flujo asíncrono.");

    public Task<Response> RequestAsync(Request request) => _handler(request);
}
