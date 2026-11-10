using System.Net.Http.Json;
using System.Text.Json;
using CoppAddresd.Application.Common;
using CoppAddresd.Application.Features.Nutrition.Cases;
using CoppAddresd.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace CoppAddresd.Infrastructure.Services;

public sealed class NutritionDraftClient(HttpClient client, IOptions<AiServiceSettings> settings) : INutritionDraftClient
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    public async Task<NutritionDraftResponse> GenerateAsync(NutritionDraftRequest request, CancellationToken ct)
    {
        try
        {
            using var call = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(settings.Value.BaseUrl), "/internal/nutrition/plan-draft"));
            call.Headers.Add("X-Internal-Key", settings.Value.InternalApiKey);
            call.Options.Set(AiConsentSubject.Key, new("patient-ai", PatientId: request.SubjectPatientId));
            call.Content = JsonContent.Create(request, options: Json);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct); deadline.CancelAfter(TimeSpan.FromSeconds(50));
            using var response = await client.SendAsync(call, deadline.Token);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<NutritionDraftResponse>(Json, deadline.Token)
                ?? throw new JsonException("Respuesta de nutrición vacía.");
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && ex is HttpRequestException or JsonException or OperationCanceledException)
        {
            return new(new("Propuesta determinista pendiente de revisión.", "Deterministic proposal pending review."),
                new("Revisar porciones y alertas antes de aprobar.", "Review portions and alerts before approval."), request.Menus, "deterministic_fallback");
        }
    }
}
