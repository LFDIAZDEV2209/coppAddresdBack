namespace CoppAddresd.Application.DTOs.Ai;

/// <summary>
/// Solicitud de sesión de voz conversacional (ElevenLabs). El body del
/// cliente NO define identidad: el usuario/paciente proviene del JWT que el
/// controller inyecta; solo se acepta el thread opcional para correlacionar
/// la conversación de voz con el chat del paciente.
/// </summary>
public record VoiceSessionRequestDto(string? ThreadId);

/// <summary>
/// Payload interno backend → ai-service para emitir la sesión. Los ids
/// provienen del JWT del backend (jamás del cliente); el ai-service solo los
/// usa para auditoría. El <c>SignedUrl</c> viaja una sola vez en la respuesta.
/// </summary>
public record VoiceSessionInternalRequest(string UserId, string? PatientId, string? ThreadId);

/// <summary>
/// Sesión temporal de voz lista para conectar desde la app de pacientes.
/// El <c>SignedUrl</c> es de vida corta y solo viaja por HTTPS en esta
/// respuesta: jamás se persiste, se loguea ni se expone la API key.
/// </summary>
public record VoiceSessionResponseDto(string SignedUrl, string AgentId, string? ConversationId);
