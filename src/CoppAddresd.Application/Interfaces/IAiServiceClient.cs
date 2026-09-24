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

    /// <summary>
    /// Stream crudo (líneas SSE verbatim) del chat. Cuando
    /// <paramref name="onControlSignal"/> no es null, las líneas
    /// <c>event: control_signal</c> + la siguiente <c>data:</c> (señal interna
    /// de la fase 2 de controles) se CONSUMEN en el cliente y se entregan por
    /// el callback — jamás se yield downstream; el resto del stream viaja
    /// byte a byte.
    /// </summary>
    IAsyncEnumerable<StreamChatChunk> StreamRawAsync(
        ChatRequest request,
        Action<string>? onControlSignal = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Solicita la generación de un plan (<c>nutrition</c> | <c>exercise</c>)
    /// condicionado por el contexto clínico consolidado y las restricciones de
    /// seguridad aplicables al paciente.
    /// </summary>
    Task<AiPlanResult> GeneratePlanAsync(
        string type,
        ClinicalContextDto context,
        IReadOnlyList<RestrictionDto> restrictions,
        CancellationToken ct = default
    );

    /// <summary>
    /// Inyecta un mensaje proactivo del bot en el thread estable del usuario
    /// (sin LLM, costo cero). Best-effort: si el AI Service no está disponible
    /// el caller decide si falla la operación global (el push es lo principal).
    /// <paramref name="role"/>: <c>bot</c> (visible en el historial) o
    /// <c>system</c> (solo contexto del LLM, invisible para la app).
    /// </summary>
    Task<ProactiveMessageResult> ProactiveMessageAsync(
        Guid userId,
        string message,
        string agentTypeId = "base",
        string role = "bot",
        CancellationToken ct = default
    );

    /// <summary>
    /// Lee el estado del historial de un thread del AI Service (canal interno
    /// con X-Internal-Key): resumen (messageCount + lastMessage) y —contrato
    /// aditivo— los mensajes visibles en orden (la página solicitada, tope 100
    /// visibles). <paramref name="limit"/> y <paramref name="before"/>
    /// paginan el historial desde el más reciente (<c>before</c> = offset
    /// devuelto como <c>nextCursor</c>); sin valor, el AI Service aplica sus
    /// defaults. Es lo que el paciente debe ver al abrir el chat tras un
    /// re-login o un push proactivo.
    /// </summary>
    Task<ThreadStateResult> GetThreadStateAsync(
        string threadId,
        string userId,
        int? limit = null,
        int? before = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Registra el feedback del paciente sobre una respuesta del chat
    /// (rating 1-5 + comentario opcional). Canal interno con X-Internal-Key;
    /// alimenta la adaptive memory del AI Service. El <c>userId</c> proviene
    /// del JWT del backend, nunca del cliente.
    /// </summary>
    Task<ChatFeedbackResponseDto> SendFeedbackAsync(
        ChatFeedbackRequestDto request,
        string userId,
        CancellationToken ct = default
    );

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
        CancellationToken ct = default
    );

    /// <summary>
    /// Genera la narración empática del examen (segundo request, canal interno
    /// con X-Internal-Key) a partir de la tabla de evolución pre-computada en
    /// .NET. Best-effort por contrato: cualquier fallo (404 de un ai-service
    /// anterior, timeout, red, payload inválido) devuelve cadena vacía y el
    /// caller usa el summary — nunca lanza ni debe romper el upload ni disparar
    /// la compensación S3.
    /// </summary>
    Task<string> NarrateLabExamAsync(
        Guid patientId,
        Guid batchId,
        IReadOnlyList<LabExamAiMetricDto> metrics,
        IReadOnlyDictionary<string, MetricEvolution> previousMeasurements,
        string? language,
        CancellationToken ct = default
    );
}
