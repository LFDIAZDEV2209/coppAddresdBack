---
name: aws-production
description: 'Producción en AWS para CoppAddresd: RDS, ECS/EC2, S3, CloudWatch, secrets, IAM, networking, monitoreo. Prohibido: secretos en código.'
---

# AWS en Producción — Reglas del proyecto

El proyecto debe desplegarse correctamente en AWS. Todo lo que se escribe debe ser desplegable y diagnosticable.

## Regla de oro: secretos

**NUNCA en código/repo**: passwords, connection strings con credenciales, AWS Access Keys, tokens, PATs, API Keys.

- Config en `appsettings` (gitignoreado) en local; en AWS usar **AWS Secrets Manager** (o SSM Parameter Store para no-secretos) + variable de entorno/`IConfiguration`.
- Credenciales AWS: solo vía roles de instancia/task (IAM role), nunca keys hardcodeadas. En local: `AWS_PROFILE`/`aws sso login`.
- Connection string armado en runtime desde secrets (no exponer en logs).

## Servicios objetivo (orientativo)

| Área | Servicio sugerido |
|---|---|
| App .NET | ECS (Fargate) o EC2 detrás de ALB; tareas stateless |
| BD | RDS PostgreSQL 16 (Multi-AZ; réplica de lectura para reportes) |
| Archivos | S3 (con presigned URLs para subida/descarga; nunca servir la app desde S3 sin CDN si aplica) |
| Colas/async | SQS (procesos background, eventos entre servicios) |
| Secrets | AWS Secrets Manager |
| Logs/métricas | CloudWatch Logs + Metrics + Alarms |
| Networking | VPC privada; RDS en subred privada, sin IP pública |

## Configuración de la app .NET en AWS

- `ASPNETCORE_ENVIRONMENT=Production`; `UseUrls` explícito; HTTPS detrás de ALB (terminación TLS en ALB, `ForwardedHeaders` para X-Forwarded-Proto).
- Health checks: `/health` expuesto (endpoint aparte, no autenticado) para ALB/ECS; verificar dependencias (BD) con timeout corto.
- Connection pooling: `MaxPoolSize` según instancia RDS (skill `production-performance`); réplica de lectura en connection string secundario para lecturas pesadas.
- Graceful shutdown: ECS envía SIGTERM → app detiene background services con CancellationToken (skill `cancellation-token`).

## RDS

- Índices antes de migrar datos grandes (skill `migrations`); `ANALYZE` post-migración.
- `max_connections` del tamaño de instancia > suma de pools; monitorear `connection utilization`.
- Backups automáticos + restauración probada; snapshots para migraciones.
- Escala: empezar pequeño, subir por métricas; usar réplica para lecturas de reportes.

## CloudWatch / monitoreo

- Logs estructurados (JSON) con Correlation ID (skill `logging`).
- Métricas: latencia p50/p95/p99 (custom metric o API Gateway/ALB), 4xx/5xx, duración de query BD, conexiones BD, CPU/memoria.
- Alarmas mínimas: 5xx alto, latencia p95 alto, CPU RDS > 80% sostenido, connection utilization > 80%.
- Log groups con retención acorde (30 días default razonable; 90 si auditoría).

## Networking y seguridad

- RDS solo accesible desde el security group de las tareas/app.
- ALB público; app en subred privada.
- IAM: mínimo privilegio (task role con permisos solo a los recursos que usa: SecretsManager get, S3 bucket específico, SQS queue).
- No abrir puertos de BD a Internet; no SSH sin bastion/IP restringido.

## Costo

- Evaluar costo por decisión: instancias, RDS storage, DataTransfer, CloudWatch (log ingestion cuesta), S3 (IA/GI para logs viejos).
- No loguear en exceso (costo + ruido).

## Operación

- Despliegues: imagen Docker, tag inmutable, rollback simple (redeploy de tag anterior).
- Probar: health checks, restore, escalado, fallo de AZ (Multi-AZ), failover.
- Documentar la arquitectura AWS en `docs/aws/production.md` (diagrama, recursos, cómo diagnosticar un incidente).
