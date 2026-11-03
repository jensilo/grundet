# 011 — Unified project-scoped attachment storage with content-hash dedup

**Date:** 2026-06-27
**Status:** Accepted
**Supersedes:** [ADR-010](010-requirement-image-attachments.md)

## Context

ADR-010 shipped image attachments for requirements: an `IImageAttachmentStorage` blob on disk plus a
`RequirementAttachment` metadata row carrying `ProjectId`, referenced by `Requirement.AttachmentIds[]`
copied into every version row. Two follow-on needs reshaped that design:

1. **Work items need the same capability** (bug evidence, screenshots) with a gallery — ideally
   sharing an image with the requirement it implements.
2. Storage was already *project-scoped, not requirement-scoped*: `RequirementAttachment` had no FK to
   a requirement, and upload (`POST /projects/{projectId}/requirements/attachments`) was never tied
   to one. The "requirement" in the name was incidental.

A parallel `WorkItemAttachment` table would duplicate the entity, store, service, endpoints, and RLS
for a third time (Feedback already has its own), and would make content dedup per-table — so the same
image referenced by an intent and its work item would be stored twice.

## Decision

**One project-scoped `Attachment` table**, shared by all owners. `RequirementAttachment` is renamed to
`Attachment` and moved to `Features/Attachments/` (`AttachmentService`, `IAttachmentStore`,
`AttachmentStore`, `AttachmentOptions`, `AttachmentEndpoints`). Upload and serving become
owner-agnostic:

- `POST /projects/{projectId}/attachments` — multipart upload, owner = project.
- `GET /projects/{projectId}/attachments/{attachmentId}/content` — stream bytes.

Both `Requirement.AttachmentIds[]` and `WorkItem.AttachmentIds[]` reference rows in this one table and
are copied verbatim into every version row (the version history is the changelog of attached images),
including cascade-delete tombstone paths and the `CurrentRequirements` / `CurrentWorkItems` views.

**Content-hash dedup, project-scoped.** `Attachment` gains `Sha256` (hex of the *original upload
bytes*) with a unique `(ProjectId, Sha256)` index. On upload the server computes the hash and, if a
row with that hash already exists in the project (or appears earlier in the same batch), reuses the
existing blob and discards the redundant file. Identical bytes therefore cost one row + one file per
project.

**Single migration, no backfill.** All schema (table rename, `Sha256` + unique index,
`WorkItems.AttachmentIds`, both view updates) lands by editing the existing attachments migration; dev
and other databases are recreated, so there is no migration-history or data-backfill concern.
`Sha256` is `NOT NULL` from the start.

## Alternatives Considered

- **Parallel `WorkItemAttachment` table.** Rejected: triplicates an owner-agnostic concern and makes
  dedup per-table, so a shared image is stored twice. The existing table already had no requirement
  FK, so unification is a rename, not a redesign.
- **Global (cross-project) dedup.** Rejected as a tenant-isolation hazard: a shared blob across
  tenants is a data leak, and a global hash lookup is an existence oracle ("does any tenant already
  have this file?"). Dedup is keyed on `(ProjectId, Sha256)` — never global.
- **Trusting a client-supplied hash to skip upload.** Rejected: it would let a caller obtain a
  reference to a blob it never uploaded (access-control bypass). The hash is always computed
  server-side from the received bytes.
- **Hashing the re-encoded JPEG instead of the original bytes.** Rejected: JPEG re-encoding is not
  guaranteed byte-stable across Skia versions, so the same source file could fail to dedup. Hashing
  the original upload is the stable "same file" signal and lets a hit skip re-encoding entirely.

## Consequences

- **Dedup reuse must ship with the unique index.** Because the index is created immediately, a
  duplicate upload would otherwise hit a constraint violation; the upload path is dedup-aware from the
  same change (handles both already-stored and intra-batch duplicates).
- **Reused blobs keep the first uploader's `OriginalFileName`.** Two identical uploads with different
  filenames collapse to one row; the second reference shows the first name. Cosmetic, accepted.
- **Append-only and orphan behaviour from ADR-010 are unchanged.** No GC; blobs persist so superseded
  versions still render. The dedup file-discard on a hit is the only deletion, and only of a
  just-written redundant copy.
- **`Attachment` carries its own RLS** (project-scoped insert/select policies) as a DB backstop to
  service-layer authorization, mirroring the prior `RequirementAttachments` policies.
- **Deploy note:** the config section renamed `RequirementAttachments` → `Attachments`
  (`Attachments__AttachmentsRoot` env var).
