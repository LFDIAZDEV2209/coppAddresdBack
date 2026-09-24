# Módulo de Chat / Integración AI

Integración con el AI Service (Python) vía HTTP, con soporte para chat síncrono y streaming SSE.

## Arquitectura

```
┌─────────────────────────────────────────────────────────────┐
│                    API (Puerto 5122)                          │
├─────────────────────────────────────────────────────────────┤
│  Controllers                                                │
│  ├── ChatController [Authorize]                             │
│  │   ├── POST /api/v1/chat          → Chat síncrono         │
│  │   ├── POST /api/v1/chat/stream   → Streaming SSE         │
│  │   └── POST /api/v1/chat/feedback → Feedback (Fase 9)     │
│  └── ThreadsController [Authorize] (Fase 9, anti-IDOR)      │
│      └── GET /api/v1/threads/{id}/messages → Historial      │
├─────────────────────────────────────────────────────────────┤
│  Application (MediatR)                                      │
│  ├── ChatCommand / ChatCommandHandler                       │
│  ├── StreamChatCommand / StreamChatCommandHandler           │
│  └── SendChatFeedbackCommand / Handler + Validator (Fase 9) │
├─────────────────────────────────────────────────────────────┤
│  Infrastructure                                             │
│  ├── IAiServiceClient (interfaz)                            │
│  ├── AiServiceClient (HTTP + Polly resilience)              │
│  └── HttpClientResilienceExtensions (retry + circuit breaker)│
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼ HTTP
┌─────────────────────────────────────────────────────────────┐
│              AI Service (Python, Puerto 8000)                │
│  POST /api/v1/chat          → Respuesta JSON completa       │
│  POST /api/v1/chat/stream   → SSE stream (text/event-stream)│
│  POST /api/v1/chat/feedback → Feedback 1-5 (adaptive memory)│
└─────────────────────────────────────────────────────────────┘
```

## Decisiones de diseño

| Decisión                  | Razón                                                                     |
| ------------------------- | ------------------------------------------------------------------------- |
| **HTTP client (no gRPC)** | AI Service es Python/FastAPI. HTTP es más simple y compatible.            |
| **Polly resilience**      | Retry con backoff exponencial + circuit breaker para tolerancia a fallos. |
| **SSE para streaming**    | Server-Sent Events es estándar, compatible con todos los navegadores.     |
| **MediatR handlers**      | Consistencia con patrón CQRS del resto de la aplicación.                  |
| **JWT en API**            | Solo usuarios autenticados pueden usar el chat.                           |

## Flujo de chat síncrono

```
1. POST /api/v1/chat { "message": "hola" }
2. ChatController → MediatR ChatCommand
3. ChatCommandHandler → IAiServiceClient.ChatAsync()
4. HTTP POST a AI Service /api/v1/chat
5. AI Service procesa (guardrails → agent → tools → END)
6. Respuesta JSON { "reply": "...", "threadId": "..." }
7. Retornar ChatResponse al cliente
```

## Flujo de chat streaming

```
1. POST /api/v1/chat/stream { "message": "hola" }
2. ChatController → MediatR StreamChatCommand
3. StreamCommandHandler → IAiServiceClient.StreamRawAsync()
4. HTTP POST a AI Service /api/v1/chat/stream
5. AI Service retorna SSE stream
6. Controller lee línea por línea (IAsyncEnumerable)
7. Cada línea se envía al cliente como text/event-stream
8. Cliente recibe eventos: start, token, node, done, error
```

## Historial de thread (proxy de lectura, blindaje anti-IDOR Fase 9)

La app móvil reconstruye la conversación tras un re-login con
`GET /api/v1/threads/{threadId}/messages` (`ThreadsController`):

```
1. [Authorize] a nivel de controlador: solo JWT válido (401 sin sesión).
2. El dueño del thread sale estricta y exclusivamente del JWT
   (ClaimTypes.NameIdentifier); el query param `userId` se conserva por
   compatibilidad pero se IGNORA siempre — nunca define identidad ni alcance.
3. Backend → AI Service GET /api/v1/threads/{thread_id}/state?user_id=…&limit=…&before=… (canal interno X-Internal-Key).
4. Respuesta: { threadId, messageCount, lastMessage, messages: [{ role: "user"|"bot", text }], hasMore, nextCursor }
5. Paginación hacia atrás: `before` saltea mensajes visibles desde el más nuevo y `nextCursor` es el `before` de la próxima página.
6. Si el AI Service falla o el thread no existe → 200 con estado vacío (nunca 500).
```

Contrato aditivo: `messages` viaja en orden cronológico y el AI Service
devuelve la página solicitada (paginación por `limit`/`before`, tope 100
visibles). Un ai-service anterior no envía el campo: el backend lo degrada a
lista vacía sin romper `messageCount` ni `lastMessage`.

## Feedback del chat (Fase 9, adaptive memory)

El paciente califica una respuesta del asistente con
`POST /api/v1/chat/feedback` (`ChatController`, `[Authorize]`):

