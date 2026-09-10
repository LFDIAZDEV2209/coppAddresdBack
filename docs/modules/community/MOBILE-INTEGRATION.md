# MOBILE-INTEGRATION — Módulo de Clubes en antares-paciente

> Plan concreto para conectar la app móvil `antares-paciente` al backend de
> clubes del servicio Community. Extiende [`PLAN.md`](./PLAN.md) (diseño
> funcional + roadmap). El backend ya implementa el contrato D1 congelado
> (`antares-paciente/src/graphql/clubs.ts` = `coppaddresd-front/features/community/clubs/types.ts`).
> Precedente de formato: `docs/modules/program-progress/MOBILE-INTEGRATION.md`.

---

## 1. Estado actual

| Sistema | Estado | Detalle |
|---|---|---|
| **Backend** (`CoppAddresd.Community`, :5200 vía gateway :5080) | ✅ 100% implementado | 14 queries, 30 mutations, 4 subscriptions, resolvers `MyMembership`/`MyAttendance`, `ClubSeeder` (5 clubes sistema + 2 demo con perfiles). Schema `community.`. Auth `[Authorize]` con auto-provisión de perfil (`Me` → `Profile` por `UserId` del JWT). |
| **ERP** (`coppaddresd-front`) | ✅ 100% conectado | `services/clubs-service.ts` (urql imperativo `query`/`mutate`), `mock/clubs-api.ts` (wire → contrato D1), `resolveStorageUrl()` (URLs relativas `/storage/...` → gateway). **Referencia de implementación.** |
| **App móvil** (`antares-paciente`) | 🟡 Infra lista, clubes en mock | Cliente urql real con Bearer + refresh + WS (`graphql/client.ts`), `useCommunity.ts` (feed/posts/chat reales), `utils/apiBaseUrl.ts` (gateway; Vite proxy `/api/v1/community` + `/storage`; Capacitor `10.0.2.2`). **`ClubsSection.tsx` + feed general usan `mocks/clubs-api.ts`** (en memoria, `CURRENT_PROFILE_ID = "m-1"`). |

### Archivos de la app involucrados

| Archivo | Rol | Fuente de datos hoy |
|---|---|---|
| `src/graphql/clubs.ts` | Tipos del contrato D1 (espejo ERP) | — (solo tipos, sin operaciones) |
| `src/mocks/clubs-api.ts` (1021 líneas) | API mock completa: 14 queries + 30 mutations | En memoria + latencia 300 ms |
| `src/mocks/clubs-data.ts` | Seeds mock (clubes, posts, eventos, lives) | Estático |
| `src/components/community/ClubsSection.tsx` (1332 líneas) | Lista + detalle (info/feed/eventos/lives) + chat live + QR | `mocks/clubs-api` |
| `src/pages/CommunityPage.tsx` | Tab "clubes" → `ClubsSection`; feed general inyecta posts públicos de clubes | Mock (`fetchPublicClubPosts`, `MOCK_MY_ID`) |
| `src/graphql/client.ts` | Cliente urql (auth + WS) | Real — reutilizar |
| `src/hooks/useCommunity.ts` | Hooks reales feed/posts/chat + uploads | Real — reutilizar patrón |

### Brecha

`ClubsSection` y el feed general de `CommunityPage` importan `mocks/clubs-api`
directamente. No existe capa GraphQL de clubes en la app (ni operaciones ni
mapeo wire → contrato D1).

---

## 2. Decisiones de arquitectura

### 2.1 Capa de datos: espejo del ERP, estilo `useCommunity`

- Crear `src/graphql/clubs-operations.ts` con las operaciones GraphQL
  (constantes `gql`, espejo de `clubs-service.ts` del ERP, nombres de campos
  idénticos al contrato D1).
- Crear `src/services/clubs-api.ts`: ejecución urql imperativa
  (`communityClient.executeQuery/executeMutation`) + mapeo `wire → D1`
  (`toClub`, `toClubPost`, `toClubMember`, `toClubEvent`, `toLiveSession`,
  `toClubNotification`) + `resolveStorageUrl()`.
- **Firmas idénticas a `mocks/clubs-api.ts`** (mismos nombres/parámetros/
  retornos) → `ClubsSection.tsx` y `CommunityPage.tsx` solo cambian el import.
