using System.Net;
using System.Text.Json;
using CoppAddresd.Application.WhatsApp.Messaging;
using CoppAddresd.WhatsApp.Messaging;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.WhatsApp.Tests;

[Trait("Category", "Unit")]
public sealed class SenderTests
{
    [Theory]
    [InlineData(429, null, WhatsAppFailureKind.Transient)]
    [InlineData(503, null, WhatsAppFailureKind.Transient)]
    [InlineData(400, 1, WhatsAppFailureKind.Transient)]
    [InlineData(400, 2, WhatsAppFailureKind.Transient)]
    [InlineData(400, 4, WhatsAppFailureKind.Transient)]
    [InlineData(400, 80007, WhatsAppFailureKind.Transient)]
    [InlineData(400, 130429, WhatsAppFailureKind.Transient)]
    [InlineData(400, null, WhatsAppFailureKind.Permanent)]
    [InlineData(500, 190, WhatsAppFailureKind.Permanent)]
    public void Errors_AreClassified(int status, int? code, WhatsAppFailureKind expected) =>
        Assert.Equal(expected, MessagePolicy.Classify(status, code));

    [Fact]
    public void Split_PreservesLongUnicode_AndMaskHidesPhone()
    {
        var text = new string('a', 4095) + "😀" + new string('b', 5000);
        var parts = MessagePolicy.Split(text);
        Assert.Equal(text, string.Concat(parts));
        Assert.All(
            parts,
            part =>
            {
                Assert.InRange(part.Length, 1, 4096);
                Assert.False(char.IsHighSurrogate(part[^1]));
            }
        );
        Assert.Equal("573***819", MessagePolicy.MaskPhone("+573053924819"));
        Assert.Equal("***", MessagePolicy.MaskPhone("123"));
    }

    [Fact]
    public async Task Sender_VerifiesEndpointHeadersPayload_AndSequentialFragments()
    {
        List<string> sent = [];
        var handler = new Handler(
            async (request, ct) =>
            {
                Assert.Equal(
                    "https://graph.facebook.com/v23.0/test-phone/messages",
                    request.RequestUri!.ToString()
                );
                Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
                Assert.Equal("test-only", request.Headers.Authorization.Parameter);
                var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                Assert.Equal(
                    "whatsapp",
                    body.RootElement.GetProperty("messaging_product").GetString()
                );
                Assert.Equal("573053924819", body.RootElement.GetProperty("to").GetString());
                sent.Add(body.RootElement.GetProperty("text").GetProperty("body").GetString()!);
                return Ok();
            }
        );
        var sender = Create(handler);
        var original = new string('a', 5500);
        foreach (var part in MessagePolicy.Split(original))
            Assert.True(
                (
                    await sender.SendTextAsync(
                        "+573053924819",
                        part,
                        DateTimeOffset.UtcNow,
                        CancellationToken.None
                    )
                ).Succeeded
            );
        Assert.Equal(2, sent.Count);
        Assert.Equal(original, string.Concat(sent));
    }

    [Fact]
    public async Task ClosedWindow_IsRejectedWithoutHttp()
    {
        var sender = Create(
            new Handler((_, _) => throw new InvalidOperationException("No debe enviar"))
        );
        var result = await sender.SendTextAsync(
            "+573053924819",
            "hola",
            DateTimeOffset.UtcNow.AddHours(-24),
            CancellationToken.None
        );
        Assert.Equal("window_closed", result.ErrorCode);
    }

    [Fact]
    public async Task Template_HasLanguageAndBodyParameters()
    {
        var sender = Create(
            new Handler(
                async (request, ct) =>
                {
                    using var json = JsonDocument.Parse(
                        await request.Content!.ReadAsStringAsync(ct)
                    );
                    var template = json.RootElement.GetProperty("template");
                    Assert.Equal("checkin_bienestar", template.GetProperty("name").GetString());
                    Assert.Equal(
                        "es",
                        template.GetProperty("language").GetProperty("code").GetString()
                    );
                    Assert.Equal(
                        "Ana",
                        template
                            .GetProperty("components")[0]
                            .GetProperty("parameters")[0]
                            .GetProperty("text")
                            .GetString()
                    );
                    return Ok();
                }
            )
        );
        Assert.True(
            (
                await sender.SendTemplateAsync(
                    "+573053924819",
                    "checkin_bienestar",
                    "es",
                    ["Ana", "12/11/2026"],
                    CancellationToken.None
                )
            ).Succeeded
        );
    }

    [Fact]
    public async Task CallerCancellation_Propagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var sender = Create(new Handler((_, _) => throw new InvalidOperationException()));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            sender.SendTextAsync("+573053924819", "hola", DateTimeOffset.UtcNow, cts.Token)
        );
    }

    [Fact]
    public async Task TypingIndicator_SendsReadStatusAndNeverThrows()
    {
        List<JsonDocument> bodies = [];
        var sender = Create(
            new Handler(
                async (request, ct) =>
                {
                    bodies.Add(JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)));
                    return new(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{\"success\":true}"),
                    };
                }
            )
        );
        await sender.ShowTypingAsync("wamid.inbound-1", CancellationToken.None);
        var body = Assert.Single(bodies);
        Assert.Equal("whatsapp", body.RootElement.GetProperty("messaging_product").GetString());
        Assert.Equal("read", body.RootElement.GetProperty("status").GetString());
        Assert.Equal("wamid.inbound-1", body.RootElement.GetProperty("message_id").GetString());
        Assert.Equal(
            "text",
            body.RootElement.GetProperty("typing_indicator").GetProperty("type").GetString()
        );

        // Un rechazo de Meta o un error de red NO deben propagarse: el indicador
        // es cosmético.
        var failing = Create(
            new Handler(
                (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden))
            )
        );
        await failing.ShowTypingAsync("wamid.inbound-1", CancellationToken.None);
        var broken = Create(new Handler((_, _) => throw new InvalidOperationException()));
        await broken.ShowTypingAsync("wamid.inbound-1", CancellationToken.None);

        // La cancelación del llamador SÍ se propaga (disciplina OCE del módulo).
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var propagating = Create(new Handler((_, _) => throw new TaskCanceledException()));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            propagating.ShowTypingAsync("wamid.inbound-1", cts.Token)
        );
    }

    public static MetaCloudWhatsAppSender Create(HttpMessageHandler handler) =>
        new(
            new HttpClient(handler),
            new WhatsAppOptions { AccessToken = "test-only", PhoneNumberId = "test-phone" },
            TimeProvider.System,
            NullLogger<MetaCloudWhatsAppSender>.Instance
        );

    public static HttpResponseMessage Ok() =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"messages\":[{\"id\":\"wamid.test\"}]}"),
        };

    public sealed class Handler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send
    ) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken ct
        ) => send(request, ct);
    }
}
