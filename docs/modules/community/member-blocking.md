# Member blocking for the patient app

Authenticated app members can call `blockUser(profileId: UUID!)` and
`unblockUser(profileId: UUID!)` (Boolean result), and query
`blockedProfiles(take: Int = 50, skip: Int = 0)` for
`{profileId, displayName, avatarUrl, createdAt}`. Page size is 1–100. Actor identity
comes only from the JWT; self/system/unknown targets are rejected. No caller-
supplied actor ID is accepted. The app lists/manages its own choices only.

A directed choice hides either member from the other. Unblocking removes only
the caller's choice, does not cancel a reverse block, and never restores follows.
Blocking atomically removes both follow directions. Existing posts, messages and
reports remain as evidence. Reporting an already-seen post/comment remains
possible after blocking without returning hidden content.

App-scoped EF filters cover profiles, posts, nested comments/replies/reposts,
likes/votes, people/follows, shared group members/messages, club members/live
chat and feed events. DM/group summary raw SQL filters before pagination. A
bounded recursive SQL function validates every comment ancestor and fails closed
for malformed or cyclic legacy threads. Per-event fresh database scopes sanitize
queued subscription payloads; blocked messages/events are skipped. Direct/group
participants, club privacy and live membership are rechecked per event.

An ERP JWT (`aud=erp` without `aud=app`) retains complete moderation evidence;
app moderator permissions alone do not bypass blocking. System profiles cannot
be blocked. The standalone service initializes the request viewer after JWT
authentication. Background tasks with no viewer retain their operational scope.

Deploy the Community migration before an app uses these operations. Startup
currently migrates this service automatically; review the generated SQL and
migration history before rollout. Downgrade removes only the new choices and
visibility function, leaving message/moderation history intact. The mobile app
adds member-profile confirmation, a paginated management panel and network-only
refresh of affected views; active peer views close after blocking to clear stale
content. This is not a complete provider moderation or staffed response audit.

Validation is synthetic, local PostgreSQL and schema/unit/UI checks. No real
member was blocked, followed, messaged or reported, and no load test was run.
