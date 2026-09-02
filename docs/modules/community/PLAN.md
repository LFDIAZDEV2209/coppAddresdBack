# Comunidades y Clubes — Análisis y diseño (PLAN)

> Estado: diseño funcional (sin código). Fase previa al proposal OpenSpec.
> Alcance: servicio Community (backend), consola ERP (coppaddresd-front) y red social (antares-paciente).

## 1. Auditoría del sistema actual

### 1.1 Backend `CoppAddresd.Community` (puerto 5200, GraphQL code-first, schema `community.`, 19 migraciones)

- **19 entidades**: Profile, Post, Comment, Like, Follow, Message, ChatGroup, ChatGroupMember, Poll/Option/Vote, XpEntry, FeedEvent, Recognition, NetworkChannel, PostReport, CommentReport, Repost
- **29 queries / 41 mutations / 6 subscriptions** (WS `MessageAdded`, `PostAdded`, `GroupMessageAdded`, `FeedEventAdded`, `CommentAdded`, `GroupChanged`)
- **Moderación**: reportes post/comentario, ban/unban con motivo, borrado soft-delete
- **Gamificación**: XP, rachas, reconocimientos, ranking, feed de eventos
- **Chat real**: DM (conversaciones derivadas) + grupos con subscriptions
- **Media**: storage propio (Local/S3, presigned), prefijos `community/avatars|covers|posts`, video MP4
- **Concepto "comunidades" hoy**: enum `PostDestination` con 6 valores hardcodeados (`ComunidadADRED`, `RetoCaminata`, `ApoyoEmocional`, `CocinaSaludable`, `SoloInactivos`, ...) + `ChatGroup` + `NetworkChannel`

### 1.2 Consola ERP (`coppaddresd-front/features/community`)

Consola administrativa: dashboard/analítica, posts + fijar/reordenar, moderación, miembros (ban/unban), rachas, regiones, diagnósticos, rewards (XP), grupos (solo tabla admin + enviar mensaje), redes externas. 13 rutas con permisos `Community.View/Manage/Moderate/Profiles`. Transporte: urql GraphQL directo a 5200 (no vía gateway) + WS subscriptions.

### 1.3 App (`antares-paciente`)

Red social completa conectada al GraphQL 5200: feed Para ti/Siguiendo con live subscription (`POST_ADDED`), perfiles con avatar/portada (presigned PUT), follows/amigos, DM + grupos con chat en vivo, posts con imagen/video/encuesta, comentarios anidados, reposts, reportes, share-link, XP/rachas, FCM push (solo tap→chat IA).

### 1.4 Infra reutilizable

| Pieza                                                 | Dónde         | Reuso para clubes                |
| ----------------------------------------------------- | ------------- | -------------------------------- |
| Motor posts/comentarios/reacciones/encuestas          | Community     | `Post` + `ClubId`                |
| Moderación (reportes, ban, restrict)                  | Community     | Club-scoped                      |
| Gamificación (XP, rachas, reconocimientos)            | Community     | Insignias/rankings de club       |
| Chat (DM + grupos, WS)                                | Community     | Chat del club                    |
| FCM push + `app.device_tokens`                        | API principal | Recordatorios/invitaciones       |
| Twilio Video (`IVideoProvider`, `tele.virtual_rooms`) | Telemedicine  | Lives (patrón a copiar)          |
| Storage con presigned                                 | Community     | Portadas/logos/medios            |
| `Community.*` permisos (4 códigos)                    | Auth          | Existen, **sin asignar a roles** |

### 1.5 Brechas

1. **Entidad Club** — no existe. `PostDestination` es enum rígido, no creable
2. **Membresía/roles/invitaciones** — nada (ChatGroupMember sin estados ni roles)
3. **Eventos/calendario** — nada en community; `tele.appointments` es dominio clínico separado
4. **Lives** — solo telemedicina (Twilio Video); community solo video grabado
5. **Notificaciones in-app** — no hay tabla/feed; solo push FCM
6. **Permisos `Community.*`** no mapeados a roles (solo Admin vía seeder)
7. **Scheduler** — nada para programar publicaciones/recordatorios
8. **QR/enlaces de invitación** — nada

## 2. Decisiones de diseño

### 2.1 PostDestination → Club (decisión A, recomendada)

Los 6 valores del enum son el concepto de comunidad actual. Migrar a **clubes semilla** (`IsSystem=true`) + `Post.ClubId` nullable. El feed social global (Para ti/siguiendo) sigue siendo posts sin `ClubId`; no rompe el contrato si se conserva el enum como capa de compat mientras el frontend migra. Alternativa B (convivir enum + clubes) duplica lógica de filtrado — descartada.

### 2.2 Notificaciones: tabla in-app + FCM