- Exportar un `useClubs()` hook (si la UI necesita reactividad por club) o
  funciones imperativas con `reexecute` estilo `useCommunity`.

### 2.2 Identidad del usuario: perfil real del JWT, no "m-1"

- El mock usa `CURRENT_PROFILE_ID = "m-1"` (Equipo ANTARES). En backend no
  existe ese perfil: el perfil se auto-provisiona del JWT (`Me` → `Profile`).
- `ClubsSection` ya recibe `me: Profile | null` por props — usar `me.id`
  para `likedByMe`/`myAttendance`/voto y para `myMembership` (viene del wire).
- El feed general (`CommunityPage`) debe derivar `likedByMe` con `me.id`
  (ya lo hace para posts normales) en vez de `MOCK_MY_ID`.

### 2.3 URLs de media: resolver relativas contra el gateway

El backend devuelve `coverUrl`/`readUrl`/`uploadUrl` como rutas relativas
(`/storage/community/covers/...`). El ERP ya sufre este bug y lo resolvió con
`resolveStorageUrl()` (prefijar `env.apiUrl`). Replicar en la app:

- Lectura: prefijar `getGatewayBaseUrl()` en `toClub`/`toClubPost` (coverUrl,
  imageUrl, avatarUrl si vienen relativos).
- Upload (portada/logo): prefijar `uploadUrl` antes del `fetch` PUT (patrón
  `uploadProfileImage` de `useCommunity.ts` + fix del ERP).

### 2.4 Subscriptions (vida real)

Backend expone 4 topics:
- `club_{clubId}_posts` → `ClubPostAdded` (feed en vivo por club)
- `club_notif_{profileId}` → `ClubNotificationAdded` (notificaciones)
- `live_{liveId}_chat` → `LiveChatMessageAdded` (**chat del live en vivo**)
- `club_member_{profileId}` → `ClubMembershipChanged`

Hoy `ClubsSection` hace *polling* manual tras enviar mensaje (`fetchClubLiveSessions`
+ estado local). Reemplazar el chat del live por `useSubscription(LiveChatMessageAdded)`
con patrón `useConversationMessageListener` de `useCommunity.ts`. Feed y
notificaciones con las otras dos subs (opcional en Fase 1).

### 2.5 Alcance app vs ERP

La app NO consume: `ClubAnalytics`, `ClubReports`, `ClubRequests`, `ModerationLog`,
`AddClubMember`, `Approve/RejectMembership`, `Expel/Mute/Unmute`,
`ChangeMemberRole`, `CreateInvitation`, `ResolveReport`, `CreateClub`,
`UpdateClub`, `Archive/Unarchive`, `ScheduleLive` (admin) — todo eso es ERP.
El mock ya no las necesita en la app → el service real puede omitirlas.

---

## 3. Contrato GraphQL (backend → app)

### Queries (14)

| Operación | Args | Uso en app |
|---|---|---|
| `Clubs(filter, take, skip)` | `ClubFilterInput { category, search, visibility }` | Lista explorar |
| `Club(id)` | — | Ficha |
| `ClubFeed(clubId, take, skip)` | — | Feed del club (no-miembro → solo públicos, lo filtra el backend) |
| `PublicClubPosts(take)` | — | Feed general "Para ti" |
| `ClubEvents(clubId)` | — | Tab eventos |
| `ClubLiveSessions(clubId)` | — | Tab lives |
| `ClubMembers(clubId, status)` | — | Lista miembros |
| `ClubInvitation(token)` | — | QR/enlace invitación |
| `MyClubs` | — | "Mis clubes" |
| `ClubNotifications(take)` | — | Centro de notificaciones |
| (resto: Analytics/Requests/Reports — ERP) | | |

### Mutations (las que consume la app, 12)

`CreateClubMediaUploadInfo` (portada), `JoinClubDirect`, `RequestMembership`,
`JoinWithInvitation(token)`, `LeaveClub`, `CreateClubPost`, `ToggleClubPostLike`,
`AddClubComment`, `VoteClubPoll`, `ConfirmAttendance`, `JoinWaitlist`, `CheckIn`,
`SendLiveChatMessage`, `MarkNotificationRead`.

### Subscriptions (4 → usar 2 en F1, 4 en F3)

`ClubPostAdded`, `ClubNotificationAdded`, `LiveChatMessageAdded`,
`ClubMembershipChanged`.

---

