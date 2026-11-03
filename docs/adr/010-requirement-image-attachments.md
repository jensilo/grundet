# 010 — Requirement image attachments: upload-then-reference, append-only blobs

**Date:** 2026-06-25
**Status:** Superseded by [ADR-011](011-unified-project-attachment-storage.md) (storage unified to a
project-scoped `Attachment` table shared by requirements and work items; content-hash dedup, thumbnail
variant, and changelog diff added). The upload-then-reference model and append-only/version-copy
invariants below remain in force.

## Context

Requirements need to carry one or more images so a picture can stand in as (or alongside) the
spec/chart it represents. The constraints that shape this are the project's non-negotiables:

- **Versioning is sacred.** Every requirement mutation inserts a new immutable version row. An image
  referenced by any version must never be silently lost when text is later edited, and superseded
  versions must keep rendering the images they referenced.
- **Tenant isolation.** Image bytes are tenant data and must only be served to users authorized for
  the owning project. Attachment ids supplied at create/edit are a cross-tenant attack surface.
- **API contract chain.** Typed DTOs → OpenAPI → generated client → frontend. No raw `fetch`, no
  hand-patched client.

An existing local-disk image stack (`IImageAttachmentStorage` / `SkiaImageAttachmentStorage`, used
by Feedback) re-encodes uploads to JPEG. There was no existing image-serving endpoint in the repo.

## Decision

**Upload-then-reference.** A dedicated multipart endpoint
(`POST /projects/{projectId}/requirements/attachments`) stores images via `IImageAttachmentStorage`
under **owner = `projectId`** (the requirement may not exist yet at upload time) and persists
immutable `RequirementAttachment` blob-metadata rows that carry `ProjectId`. Create/edit stay typed
JSON and gain an ordered `Guid[] AttachmentIds`; the service validates each id exists and belongs to
the requirement's project (an authorized filter surface) before writing it into the new version row.

`Requirement` gains `Guid[] AttachmentIds`, copied verbatim into every version row alongside
`Labels`/`AcceptanceCriteria` — including the three cascade-delete tombstone paths
(`ProjectStore`, `TeamStore`, `FeatureStore`) and the `CurrentRequirements` view (which those
cascades read). Blobs are **append-only**: never updated or deleted by the API. Removing an image
from a requirement just omits its id from the next version; the row and file persist so older
versions still render.

**Serving via blob-via-SDK.** `GET /projects/{projectId}/requirements/attachments/{attachmentId}/content`
authorizes the project, re-derives the absolute path from the configured root + stored relative path
(never a client path; traversal-guarded), and streams the bytes with a long immutable private cache.
The frontend fetches bytes through the generated client (`parseAs: "blob"`) into an object URL,
which keeps the contract chain intact and sidesteps the cross-origin cookie problem a plain
`<img src="{apiOrigin}/…">` would hit.

**Re-encode caps raised additively.** `ImageAttachmentOptions` gained `MaxDimension`/`JpegQuality`
(defaults 1280/82, so Feedback is unchanged); requirement attachments use 2560/85 so a detailed
chart stays legible.

## Alternatives Considered

- **Single multipart create/edit (no second endpoint, no orphans).** Rejected: form-binding the
  PATCH-style `UpdateRequirementCommand` (its `…WasProvided` flags, enums, string arrays) into
  multipart is awkward, and it couples image storage to every text edit. Upload-then-reference gives
  cleaner edit UX (add/remove an image without re-sending all fields) at the cost of orphan uploads.
- **`<img>` with a hardened cross-origin cookie / short-lived signed URLs.** Rejected for v1: a
  hand-built API URL in `<img>` is a generated-client bypass, and `SameSite=None` widens the cookie
  surface. Blob-via-SDK is simpler and contract-clean.
- **FK from attachment to a requirement version.** Rejected: a blob is shared across versions; the
  `AttachmentIds[]` array on the version row is the link. The attachment row carries `ProjectId` so
  reference- and serve-time auth need no join.
- **Preserving the original (PNG) losslessly.** Deferred: JPEG re-encoding still degrades text-heavy
  charts regardless of dimension. Lossless preservation is a larger, deliberate change; called out,
  not silently shipped.

## Consequences

- **Orphaned uploads are accepted (v1, no GC).** An image can be uploaded and never referenced (the
  user abandons the form). An orphan is a `RequirementAttachment.Id` present in zero version rows'
  `AttachmentIds`; the row carries `ProjectId` + `CreatedAt` for a future sweep. The sweep is not
  built.
- **Disk growth is effectively permanent.** Project deletion is soft and there is no hard-delete of
  requirements/versions, so `DeleteOwnerAttachmentsAsync` would essentially never run. Append-only
  forever is the steady state, accepted until disk pressure is real.
- **RequirementAttachments carries its own RLS** (project-scoped insert/select policies mirroring
  `Requirements`) as a DB-level backstop to the service-layer authorization.
- **Cascade copy is a sacred-versioning trap.** `AttachmentIds = []` is a NOT NULL `uuid[]`; omitting
  it at a cascade tombstone site fails *silently* (writes empty array), so integration tests cover
  project- and feature-delete cascades preserving `AttachmentIds`, not just single-requirement delete.
