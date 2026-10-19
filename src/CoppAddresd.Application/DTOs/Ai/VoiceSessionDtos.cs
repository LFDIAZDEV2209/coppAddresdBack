namespace CoppAddresd.Application.DTOs.Ai;

/// <summary>
/// Solicitud de sesión de voz conversacional (ElevenLabs). El body del
/// cliente NO define identidad: el usuario/paciente proviene del JWT que el
/// controller inyecta; solo se aceptan el thread opcional (correlación con
/// el chat) y el idioma de la app ('es' | 'en') para que el agente de voz
/// arranque hablando el idioma de la app.
/// </summary>
public record VoiceSessionRequestDto(string? ThreadId, string? Language = null);

/// <summary>
/// Payload interno backend → ai-service para emitir la sesión. Los ids
/// provienen del JWT del backend (jamás del cliente); el ai-service solo los
/// usa para auditoría. El <c>SignedUrl</c> viaja una sola vez en la respuesta.
/// El <c>Language</c> ('es' | 'en') viaja si la app lo envió; con null el
/// ai-service no toca la configuración del agente (compatible hacia atrás).
/// </summary>
public record VoiceSessionInternalRequest(string UserId, string? PatientId, string? ThreadId, string? Language = null);

/// <summary>
/// Sesión temporal de voz lista para conectar desde la app de pacientes.
/// El <c>SignedUrl</c> es de vida corta y solo viaja por HTTPS en esta
/// respuesta: jamás se persiste, se loguea ni se expone la API key.
/// </summary>
public record VoiceSessionResponseDto(string SignedUrl, string AgentId, string? ConversationId);
