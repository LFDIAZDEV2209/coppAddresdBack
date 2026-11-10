# Versioned consent receipts

Patient AI requests require a server receipt for the current published legal
version and the configured processing disclosure. No approved legal text or
active providers are assumed. The default configuration makes those features
unavailable. This change does not publish a policy or make a compliance claim.

The existing `erp.legal_documents` and `erp.legal_document_versions` remain the
content store. `GET /api/v1/legal-documents/public/{code}` now also returns
`versionId` and `publishedAt` from the actual published snapshot; its existing
properties remain compatible. Saving a draft can still change the document
title/updatedAt; put the approved heading inside the versioned text if relevant.

Migration `20261110174324_AddLegalConsentReceipts` adds
`app.legal_document_acceptances`, its version FK, per-user idempotency index and
existing PostgreSQL audit trigger. A canonical scope/provider/revision snapshot
and its SHA-256 are recorded; document text is not duplicated. Withdrawal is
audited and historical receipts remain. Published legal snapshots also become
immutable at the database level. Draft/publish continues to create new snapshots.

## App API

All three endpoints require authentication and derive the user ID from the JWT.
They do not need an existing patient profile. Responses are not cacheable.

* `GET /api/v1/me/legal-consents?locale=es` (`es` or `en`) returns
  `{locale,scopes:[{scope,available,accepted,disclosureId,providers,documents,
  receiptId,acceptedAt}]}`. `receiptId`/`acceptedAt` identify the current acceptance,
  or otherwise the latest own nonwithdrawn scope receipt across locales. The
  latter remains withdrawable after a document/provider/configuration change or
  while the scope is unavailable; it never sets `accepted:true` for stale terms.
  Each available scope has one document with
  `{code,versionId,version,title,content,locale,publishedAt}`. Missing configuration,
  approval, translation or publication produces `available:false` for that scope.
* `POST /api/v1/me/legal-consents/acceptances` takes
  `{scope,versionId,locale,disclosureId,idempotencyKey}` and returns
  `{id,scope,versionId,locale,disclosureId,acceptedAt,withdrawnAt}`. Generate a UUID
  request key for each deliberate acceptance and reuse it only to retry that
  request. Reusing a key with a different request is rejected. The server validates
  publication, exact current version and disclosure, then records its own time.
* `DELETE /api/v1/me/legal-consents/acceptances/{receiptId}` returns 204 and
  withdraws all currently active receipts for that user's scope, including other
  locale receipts. Foreign receipt IDs return 404. A repeated withdrawal is
  idempotent. Reacceptance requires a fresh request key and creates a new row.

Re-read status after acceptance. An idempotent retry returns the historical
receipt, which may have been withdrawn or superseded. `accepted` is a statement
about current server requirements, not the mere existence of a receipt.

Receipt/audit retention after account deletion needs the company's approved
policy. This migration does not add a blanket purge or claim that all legal
evidence disappears when an auth account is deleted.

Error responses expose `{code,scope}`: 403 `CONSENT_REQUIRED`; 503
`CONSENT_DOCUMENT_UNAVAILABLE` or `CONSENT_SUBJECT_REQUIRED`; 409
`DOCUMENT_VERSION_CHANGED` or `CONSENT_REQUEST_CONFLICT`; 400 invalid locale or
request. Refresh requirements and display a new unchecked choice after 409.
Refusal, network failure and missing documents must not start another provider,
speech recognition, upload or chat retry. A successful DELETE does not erase
already processed provider data.

## Configuration and later publication

`LegalConsents:Scopes` supports `program`, `chat`, `voice`, `food`, `lab`,
`patient-ai` and `sos`. Each entry has this shape (placeholders, not approved data):

```json
{
  "Approved": false,
  "ProcessingRevision": "",
  "Providers": [],
  "Documents": { "es": "", "en": "" }
}
```

The company must approve text and actual named recipients, including fallback
models, embeddings, voice tools and relevant subprocessors. AI scopes require
nonempty provider names; program/SOS may have no AI recipients. Populate the
existing CMS via its permission-protected draft/publish workflow and map each
locale to its existing document code. No legal text is seeded here. There is no
automatic machine translation or cross-locale fallback for legal documents.

`ProcessingRevision` describes the approved deployed processing configuration.
The canonical provider list, scope and revision determine `disclosureId`; changing
them invalidates old receipts even if the document version stays the same.
Publishing a new current version also requires reacceptance. Configuration reload
or normal server rollout and CMS publication can update requirements after the
initial consent-aware binary ships. Environment variable changes require process
restart. Old binaries need upgrading for consent UI and authenticated FoodAI.

The backend cannot independently discover the remote AI service or ElevenLabs
dashboard's actual model/subprocessors. Deploy processor configuration and its
approved disclosure together; do not enable a processor that the document and
manifest do not cover. Local source supports Anthropic/OpenAI chat/embeddings and
ElevenLabs voice, but that does not prove which production paths are enabled.
The deployed food provider chain must be supplied and verified separately.

## Enforcement and compatibility

