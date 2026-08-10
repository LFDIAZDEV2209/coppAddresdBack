# AWS — Producción

Skill: `aws-production`. Estado: **pendiente de implementar** (esqueleto). Este documento define el target y se actualizará con el despliegue real.

## Diagrama objetivo (texto)

```
Internet → ALB (TLS) → ECS/Fargate (CoppAddresd.Api) ─┐
                              ├─ Auth service (JWT/Identity) → RDS PostgreSQL (privado)
                              ├─ Secrets Manager (credenciales)
                              ├─ S3 (archivos, presigned URLs)
                              └─ SQS (background/eventos, si aplica)
CloudWatch: Logs + Metrics + Alarms
```

- App en subredes privadas; RDS en subred privada sin IP pública; security groups mínimos.
- Multi-AZ RDS; réplica de lectura para reportes.

## Secretos — regla de oro

**Nunca** en código/repo: passwords, connection strings con credenciales, AWS keys, tokens, PATs, API keys.

- Local: `appsettings` (gitignoreado) / User Secrets.
- AWS: Secrets Manager + IAM role de la task (mínimo privilegio: `secretsmanager:GetSecretValue` sobre el secreto, S3 sobre el bucket propio, SQS sobre la cola propia).
- Connection string construido en runtime desde Secrets; jamás en logs.

## Configuración de la app

- `ASPNETCORE_ENVIRONMENT=Production`; `ForwardedHeaders` para `X-Forwarded-Proto` (TLS termina en ALB).
- Health check `/health` (no autenticado, sin secretos, verifica BD con timeout corto).
- `MaxPoolSize` coherente con `max_connections` del RDS (presupuesto de conexiones = max_connections / instancias).
- Graceful shutdown: SIGTERM → background services detienen con CancellationToken.

## RDS

- Índices antes de migrar datos grandes; `ANALYZE` post-migración (skills `migrations`, `database-indexes`).
- Backups automáticos + restore probado; snapshots pre-migración.
- Monitorear: connection utilization, CPU, deadlocks, disk.
- Migraciones de esquema: ver skill `migrations` (CONCURRENTLY, NOT VALID, chunking, fuera de pico).

## CloudWatch

- Logs estructurados (JSON) con Correlation ID (skill `logging`).
- Métricas: latencia p50/p95/p99, 4xx/5xx, duración de query, conexiones BD.
- Alarmas mínimas: 5xx alto, p95 alto, CPU RDS > 80% sostenido, connection utilization > 80%.
- Retención: 30 días default (90 si auditoría).

## Costo

- Evaluar por decisión: instancias, storage RDS, DataTransfer, CloudWatch ingestion, S3 lifecycle para logs viejos.
- Sin logs excesivos (costo + ruido).

## Runbook (a completar en despliegue)

| Incidente | Diagnóstico | Acción |
|---|---|---|
| 5xx masivos | CloudWatch errores + logs | revisar deploy reciente / BD / secretos |
| Latencia alta p95 | métricas por endpoint + duración query | plan de ejecución, índices, cache |
| BD sin conexiones | connection utilization | pool size / instancia / queries largas |
| 401/403 | logs auth | claims/issuer/jwks del servicio Auth |

## Checklist de despliegue

- [ ] Sin secretos en imagen/config/repo
- [ ] Health check funcionando y registrado en ALB/ECS
- [ ] IAM mínimo privilegio
- [ ] Backups + restore probado
- [ ] Alarmas configuradas
- [ ] Rollback probado (tag inmutable de imagen)
- [ ] Índices/migraciones verificadas para producción (skills `migrations`, `database-indexes`)