## 4. Fases (tareas)

### Fase 1 — Capa de datos real (sin tocar UI)

- [ ] `src/graphql/clubs-operations.ts`: queries/mutations/subscriptions (espejo
      del ERP, campos del contrato D1).
- [ ] `src/services/clubs-api.ts`: `query`/`mutate` urql sobre `communityClient`
      (de `graphql/client.ts`) + mapeos wire→D1 + `resolveStorageUrl`.
- [ ] Mantener firmas de `mocks/clubs-api.ts`; eliminar el mock solo cuando la
      UI importe el service real.
- [ ] Verificación: `npm run build` + `npm run lint`.

### Fase 2 — ClubesSection + feed general en real

- [ ] Cambiar imports en `ClubsSection.tsx` de `mocks/clubs-api` →
      `services/clubs-api`.
- [ ] Reemplazar `MOCK_MY_ID` por `me.id` (prop) en likes/votos/asistencia.
- [ ] `CommunityPage.tsx`: `fetchPublicClubPosts` real + `likedByMe` con `me.id`.
- [ ] Probar: lista, ficha, feed (público sin membresía / completo con
      membresía), join/leave directo, solicitud a club privado, like/comentario/voto.
- [ ] Verificación: manual en `http://localhost:5173` (login OTP demo
      `123456`) + `npm run build` + `npm run lint`.

### Fase 3 — Eventos, lives y vida real (subs)

- [ ] Eventos: `ClubEvents`, `ConfirmAttendance`, `JoinWaitlist`, `CheckIn`
      (ya en service F1; conectar botones de `ClubsSection`).
- [ ] Lives: `ClubLiveSessions` + **`LiveChatMessageAdded`** en vivo (reemplaza
      el polling manual de `sendChat`).
- [ ] Subs opcionales: `ClubPostAdded` (feed club en vivo), `ClubNotificationAdded`
      + `ClubNotifications` + `MarkNotificationRead` (centro de notificaciones).
- [ ] Verificación: dos sesiones de navegador (web + Capacitor) para chat live.

### Fase 4 — Portada, QR/invitación y limpieza

- [ ] Upload portada: `CreateClubMediaUploadInfo` + PUT con Bearer +
      `resolveStorageUrl` (patrón `uploadProfileImage` de `useCommunity`).
- [ ] QR/invitación: `ClubInvitation(token)` + `JoinWithInvitation` (la UI de QR
      ya existe en `ClubsSection`).
- [ ] Borrar `mocks/clubs-api.ts` + `mocks/clubs-data.ts` (o dejarlos solo como
      seeds de preview si algo más los importa).
- [ ] Verificación: `npm run build`, `npm run lint`, `npm run i18n:check`.

---

## 5. Riesgos y gotchas

1. **URLs relativas de media** — bug conocido del ERP (PUT/lectura a
   `localhost:3000` en vez de gateway). Prefijar SIEMPRE con
   `getGatewayBaseUrl()`; no copiar el `fetch(uploadUrl)` directo.
2. **`MOCK_MY_ID` ("m-1")** — no existe en backend. Cualquier like/voto/
   membresía calculado con `m-1` queda huérfano en real. Migrar todo a `me.id`.
3. **`verbatimModuleSyntax`** (antares-paciente) — usar `import type` para tipos.
4. **Auth de la app**: `applicationCode = "app"` → `aud` del JWT. Los clubes
   son `[Authorize]` (sin permiso granular), pero el perfil se auto-provisiona
   al primer `Me` — asegurar que `me` ya esté cargado antes de usar `me.id`.
5. **Perfil con permisos ERP**: `ClubFeed` da "todo" si el claim `permission =
   Community.Manage` existe — un usuario app normal no lo tiene → ve solo
   públicos sin membresía (comportamiento correcto, verificar en pruebas).
6. **Paginación**: backend limita `take ≤ 100` (queries) y `≤ 10`
   (`PublicClubPosts`). El mock devolvía todo; no confiar en tamaños.
7. **i18n**: strings nuevos (errores de club, estados de membresía) en
   `t()` + `es.json`/`en.json`; correr `npm run i18n:check` antes de commit.
8. **No usar reacciones/reposts de posts de club en el feed general** si el
   contrato `ClubPost` (D1) no los modela — el backend los expone como `Post`
   con `ClubId`, mapear solo lo que D1 define.