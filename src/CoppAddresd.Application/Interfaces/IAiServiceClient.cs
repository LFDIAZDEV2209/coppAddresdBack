using CoppAddresd.Application.DTOs.Ai;
using CoppAddresd.Application.DTOs.LabExam;
using CoppAddresd.Application.Features.Chat;
using CoppAddresd.Application.Features.Threads;
using CoppAddresd.Application.Features.Wellness;

namespace CoppAddresd.Application.Interfaces;

public interface IAiServiceClient
{
    Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct = default);

    IAsyncEnumerable<SseEvent> StreamChatAsync(ChatRequest request, CancellationToken ct = default);

    IAsyncEnumerable<StreamChatChunk> StreamRawAsync(ChatRequest request, CancellationToken ct = default);

    /// <summary>
    /// Solicita la generación de un plan (<c>nutrition</c> | <c>exercise</c>)
    /// condicionado por el contexto clínico consolidado y las restricciones de
    /// seguridad aplicables al paciente.
    /// </summary>
    Task<AiPlanResult> GeneratePlanAsync(
        string type,
        ClinicalContextDto context,
        IReadOnlyList<RestrictionDto> restrictions,
        CancellationToken ct = default);

    /// <summary>
    /// Inyecta un mensaje proactivo del bot en el thread estable del usuario
    /// (sin LLM, costo cero). Best-effort: si el AI Service no está disponible
    /// el caller decide si falla la operación global (el push es lo principal).
    /// </summary>
    Task<ProactiveMessageResult> ProactiveMessageAsync(
        Guid userId,
        string message,
        string agentTypeId = "base",
        CancellationToken ct = default);

    /// <summary>
    /// Lee el resumen del historial de un thread del AI Service (canal interno
    /// con X-Internal-Key). Devuelve el último mensaje del thread, que es lo
    /// que el paciente debe ver al abrir el chat tras un push proactivo.
    /// </summary>
    Task<ThreadStateResult> GetThreadStateAsync(
        string threadId,
        string userId,
        CancellationToken ct = default);

    /// <summary>
    /// Envía un archivo de examen de laboratorio al AI Service vía multipart/form-data
    /// y devuelve las métricas extraídas y el resumen textual para el chat.
    /// Canal interno (X-Internal-Key); el frontend jamás llama directo.
    /// </summary>
    Task<LabExamAiResponse> ExtractLabMetricsAsync(
        Guid patientId,
        Guid batchId,
        Stream fileStream,
        string fileName,
        string contentType,
        string? threadId = null,
        CancellationToken ct = default);
}
