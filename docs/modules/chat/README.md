# Módulo de Chat / Integración AI

Integración con el AI Service (Python) vía HTTP, con soporte para chat síncrono y streaming SSE.

## Arquitectura

```
┌─────────────────────────────────────────────────────────────┐
│                    API (Puerto 5122)                          │
├─────────────────────────────────────────────────────────────┤
│  Controllers                                                │
│  └── ChatController                                         │
│      ├── POST /api/v1/chat          → Chat síncrono         │
│      └── POST /api/v1/chat/stream   → Streaming SSE         │
├─────────────────────────────────────────────────────────────┤
│  Application (MediatR)                                      │
│  ├── ChatCommand / ChatCommandHandler                       │
│  └── StreamChatCommand / StreamChatCommandHandler           │
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
└─────────────────────────────────────────────────────────────┘
```

## Decisiones de diseño

| Decisión | Razón |
|----------|-------|
| **HTTP client (no gRPC)** | AI Service es Python/FastAPI. HTTP es más simple y compatible. |
| **Polly resilience** | Retry con backoff exponencial + circuit breaker para tolerancia a fallos. |
| **SSE para streaming** | Server-Sent Events es estándar, compatible con todos los navegadores. |
| **MediatR handlers** | Consistencia con patrón CQRS del resto de la aplicación. |
| **JWT en API** | Solo usuarios autenticados pueden usar el chat. |

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
record ChatRequestDto(string Message, string? Agent = null, string? ThreadId = null);
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