Resource filters run before upload model binding, storage, signed-URL issuance
and SSE headers on chat (sync, stream, feedback), voice, FoodAI and laboratory
uploads. FoodAI analysis now requires a Bearer token; its health endpoint remains
public. The mobile multipart upload must supply/refresh authentication.

Outgoing typed HTTP clients independently recheck consent before each AI send.
Server-only request metadata maps the subject and scope; it is not serialized:

| Path | Required scope | Subject |
| --- | --- | --- |
| Text chat, stream, feedback | chat | JWT auth user |
| Voice session creation | voice | JWT auth user |
| Food image analysis | food | JWT auth user |
| Lab extraction/narration | lab | resolved patient profile owner |
| Assessment profile interpretation | patient-ai | patient profile owner |
| Nutrition plan generation (including worker) | patient-ai | patient profile owner |
| Nutrition draft generation (including worker) | patient-ai | patient profile owner |
| Program assignment narrative worker | patient-ai | patient profile owner |
| App-triggered plan/profile content translation | patient-ai | JWT auth user |
| Food result/history name translation | food | JWT auth user |
| Background/custom push translation | patient-ai | actual English recipient `DeviceToken.UserId` |

Translation recipient metadata is server-only and is never serialized into the
provider payload. A custom push batch must have exactly one nonempty English
recipient identity; multi-user/unknown-subject batches fail closed. Each outgoing
translation batch rechecks that recipient's current consent, including when an
ERP operator triggered the push. Reviewed/static or supplied English copy needs
no model and remains deliverable without invoking translation.

Unmapped background translation is still blocked with `CONSENT_SUBJECT_REQUIRED`;
new jobs must explicitly carry their own recipient rather than borrowing an
ambient operator or another patient's acceptance. Generic health-template
translation happens before patient identity interpolation in its existing ERP
workflow. It has no implicit background exemption.

Without approved/current consent, basic EN read projections retain the original
clinical DTO and declare its authored language (`Content-Language: es`) instead
of sending it to an AI translator or failing the whole read. This can show authored
Spanish content until the approved consent is available and accepted. Stored
reviewed English remains usable without a provider call. Food history similarly
keeps original names and any reviewed/catalog English, leaving missing `nameEn`
unset. Only typed missing-consent/document/subject errors receive that fallback;
other translation/provider failures and cancellation retain their existing errors.
Blocked generation jobs may enter their existing retry/failure queues;
inspect/requeue them after approval and acceptance rather than claim they have
generated content.

Existing internal history/health GETs and proactive message injection do not
invoke an external model and remain available. Operator requests authenticated
with `aud=erp` and without `aud=app` deliberately retain their existing authorization/processing flows;
this is a patient-app consent gate, not a new ERP clinical legal-basis policy.
Background work has no ERP request identity and cannot use that exception.

This protects this backend's known outbound clients, not a separately reachable
AI service. Keep the AI service's internal key/network restrictions in place.
Existing external voice sessions/signed URLs cannot be retrospectively revoked
by this receipt table: the app must close an active session on withdrawal; verify
provider-side revocation support before promising immediate remote termination.

## Coordinated rollout

Apply the main API migration through the normal one-off migration job before
starting a consent-aware API. Ship the matching mobile UI/authenticated FoodAI
client together with the enforcement change; older binaries do not have the
required receipt screens and cannot create current receipts. Do not silently
turn approval on to keep an old binary working. Default unavailable scopes are
intentional while the official document and processor disclosures are pending.

After company approval, publish the correct locale snapshots through the legal
CMS, map their codes and verified processor/revision settings, then check fresh
acceptance, cancellation, version change and withdrawal in the release build.
Existing receipt history is not a substitute for a current match. Previously
queued generation work may need explicit retry after its recipient accepts.
The app must stop its own active voice session on withdrawal; this backend does
not claim to revoke an already issued remote voice session retroactively.

Food analysis reads/feedback now require the exact authenticated owner. Legacy
anonymous analyses return the same 404 as foreign records; this closes their
previous IDOR exposure without publishing, modifying or deleting those rows.

## Local validation

Unit tests use synthetic InMemory legal documents and a counting HTTP transport.
`LegalConsentPersistenceTests` requires `COP_TEST_DB_CONNECTION`, rejects nonlocal
hosts, creates a unique `copp_consent_test_*` database, applies only the exact legal/
audit migration dependencies and drops it afterward. It uses a few synthetic rows
to check concurrent idempotency, audit actor, withdrawal, immutability and migration
up/down/up. No provider requests or patient records are needed.

```sh
dotnet test tests/CoppAddresd.UnitTests --filter FullyQualifiedName~LegalConsentTests
# Use the isolated local PostgreSQL test wrapper for:
python3 /tmp/diomedes-run-local-backend-tests.py tests/CoppAddresd.IntegrationTests/CoppAddresd.IntegrationTests.csproj --filter FullyQualifiedName~LegalConsentPersistenceTests
```

That session wrapper reads only the documented local DB connection in memory,
rejects non-loopback/ambiguous host settings, and passes it only to the test
subprocess. It prints no connection values. The fixture adds the independent
loopback and unique-database checks described above.
