using System.Net;
using System.Text.Json;
using CoppAddresd.Telemedicine.Application.Interfaces;
using CoppAddresd.Telemedicine.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace CoppAddresd.Telemedicine.UnitTests;

/// <summary>
/// Contrato HTTP de entrega F2 (<c>TelemedicineNotifier</c>): path interno,
/// shape camelCase ({ userId, title, body, channels ["Push","Sms"], data,
/// dedupeKey }) y best-effort sin excepciones (2xx → true; rechazo, timeout o
/// red caída → false + log).
/// </summary>
public class TelemedicineNotifierTests
{
    private sealed class CaptureHandler(HttpStatusCode status, Exception? throws = null)
        : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Request = request;
            Body = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            if (throws is not null)
            {
                throw throws;
            }

            return new HttpResponseMessage(status) { Content = new StringContent("{}") };
        }
    }

    private static TelemedicineNotifier CreateNotifier(HttpMessageHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("http://backend.local") },
            NullLogger<TelemedicineNotifier>.Instance
        );

    private static TelemedicineNotification Notification() =>
        new(
            TestData.PatientUserId,
            "Recordatorio de tu cita",
            "Tu cita es pronto.",
            [TelemedicineNotificationChannel.Push, TelemedicineNotificationChannel.Sms],
            new Dictionary<string, string>
            {
                ["appointmentId"] = TestData.PatientId.ToString(),
                ["screen"] = "room",
            },
            "appointment:x:reminder-1h"
        );

    [Fact]
    public async Task SendAsync_Aceptado_EnviaElContratoExacto()
    {
        var handler = new CaptureHandler(HttpStatusCode.OK);
        var notifier = CreateNotifier(handler);

        var accepted = await notifier.SendAsync(Notification());

        Assert.True(accepted);
        Assert.Equal(
            "/api/v1/internal/telemedicine/notifications",
            handler.Request!.RequestUri!.AbsolutePath
        );
        using var json = JsonDocument.Parse(handler.Body!);
        var root = json.RootElement;
        Assert.Equal(TestData.PatientUserId.ToString(), root.GetProperty("userId").GetString());
        Assert.Equal("Recordatorio de tu cita", root.GetProperty("title").GetString());
        Assert.Equal("Tu cita es pronto.", root.GetProperty("body").GetString());
        Assert.Equal("Push", root.GetProperty("channels")[0].GetString());
        Assert.Equal("Sms", root.GetProperty("channels")[1].GetString());
        Assert.Equal("room", root.GetProperty("data").GetProperty("screen").GetString());
        Assert.Equal(
            TestData.PatientId.ToString(),
            root.GetProperty("data").GetProperty("appointmentId").GetString()
        );
        Assert.Equal(
            "appointment:x:reminder-1h",
            root.GetProperty("dedupeKey").GetString()
        );
    }

    [Fact]
    public async Task SendAsync_BackendRechaza_DevuelveFalse()
    {
        var notifier = CreateNotifier(new CaptureHandler(HttpStatusCode.ServiceUnavailable));

        var accepted = await notifier.SendAsync(Notification());

        Assert.False(accepted);
    }

    [Fact]
    public async Task SendAsync_RedCaida_DevuelveFalseSinLanzar()
    {
        var notifier = CreateNotifier(
            new CaptureHandler(HttpStatusCode.OK, new HttpRequestException("backend caído"))
        );

        var accepted = await notifier.SendAsync(Notification());

        Assert.False(accepted);
    }

    [Fact]
    public async Task SendAsync_Timeout_DevuelveFalseSinLanzar()
    {
        var notifier = CreateNotifier(
            new CaptureHandler(HttpStatusCode.OK, new TaskCanceledException("timeout"))
        );

        var accepted = await notifier.SendAsync(Notification());

        Assert.False(accepted);
    }
}
