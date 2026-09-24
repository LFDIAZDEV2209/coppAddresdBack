# Módulo de Comunidad y Clubes

Servicio standalone `CoppAddresd.Community` (puerto 5200, esquema `community`,
historial propio `community.__ef_migrations_history`): red social de pacientes
con GraphQL (HotChocolate), clubes temáticos, eventos, lives, mensajes y
storage de imágenes. Sin ProjectReferences a otros proyectos del repo.

## Arquitectura

```
┌──────────────────────────────────────────────────────────────┐
│              Community (Puerto 5200)                           │
├──────────────────────────────────────────────────────────────┤
│  GraphQL (/api/v1/community/graphql)                          │
│  ├── CommunityQuery      → perfiles, feed, trending, avatares │
│  ├── CommunityMutation [Authorize]                            │
│  │   └── createPost, likePost, votePoll, reportPost…          │
│  ├── ClubQuery / ClubMutation [Authorize] (Fase 10)           │
│  │   └── joinClubDirect, leaveClub, posts, eventos, lives…    │
│  └── CommunitySubscription (auth por interceptor)             │
├──────────────────────────────────────────────────────────────┤
│  Storage (/storage/*)                                         │
│  └── PostStorageEndpoints [RequireAuthorization]              │
│      avatares, portadas, imágenes de posts (Local o S3)       │
├──────────────────────────────────────────────────────────────┤
│  Persistence (EF Core + Npgsql)                               │
│  ├── CommunityDbContext (esquema community)                   │
│  ├── CommunitySeeder / ClubSeeder / CommunityContentSeeder    │
│  └── Métricas dashboard ERP (cola en memoria + backfill)      │
└──────────────────────────────────────────────────────────────┘
```

El Gateway (:5080) enruta `/api/v1/community/*` y `/storage/*` a este
servicio. Los clientes nunca lo llaman directo.

## Autenticación y autorización

- JWT Bearer emitido por el Auth Service (mismo `Jwt:Secret`, issuer y
  audiencias `erp`/`app`).
- `CommunityMutation` y `ClubMutation` llevan `[Authorize]` a nivel de clase;
  las operaciones de gestión exigen además políticas por claim `permission`:
  `Community.Manage` (crear/gestionar clubes), `CommunityModerator`
  (moderar) y `Community.View` (lecturas ERP).
- La identidad del actor sale **exclusiva y estrictamente del JWT** vía
  `CommunityQuery.CurrentUserId` (`ClaimTypes.NameIdentifier` → `Guid`).
  Ninguna mutación acepta `userId`/`profileId` del cliente como identidad
  (los `profileId` que reciben algunas mutaciones de gestión son el
  _objetivo_ de la acción, nunca el actor).
- Primer uso autoaprovisiona el perfil (`Me` y `RequireMyProfileAsync`).
- Blindaje anti-IDOR (Fase 10): `ClubMutation.RequireMyProfileAsync` lanza
  `GraphQLException` si no hay JWT y **jamás crea perfiles con `UserId`
  nulo** para solicitantes anónimos.

## Mutaciones auditadas (Fase 10)

| Campo GraphQL    | Método                                                           | Identidad del actor                                               |
| ---------------- | ---------------------------------------------------------------- | ----------------------------------------------------------------- |
| `joinClubDirect` | `ClubMutation.JoinClubDirect`                                    | Perfil del JWT; club público con cupo                             |
| `leaveClub`      | `ClubMutation.LeaveClub`                                         | Solo elimina la membresía propia (el último Admin no puede salir) |
| `createPost`     | `CommunityMutation.CreatePost`                                   | `ProfileId` del post = perfil del JWT                             |
| `likePost`       | `CommunityMutation.LikePost` / `ClubMutation.ToggleClubPostLike` | Like del perfil del JWT (toggle)                                  |
| `votePoll`       | `CommunityMutation.VotePoll` / `ClubMutation.VoteClubPoll`       | Voto del perfil del JWT (un voto por encuesta)                    |
| `reportPost`     | `CommunityMutation.ReportPost`                                   | `ReportedByProfileId` = perfil del JWT                            |

Reglas transversales: publicar/moderar exige rol Admin/Moderador del club;
comentar/votar/asistir exige membresía Activa (no Pendiente/Expulsado/
Silenciado). `Community.Manage` (consola ERP) exime membresía/rol.

## Clubes

### Oficiales de sistema (Fase 10, `IsSystem = true`)

| Slug                     | Nombre                 | Visibilidad |
| ------------------------ | ---------------------- | ----------- |
| `nutricion-saludable`    | Nutrición Saludable    | Público     |
| `movimiento-y-ejercicio` | Movimiento y Ejercicio | Público     |
| `mente-y-bienestar`      | Mente y Bienestar      | Privado     |
| `habitos-y-sueno`        | Hábitos y Sueño        | Público     |
| `comunidad-general`      | Comunidad General      | Público     |

No archivabes (`ArchiveClub` los rechaza). Se siembran siempre
(`SeedOfficialClubsAsync`, idempotente por slug), también en BDs ya
sembradas.

### Históricos y demo

- Sistema antiguos (destinos de `PostDestination`): Comunidad ADRES, Reto
  Caminata, Apoyo Emocional, Cocina Saludable, Solo Inactivos.
- 9 clubes demo espejo del mock app/ERP (contrato D1): solo si no hay
  clubes (guarda por slug `caminantes-adres`).

## Seeds (solo Development, al arrancar)

1. `CommunitySeeder`: perfil de sistema + ~60 perfiles, posts, comentarios,
   likes, follows, reportes, grupos/chats y DMs demo.
2. `ClubSeeder`: categorías, perfiles demo, 5 clubes oficiales (siempre),
   membresías activas + 1 post demo en Comunidad General para los pacientes
   de prueba (`55551234`, `77777777`, `88888888`, `1012345678`) — resueltos
   por documento en `app.patient_profiles` vía ADO.NET y vinculados por su
   `user_id` real (best-effort: si el esquema `app` no existe, no hace nada);
   luego los 9 clubes demo con miembros, posts, eventos, lives y solicitudes.
3. `CommunityContentSeeder`: contenido demo ANTARES (solo si
   `CommunityDemo:Enabled=true`).

## Testing

```bash
dotnet test tests/CoppAddresd.Community.UnitTests
dotnet test tests/CoppAddresd.Community.IntegrationTests  # requiere COP_TEST_DB_CONNECTION (TEMPLATE template0)
```

Cobertura Fase 10 (`ClubMutationIdentityTests`, `OfficialClubsSeedTests`,
InMemory + NSubstitute): `[Authorize]` en ambas mutaciones, actor JWT en
join/leave/like/voto/post/reporte, 401 funcional sin JWT (sin perfiles
huérfanos), los 5 clubes oficiales, idempotencia y membresías/posts de
pacientes demo.

## Documentos relacionados

- `PLAN.md` — plan del módulo.
- `analytics.md` — métricas y dashboard ERP.
- `MOBILE-INTEGRATION.md` — integración con la app móvil.
