-- Dev-only scenario for visually checking changelog author attribution.
--
-- Run AFTER the school-board demo (it reuses that project/team):
--   make db-scenarios SCENARIOS="demo/school-board demo/changelog-attribution"
--
-- Adds a handful of *recent* version rows so they sort to the top of the project
-- changelog, each authored by a different kind of user:
--   - Former member  -> exists in "Users" but has no "TeamMembers" row for the
--                       team. Resolves to a name + "former member" marker.
--   - Unknown member -> a ChangedByUserId with no matching "Users" row at all.
--                       Renders as "Unknown member" with no marker. This state is
--                       structurally impossible in app code (the ChangedByUserId
--                       FK is DeleteBehavior.Restrict), so it is forced here only
--                       by temporarily disabling triggers. Dev fixtures only.
--   - Active member  -> normal current member, for contrast (no marker).
--
-- Assumes the school-board project (30000000-…001) and team (20000000-…001) and
-- requirements IN-1..IN-4 / work item WO-66 already exist.

\set now_utc 'now() AT TIME ZONE ''UTC'''
\set project_id '30000000-0000-0000-0000-000000000001'
\set team_id '20000000-0000-0000-0000-000000000001'
\set unknown_user '99999999-9999-9999-9999-999999999999'

BEGIN;

-- Note: psql does not interpolate :vars inside dollar-quoted blocks, so the
-- project id is written as a literal here.
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM "Projects" WHERE "Id" = '30000000-0000-0000-0000-000000000001'
    ) THEN
        RAISE EXCEPTION
            'changelog-attribution requires the demo/school-board scenario to be applied first';
    END IF;
END $$;

-- Former member: a real user account that is deliberately NOT a team member.
-- Insert with a generated id so we never collide with an unrelated account that may
-- already hold a fixed UUID, then resolve Robin's *actual* id by email and use it for
-- membership cleanup and every seeded version row below.
INSERT INTO "Users" ("Id", "Email", "DisplayName", "CreatedAt")
VALUES (gen_random_uuid(), 'robin@grundet.test', 'Robin Lane', :now_utc - INTERVAL '60 days')
ON CONFLICT ("Email") DO UPDATE SET
    "DisplayName" = COALESCE("Users"."DisplayName", EXCLUDED."DisplayName");

SELECT "Id" AS former_user FROM "Users" WHERE "Email" = 'robin@grundet.test' \gset

-- Make sure Robin is not a current member of the project's team (in case a prior
-- run or other scenario added them).
DELETE FROM "TeamMembers"
WHERE "TeamId" = :'team_id' AND "UserId" = :'former_user';

-- Disable append-only triggers (version tables are append-only in app code) AND
-- FK enforcement so the unknown-member row can reference a non-existent user.
-- DISABLE TRIGGER ALL covers both; dev-only.
ALTER TABLE "Requirements" DISABLE TRIGGER ALL;
ALTER TABLE "WorkItems" DISABLE TRIGGER ALL;

-- Idempotency: drop any rows this scenario previously inserted.
DELETE FROM "Requirements"
WHERE "Id" IN (
    '53000000-0000-0000-0000-000000000001',  -- former member, IN-1
    '54000000-0000-0000-0000-000000000003',  -- unknown member, IN-3
    '55000000-0000-0000-0000-000000000002'); -- active member, IN-2
DELETE FROM "WorkItems"
WHERE "Id" = '63000000-0000-0000-0000-000000000001';  -- former member, WO-66

-- Helper: copy the latest existing version of a requirement, overriding only the
-- identity/attribution columns. Copying every field keeps the new version row
-- valid (a partial initializer would violate NOT NULL / domain constraints).
WITH latest AS (
    SELECT DISTINCT ON ("RequirementId") *
    FROM "Requirements"
    WHERE "ProjectId" = :'project_id' AND "SequenceNumber" IN (1, 2, 3)
    ORDER BY "RequirementId", "ChangedAt" DESC, "Id" DESC
)
INSERT INTO "Requirements" (
    "Id", "RequirementId", "ProjectId", "FeatureId", "SequenceNumber", "Text", "Status",
    "Priority", "Labels", "AcceptanceCriteria", "Reason", "ChangedAt", "ChangedByUserId", "ChangeType")
SELECT
    seed."NewId",
    l."RequirementId",
    l."ProjectId",
    l."FeatureId",
    l."SequenceNumber",
    l."Text",
    l."Status",
    l."Priority",
    l."Labels",
    l."AcceptanceCriteria",
    seed."Reason",
    seed."ChangedAt",
    seed."ChangedByUserId",
    'Updated'
FROM (VALUES
    -- (SequenceNumber, NewId, ChangedByUserId, ChangedAt, Reason)
    (1, '53000000-0000-0000-0000-000000000001'::uuid, :'former_user'::uuid,
        :now_utc - INTERVAL '1 hour', 'Clarified wording before handing the project over.'),
    (3, '54000000-0000-0000-0000-000000000003'::uuid, :'unknown_user'::uuid,
        :now_utc - INTERVAL '20 minutes', 'Edited by an account that has since been deleted.'),
    (2, '55000000-0000-0000-0000-000000000002'::uuid,
        (SELECT "Id" FROM "Users" WHERE "Email" = 'sam@grundet.test'),
        :now_utc - INTERVAL '2 hours', 'Routine update from a current member.')
) AS seed("SequenceNumber", "NewId", "ChangedByUserId", "ChangedAt", "Reason")
JOIN latest l ON l."SequenceNumber" = seed."SequenceNumber";

-- Former member also touched a work item recently.
WITH latest AS (
    SELECT DISTINCT ON ("WorkItemId") *
    FROM "WorkItems"
    WHERE "ProjectId" = :'project_id' AND "SequenceNumber" = 66
    ORDER BY "WorkItemId", "ChangedAt" DESC, "Id" DESC
)
INSERT INTO "WorkItems" (
    "Id", "WorkItemId", "ProjectId", "SequenceNumber", "Title", "Description", "Status", "Priority",
    "Type", "AssignedToUserId", "BlockedReason", "Labels", "Reason", "ChangedAt", "ChangedByUserId", "ChangeType")
SELECT
    '63000000-0000-0000-0000-000000000001',
    l."WorkItemId",
    l."ProjectId",
    l."SequenceNumber",
    l."Title",
    l."Description",
    l."Status",
    l."Priority",
    l."Type",
    l."AssignedToUserId",
    l."BlockedReason",
    l."Labels",
    'Final tidy-up before leaving the team.',
    :now_utc - INTERVAL '3 hours',
    :'former_user',
    'Updated'
FROM latest l;

ALTER TABLE "Requirements" ENABLE TRIGGER ALL;
ALTER TABLE "WorkItems" ENABLE TRIGGER ALL;

COMMIT;

\echo 'Seeded changelog attribution scenario: former member (Robin Lane), unknown/deleted member, and an active member, all with recent changes at the top of the timeline.'
