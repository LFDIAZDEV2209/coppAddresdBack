namespace CoppAddresd.Application.DTOs.Ai;

/// <summary>
/// Retroalimentación del paciente sobre una respuesta del chat IA (Fase 9).
/// Viaja al ai-service como <c>execution_id</c>, <c>thread_id</c>,
/// <c>rating</c> (1-5), <c>comment</c> y <c>user_id</c> (inyectado desde el JWT).
/// </summary>
public record ChatFeedbackRequestDto(
    string ExecutionId,
    string ThreadId,
    int Rating,
    string? Comment
);

/// <summary>
/// Respuesta del ai-service al feedback: eco del thread/rating más si la
/// adaptive memory guardó una experiencia aprendida.
/// </summary>
public record ChatFeedbackResponseDto(
    string ThreadId,
    int Rating,
    bool ExperienceSaved,
    string? Outcome
);
