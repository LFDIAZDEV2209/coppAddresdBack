---
name: service-layer
description: 'Capa de aplicación y servicios en CoppAddresd: handlers MediatR, validación con pipeline, orquestación, reglas de diseño.'
---

# Capa de Servicios / Application — Reglas del proyecto

La capa de aplicación contiene los casos de uso (CQRS con MediatR). Orchesta, no implementa infraestructura.

## Piezas y ubicación

| Pieza | Ubicación |
|---|---|
| `IRequest<T>` Command/Query | `Application/Features/<Modulo>/Commands|Queries/` |
| `IRequestHandler` | `Application/Features/<Modulo>/Handlers/` |
| Validadores FluentValidation | junto al Command/Query (o `Validators/`) |
| Pipeline behaviors | `Application/Behaviors/` (validación, logging, medición) |
| DTOs | `Application/DTOs/` o junto al feature |
| Interfaces de servicios | `Application/Interfaces/` |

## Handler ideal (patrón)

```csharp
public sealed record GetOrderQuery(Guid OrderId) : IRequest<OrderDto>;

public sealed class GetOrderHandler(IOrderRepository orders) : IRequestHandler<GetOrderQuery, OrderDto>
{
    public async Task<OrderDto> Handle(GetOrderQuery request, CancellationToken ct)
    {
        var order = await orders.GetByIdAsync(request.OrderId, ct)
            ?? throw new NotFoundException(nameof(Order), request.OrderId);

        return new OrderDto(order.Id, order.Total, order.Status.ToString());
    }
}
```

- Primario constructor (C# 12+) para DI.
- Query de solo lectura: `AsNoTracking`, proyección, paginación (skills `linq`, `pagination`).
- Command: valida estado de dominio (reglas en Domain), persiste vía repositorio/contexto, transacciones solo si es multipaso (skill `transactions`).

## Validación (pipeline, no manual)

- Reglas de input → FluentValidation en el validador del command/query.
- Behavior de validación en MediatR (`IPipelineBehavior`) que valida antes de ejecutar el handler y lanza `ValidationException` (400 en la API).
- Reglas de negocio del dominio → métodos de dominio/excepciones de dominio, NO FluentValidation (esa es la diferencia: input vs negocio).

## Reglas

- **Handlers delgados**: coordenar, no implementar lógica pesada inline; lógica de negocio → Domain; acceso a datos → interfaces.
- **Un caso de uso por handler** (SRP); sin handlers "god".
- No referenciar Infrastructure desde Application (DIP: Application define interfaces, Infrastructure implementa).
- `CancellationToken` propagado en todo el flujo (skill `cancellation-token`).
- Excepciones: lanzar excepciones de dominio/aplicación (`NotFoundException`, `BusinessRuleViolationException`, ...) — el middleware las traduce a HTTP (skill `error-handling`).
- Efectos secundarios (email, S3, SQS): tras el commit, vía evento/cola — nunca bloquear dentro del flujo síncrono ni dentro de la transacción (skill `transactions`).

## Composición y reutilización

- Queries duplicadas → centralizar (repositorio o query service, skill `repository-pattern`).
- Nada de `async void`, nada de lógica en constructores.
- Medir handlers (timing en log) para observabilidad (skill `logging`).

## Anti-patrones

- Lógica de negocio en la capa HTTP o en el repositorio.
- Servicios de aplicación "utils" con métodos sueltos sin cohesión.
- Depender de DbContext directamente en Application.
- Validaciones duplicadas (FluentValidation + ifs manuales).
