using CoppAddresd.Application.Common;
using CoppAddresd.Application.DTOs.Ai;
using CoppAddresd.Application.Features.Chat;
using CoppAddresd.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Text.Json;

namespace CoppAddresd.UnitTests;

/// <summary>
/// Tests del cliente del AI Service: contrato snake_case, header X-Internal-Key
/// (Fase 1), parsing de respuesta y mapeo de errores tipados.
/// </summary>
public class AiServiceClientTests
{
    private sealed class StubHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public sealed record Captured(
            string Method,
            string Path,
            string Query,
            IReadOnlyDictionary<string, string> Headers,
            string Body);

        public List<Captured> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            Requests.Add(new Captured(
                request.Method.Method,
                request.RequestUri!.AbsolutePath,
                request.RequestUri.Query,
                request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value)),
                body));
            return responder(request);
        }
    }

    private static AiServiceClient BuildClient(StubHandler handler) => new(
        new HttpClient(handler),
        Options.Create(new AiServiceSettings
        {
            BaseUrl = "http://ai.test",
            InternalApiKey = "secret-internal-key",
            TimeoutSeconds = 30,
        }),
        NullLogger<AiServiceClient>.Instance);

    [Fact]
    public async Task ChatAsync_envia_internal_key_y_contrato_snake_case()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"answer":"hola","thread_id":"t1","execution_id":"e1","agent":"base"}"""),
        });
        var client = BuildClient(handler);

        var result = await client.ChatAsync(new ChatRequest(
            Message: "hola",
            Agent: null,
            ThreadId: "t1",
            AgentTypeId: "guid-1",
            UserId: "user-1"));

        var request = handler.Requests.Single();
        Assert.Equal("secret-internal-key", request.Headers["X-Internal-Key"]);
        Assert.Equal("/api/v1/chat", request.Path);

        using var json = JsonDocument.Parse(request.Body);
        var root = json.RootElement;
        Assert.Equal("hola", root.GetProperty("message").GetString());
        Assert.Equal("base", root.GetProperty("agent").GetString()); // default si null
        Assert.Equal("t1", root.GetProperty("thread_id").GetString());
        Assert.Equal("guid-1", root.GetProperty("agent_type_id").GetString());
        Assert.Equal("user-1", root.GetProperty("user_id").GetString());

        Assert.Equal("hola", result.Reply);
        Assert.Equal("t1", result.ThreadId);
        Assert.Equal("e1", result.ExecutionId);
        Assert.Equal("base", result.Agent);
    }

    [Fact]
    public async Task ChatAsync_campos_null_no_se_envian()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"answer":"x","thread_id":"t"}"""),
        });
        var client = BuildClient(handler);

        await client.ChatAsync(new ChatRequest(Message: "hola"));

        var request = handler.Requests.Single();
        Assert.DoesNotContain("agent_type_id", request.Body);
        Assert.DoesNotContain("user_id", request.Body);
        Assert.DoesNotContain("control_context", request.Body);
    }

    [Fact]
    public async Task ChatAsync_con_control_abierto_envia_control_context_snake_case()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"answer":"x","thread_id":"t"}"""),
        });
        var client = BuildClient(handler);

        var sendId = Guid.NewGuid();
        await client.ChatAsync(new ChatRequest(
            Message: "hola",
            UserId: "user-1",
            ControlContext: new ControlContextPayload(sendId, 14, "responded", ExamPending: true)));

        using var json = JsonDocument.Parse(handler.Requests.Single().Body);
        var ctx = json.RootElement.GetProperty("control_context");
        Assert.Equal(sendId, ctx.GetProperty("send_id").GetGuid());
        Assert.Equal(14, ctx.GetProperty("milestone_day").GetInt32());
        Assert.Equal("responded", ctx.GetProperty("status").GetString());
        Assert.True(ctx.GetProperty("exam_pending").GetBoolean());
    }

    [Fact]
    public async Task ChatAsync_mapea_control_signal_de_la_respuesta()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"answer":"ok","thread_id":"t1","control_signal":"declined"}"""),
        });
        var client = BuildClient(handler);

        var result = await client.ChatAsync(new ChatRequest(Message: "hola"));

        Assert.Equal("declined", result.ControlSignal);
        Assert.Equal("ok", result.Reply);
    }

    [Fact]
    public async Task StreamRawAsync_consume_evento_control_signal_y_reenvia_el_resto_byte_a_byte()
    {
        // Secuencia sintética del ai-service: el par `event: control_signal` +
        // `data: declined` se consume (callback) y NUNCA se reenvía; el resto
        // de las líneas viaja exactamente como llegó.
        const string raw =
            "event: start\n" +
            "data: {}\n" +
            "event: message\n" +
            "data: {\"token\":\"hola\"}\n" +
            "event: control_signal\n" +
            "data: declined\n" +
            "event: done\n" +
            "data: {\"thread_id\":\"t1\"}\n";
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(raw),
        });
        var client = BuildClient(handler);

        var signals = new List<string>();
        var chunks = new List<StreamChatChunk>();
        await foreach (var chunk in client.StreamRawAsync(
            new ChatRequest(Message: "hola"), signal => signals.Add(signal)))
        {
            chunks.Add(chunk);
        }

        var signal = Assert.Single(signals);
        Assert.Equal("declined", signal);

        var forwarded = chunks.Select(c => c.RawData).ToList();
        Assert.Equal(
            [
                "event: start",
                "data: {}",
                "event: message",
                "data: {\"token\":\"hola\"}",
                "event: done",
                "data: {\"thread_id\":\"t1\"}",
            ],
            forwarded);
        Assert.DoesNotContain(forwarded, l => l.Contains("control_signal", StringComparison.Ordinal));
        Assert.DoesNotContain(forwarded, l => l == "data: declined");
    }

    [Fact]
    public async Task StreamRawAsync_sin_callback_no_consume_nada_extra()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("event: done\ndata: {\"thread_id\":\"t1\"}\n\n"),
        });
        var client = BuildClient(handler);

        var chunks = new List<StreamChatChunk>();
        await foreach (var chunk in client.StreamRawAsync(new ChatRequest(Message: "hola")))
            chunks.Add(chunk);

        // Sin callback, el stream viaja completo (compatibilidad total con hoy).
        Assert.Equal(["event: done", "data: {\"thread_id\":\"t1\"}", ""], chunks.Select(c => c.RawData).ToList());
    }

    [Fact]
    public async Task ChatAsync_error_mapea_a_AiServiceException()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("Demasiadas solicitudes"),
        });
        var client = BuildClient(handler);

        var ex = await Assert.ThrowsAsync<AiServiceException>(() =>
            client.ChatAsync(new ChatRequest(Message: "hola")));

        Assert.Equal(429, ex.StatusCode);
        Assert.Contains("Demasiadas", ex.Detail);
    }

    [Fact]
    public async Task StreamRawAsync_envia_internal_key()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("event: done\ndata: {\"thread_id\":\"t1\"}\n\n"),
        });
        var client = BuildClient(handler);

        var chunks = new List<StreamChatChunk>();
        await foreach (var chunk in client.StreamRawAsync(new ChatRequest(Message: "hola")))
            chunks.Add(chunk);

        Assert.NotEmpty(chunks);
        var request = handler.Requests.Single();
        Assert.Equal("secret-internal-key", request.Headers["X-Internal-Key"]);
        Assert.Equal("/api/v1/chat/stream", request.Path);
    }

    [Fact]
    public async Task ExtractLabMetricsAsync_envia_multipart_form_data_y_parsea_respuesta()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"summary":"Se detectó glucosa","metrics":[{"metric_name":"glucose_fasting","value":98.2,"unit_symbol":"mg/dL","observed_at":"2026-09-01T12:00:00Z"}],"readable":true}"""),
        });
        var client = BuildClient(handler);

        using var stream = new MemoryStream("fake-file-bytes"u8.ToArray());
        var patientId = Guid.NewGuid();
        var batchId = Guid.NewGuid();

        var result = await client.ExtractLabMetricsAsync(
            patientId, batchId, stream, "exam.jpg", "image/jpeg", "t-1");

        var request = handler.Requests.Single();
        Assert.Equal("secret-internal-key", request.Headers["X-Internal-Key"]);
        Assert.Equal("/chat/lab-exam", request.Path);
        Assert.Contains("patient_id", request.Body);
        Assert.Contains(patientId.ToString(), request.Body);
        Assert.Contains("batch_id", request.Body);
        Assert.Contains(batchId.ToString(), request.Body);
        Assert.Contains("t-1", request.Body);
        Assert.Contains("exam.jpg", request.Body);

        Assert.True(result.Readable);
        Assert.Equal("Se detectó glucosa", result.Summary);
        Assert.Single(result.Metrics);
        Assert.Equal("glucose_fasting", result.Metrics[0].MetricName);
        Assert.Equal(98.2m, result.Metrics[0].Value);
        Assert.Equal("mg/dL", result.Metrics[0].UnitSymbol);
    }

    [Fact]
    public async Task ExtractLabMetricsAsync_error_502_lanza_AiServiceException()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("LLM provider unreachable"),
        });
        var client = BuildClient(handler);

        using var stream = new MemoryStream("fake-file-bytes"u8.ToArray());
        var ex = await Assert.ThrowsAsync<AiServiceException>(() =>
            client.ExtractLabMetricsAsync(Guid.NewGuid(), Guid.NewGuid(), stream, "exam.pdf", "application/pdf"));

        Assert.Equal(502, ex.StatusCode);
        Assert.Contains("LLM provider unreachable", ex.Detail);
    }

    [Fact]
    public async Task GetThreadStateAsync_mapea_messages_preservando_orden_y_roles()
    {
        // Contrato aditivo del ai-service: `messages` (rol + texto) viaja junto a
        // message_count/last_message y debe mapearse en el mismo orden del thread.
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"thread_id":"t1","message_count":3,"last_message":"chau","messages":[{"role":"user","text":"hola"},{"role":"bot","text":"buenas"},{"role":"user","text":"chau"}]}"""),
        });
        var client = BuildClient(handler);

        var result = await client.GetThreadStateAsync("t1", "user-1");

        var request = handler.Requests.Single();
        Assert.Equal("GET", request.Method);
        Assert.Equal("secret-internal-key", request.Headers["X-Internal-Key"]);
        Assert.Equal("/api/v1/threads/t1/state", request.Path);

        Assert.Equal("t1", result.ThreadId);
        Assert.Equal(3, result.MessageCount);
        Assert.Equal("chau", result.LastMessage);
        Assert.NotNull(result.Messages);
        Assert.Equal(
            [("user", "hola"), ("bot", "buenas"), ("user", "chau")],
            result.Messages!.Select(m => (m.Role, m.Text)).ToList());
    }

    [Fact]
    public async Task GetThreadStateAsync_sin_messages_degrada_a_lista_vacia()
    {
        // Compatibilidad hacia atrás: un ai-service anterior no envía `messages`;
        // el resultado debe quedar con lista vacía, sin error y conservando el resumen.
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"thread_id":"t1","message_count":2,"last_message":"viejo"}"""),
        });
        var client = BuildClient(handler);

        var result = await client.GetThreadStateAsync("t1", "user-1");

        Assert.NotNull(result.Messages);
        Assert.Empty(result.Messages!);
        Assert.Equal("t1", result.ThreadId);
        Assert.Equal(2, result.MessageCount);
        Assert.Equal("viejo", result.LastMessage);
        // Los campos aditivos de paginación degradan a sus defaults.
        Assert.False(result.HasMore);
        Assert.Null(result.NextCursor);
    }

    [Fact]
    public async Task GetThreadStateAsync_reenvia_limit_y_before_como_query_params()
    {
        // La paginación del AI Service (limit + before desde el más reciente)
        // debe viajar en la query del request saliente.
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"thread_id":"t1","message_count":10,"last_message":"x","messages":[]}"""),
        });
        var client = BuildClient(handler);

        await client.GetThreadStateAsync("t1", "user-1", limit: 5, before: 20);

        var request = handler.Requests.Single();
        Assert.Contains("user_id=user-1", request.Query);
        Assert.Contains("limit=5", request.Query);
        Assert.Contains("before=20", request.Query);
    }

    [Fact]
    public async Task GetThreadStateAsync_sin_paginacion_no_envia_limit_ni_before()
    {
        // Sin argumentos, el request no agrega limit/before: el AI Service
        // aplica sus defaults (limit=10) y la compatibilidad se mantiene.
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"thread_id":"t1","message_count":1,"last_message":"x"}"""),
        });
        var client = BuildClient(handler);

        await client.GetThreadStateAsync("t1", "user-1");

        var query = handler.Requests.Single().Query;
        Assert.DoesNotContain("limit=", query);
        Assert.DoesNotContain("before=", query);
    }

    [Fact]
    public async Task GetThreadStateAsync_mapea_has_more_y_next_cursor()
    {
        // El contrato aditivo de paginación (`has_more` + `next_cursor`) debe
        // mapearse tal cual al resultado que consume el front.
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"thread_id":"t1","message_count":25,"last_message":"x","messages":[{"role":"user","text":"hola"}],"has_more":true,"next_cursor":15}"""),
        });
        var client = BuildClient(handler);

        var result = await client.GetThreadStateAsync("t1", "user-1", limit: 10, before: 0);

        Assert.True(result.HasMore);
        Assert.Equal(15, result.NextCursor);
        Assert.Single(result.Messages!);
    }
}