La tabla `ClubNotification` habilita centro de notificaciones en la app; FCM cubre recordatorios/invitaciones fuera de sesión. Sin tabla, solo push efímero.

### 2.3 Lives

Reusar patrón Twilio Video de Telemedicine (`IVideoProvider` + `tele.virtual_rooms`) en el servicio Community para V3. Alternativa de menor costo: YouTube Live/StreamYard embebido — decisión de costo pendiente de negocio.

## 3. Modelo de datos conceptual (schema `community.`)

```
Club(id, slug, name, description, rules, objectives, category, tags[],
     coverKey, logoKey, visibility: Publico|Privado|Invitacion, maxMembers,
     status: Activo|Archivado, createdByProfileId, IsSystem)
ClubMember(id, clubId, profileId, role: Admin|Moderador|Miembro,
           status: Activo|Pendiente|Expulsado|Silenciado, mutedUntil,
           joinedAt)  ← UNIQUE(clubId, profileId), índice por club+rol
ClubInvitation(id, clubId, profileId?, token, expiresAt, usedAt, createdBy)
ClubCategory(id, name, slug, icon)          ← catálogo admin
ClubEvent(id, clubId, title, description, type: Presencial|Virtual,
          startsAt, endsAt, location?, maxAttendees, status)
EventAttendance(id, eventId, profileId, status: Confirmado|ListaEspera|CheckIn|Cancelado)
LiveSession(id, clubId, eventId?, title, scheduledStartAt, status, providerRoom*)
ClubNotification(id, clubId, profileId, type, payload, readAt, createdAt)
ModerationLog(id, clubId, actorProfileId, targetProfileId, action, reason, createdAt)
```

Reuso directo: `Post/Comment/Like/Repost/Poll/Report` (añadir `clubId` nullable), `Message/ChatGroup` (chat del club), `XpEntry/Recognition` (scoped por club), `FeedEvent` (eventos del club), `AnalyticsAggregator` (analítica por club).

## 4. Flujos

### 4.1 ERP — crear club

Comunidad → Clubes → Nuevo → wizard (identidad/portada/logo → categoría/etiquetas → visibilidad/capacidad → reglas/objetivos → admins/moderadores) → publicar. Requiere `Community.Manage` (asignar a `OrganizationAdmin`/`ClinicAdmin` en Auth).

### 4.2 ERP — gestionar club

Tablero del club (métricas), moderación (solicitudes aprobar/rechazar, expulsar, silenciar, reportes del club), contenido (publicar/fijar/destacar/programar/borradores), eventos (crear, cupos, lista de espera, check-in), lives (programar + invitar ponentes), analítica por club (miembros activos, crecimiento, engagement).

### 4.3 App — descubrir y unirse

Explorar (categorías, tendencias, búsqueda, recomendadas) → ficha del club (portada, miembros, reglas, feed público) → Unirse: directo (público) / solicitud (privado, `ClubMember.status=Pendiente` → admin aprueba → `Activo` + push + FeedEvent) / token o QR (invitación).

### 4.4 App — participar

Feed del club (reusa `PostCard`), eventos (confirmar, check-in, recordatorio push), live (entrar, chat, reacciones), ranking del club (XP scoped), notificaciones in-app + push.

## 5. Roadmap

| Fase    | Alcance                                                                                                                                                                                                                                                                       | Esfuerzo |
| ------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------- |
| **MVP** | Entidad Club + membresía (roles/estados) + invitaciones token; Clubes en ERP (CRUD wizard + solicitudes + expulsar/silenciar); feed de club en app (posts/comentarios/encuestas scoped); migración PostDestination → clubes semilla; permisos asignados a roles; "Mis clubes" | Medio    |
| **V2**  | Eventos + asistencia + waitlist + check-in + recordatorios FCM; programación de posts (scheduler); notificaciones in-app; analítica por club; QR/enlace de invitación; moderation logs                                                                                        | Alto     |
| **V3**  | Lives (Twilio Video en community: sala + chat + reacciones + grabación); insignias/rankings de club; recomendaciones personalizadas; chat del club                                                                                                                            | Alto     |

## 6. Riesgos y decisiones pendientes

1. `PostDestination`: confirmar migración a clubes semilla (afecta contrato GraphQL y frontend ERP)
2. Lives: Twilio Video (costo) vs herramienta externa (YouTube Live/StreamYard)
3. Notificaciones: tabla in-app + FCM (recomendado) vs solo FCM
4. Escala: feed por club con keyset pagination, índice `(clubId, CreatedAt)`
5. Roles ERP: qué roles reciben `Community.Manage` (hoy ninguno)
6. Scheduler: BackgroundService en el servicio vs infra de jobs externa (no existe hoy)