```
1. POST /api/v1/chat/feedback { executionId, threadId, rating (1-5), comment? }
2. ChatController inyecta el userId desde el JWT en el comando
   (el body nunca define quién califica) → MediatR SendChatFeedbackCommand
   (validado: execution/thread requeridos, rating 1-5, comment ≤2000).
3. SendChatFeedbackCommandHandler → IAiServiceClient.SendFeedbackAsync()
   (propaga CancellationToken).
4. HTTP POST al AI Service /api/v1/chat/feedback con
   { execution_id, thread_id, rating, comment, user_id } + X-Internal-Key.
5. Respuesta: { threadId, rating, experienceSaved, outcome }.
```

Errores (sin filtrar trazas internas, mensaje clínico amigable):

| Caso                                             | HTTP                    | `detail`                                                                              |
| ------------------------------------------------ | ----------------------- | ------------------------------------------------------------------------------------- |
| AI Service responde error (`AiServiceException`) | 502 Bad Gateway         | "El asistente inteligente no está disponible temporalmente. Intente en unos minutos." |
| AI Service offline/red (`HttpRequestException`)  | 503 Service Unavailable | El mismo mensaje clínico.                                                             |
| Sin JWT                                          | 401 Unauthorized        | "Usuario no identificado."                                                            |

Todo en `ProblemDetails` (RFC 7807).

## Resilience (Polly)

```csharp
// Configuración en HttpClientResilienceExtensions.cs
.AddResiliencePolicy()
  → Retry: 3 intentos, backoff exponencial (2^n segundos)
  → Circuit Breaker: 5 fallos consecutivos → abrir por 30 segundos
```

**Escenarios**:

- AI Service temporalmente lento → retry ayuda
- AI Service caído → circuit breaker abre, falla rápido sin saturar
- AI Service vuelve → circuit breaker cierra automáticamente

## Errores y auto-re-sincronización (resiliencia de negocio)

El AI Service responde 404 cuando el agente aún no está en su registry
(`ai.agent_runtime_configs`): la activación de versión sincroniza la config
vía `/internal/agents/sync-config`, pero el sync es best-effort y puede
fallar si el AI Service está caído en ese momento. Para no exponer ese 404
al cliente como error genérico:

- `AiServiceClient` lanza `AiServiceException` (StatusCode + Detail) en lugar
  de `EnsureSuccessStatusCode` (`ThrowForResponseAsync`).
- `ChatCommandHandler` y `StreamChatCommandHandler` detectan el 404 con
  `AgentTypeId` y reintentan UNA vez tras re-sincronizar la config activa del
  agente con `AgentRuntimeReconciler.TryResyncAsync` (transaccional: el agente
  con su `ActiveVersion` se sincroniza al runtime). En streaming el retry se
  hace sobre el primer `MoveNextAsync` (el enumerador lazy no permite
  try/catch con yield).
- `ChatController` traduce `AiServiceException` → 502 con el detalle real del
  AI Service; cualquier otra excepción → 500 "AI service unavailable".

## Configuración

```json
{
  "AiService": {
    "BaseUrl": "http://localhost:8000",
    "ApiPrefix": "/api/v1",
    "TimeoutSeconds": 120
  }
}
```

**Timeout**: 120 segundos para permitir respuestas largas del LLM.

## DTOs

### Request

```csharp
record ChatRequestDto(string Message, string? Agent = null, string? ThreadId = null, string? AgentTypeId = null);
```

### Feedback (Fase 9)

```csharp
record ChatFeedbackRequestDto(string ExecutionId, string ThreadId, int Rating, string? Comment);
record ChatFeedbackResponseDto(string ThreadId, int Rating, bool ExperienceSaved, string? Outcome);
```

### Response (síncrono)

```csharp
record ChatResponse(string Reply, string ThreadId);
```

### SSE Events (streaming)

```
event: start
data: {"threadId": "abc123"}

event: token
data: {"content": "Hola"}

event: token
data: {"content": " mundo"}

event: done
data: {}
```

## Testing

```bash
# Chat síncrono (requiere AI Service corriendo)
curl -X POST http://localhost:5122/api/v1/chat \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"message": "hola"}'

# Chat streaming
curl -X POST http://localhost:5122/api/v1/chat/stream \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"message": "hola"}' \
  --no-buffer

# Feedback (Fase 9: executionId sale de la respuesta del chat)
curl -X POST http://localhost:5122/api/v1/chat/feedback \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{"executionId": "<execution_id>", "threadId": "<thread_id>", "rating": 5, "comment": "Muy útil"}'

# Historial del thread (anti-IDOR: el userId sale del JWT, el query param se ignora)
curl "http://localhost:5122/api/v1/threads/<thread_id>/messages?limit=10" \
  -H "Authorization: Bearer <token>"
```

## TODO / Mejoras futuras

- [ ] Integrar `HttpAuditActorContext` con Identity para capturar userId en auditoría
- [ ] Agregar métricas (latencia, tokens usados, errores)
- [ ] Rate limiting específico para endpoints de chat
- [ ] Caché de respuestas frecuentes (opcional)
- [ ] Soporte para uploads de archivos (imágenes, PDFs)
- [ ] Historial de conversaciones persistente (lado .NET)
- [ ] WebSockets como alternativa a SSE (bidireccional)
- [ ] Health check específico para AI Service (más allá de Polly)
