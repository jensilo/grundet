-- Dev-only billing/quota smoke scenario.
--
-- Creates multiple users, each with one workspace in a different quota state.
-- Request a magic link for the email shown in each section to inspect that state.
-- Requirement/work-item limits are represented by TeamUsageSnapshots only; this avoids
-- inserting thousands of rows just to exercise the usage UI and create-only gates.

\set now_utc 'now() AT TIME ZONE ''UTC'''

-- 1. No usage snapshot yet.
INSERT INTO "Users" ("Id", "Email", "DisplayName", "CreatedAt")
VALUES ('11111111-1111-1111-1111-111111111001', 'quota-no-snapshot@grundet.test', 'Quota No Snapshot', :now_utc)
ON CONFLICT ("Email") DO UPDATE SET "DisplayName" = EXCLUDED."DisplayName";

INSERT INTO "Teams" ("Id", "Name", "Plan", "Region", "CreatedAt", "DeletedAt")
VALUES ('22222222-2222-2222-2222-222222222001', 'Quota: no snapshot', 'TeamS', 'eu', :now_utc, NULL)
ON CONFLICT ("Id") DO UPDATE SET "Name" = EXCLUDED."Name", "Plan" = EXCLUDED."Plan", "DeletedAt" = NULL;

INSERT INTO "TeamMembers" ("TeamId", "UserId", "Role")
VALUES ('22222222-2222-2222-2222-222222222001', '11111111-1111-1111-1111-111111111001', 'Lead')
ON CONFLICT ("TeamId", "UserId") DO UPDATE SET "Role" = EXCLUDED."Role";

DELETE FROM "TeamUsageSnapshots" WHERE "TeamId" = '22222222-2222-2222-2222-222222222001';

-- 2. Near requirement/work-item limit, still allowed.
INSERT INTO "Users" ("Id", "Email", "DisplayName", "CreatedAt")
VALUES ('11111111-1111-1111-1111-111111111002', 'quota-near-limit@grundet.test', 'Quota Near Limit', :now_utc)
ON CONFLICT ("Email") DO UPDATE SET "DisplayName" = EXCLUDED."DisplayName";

INSERT INTO "Teams" ("Id", "Name", "Plan", "Region", "CreatedAt", "DeletedAt")
VALUES ('22222222-2222-2222-2222-222222222002', 'Quota: near limit', 'TeamS', 'eu', :now_utc, NULL)
ON CONFLICT ("Id") DO UPDATE SET "Name" = EXCLUDED."Name", "Plan" = EXCLUDED."Plan", "DeletedAt" = NULL;

INSERT INTO "TeamMembers" ("TeamId", "UserId", "Role")
VALUES ('22222222-2222-2222-2222-222222222002', '11111111-1111-1111-1111-111111111002', 'Lead')
ON CONFLICT ("TeamId", "UserId") DO UPDATE SET "Role" = EXCLUDED."Role";

INSERT INTO "TeamUsageSnapshots" (
    "TeamId", "Plan", "ProjectCount", "MemberCount", "RequirementCount", "WorkItemCount",
    "RequirementWorkItemStatus", "OverLimitSince", "EnforcementStartsAt", "CalculatedAt")
VALUES ('22222222-2222-2222-2222-222222222002', 'TeamS', 1, 1, 7000, 1000, 'ApproachingLimit', NULL, NULL, :now_utc)
ON CONFLICT ("TeamId") DO UPDATE SET
    "Plan" = EXCLUDED."Plan",
    "ProjectCount" = EXCLUDED."ProjectCount",
    "MemberCount" = EXCLUDED."MemberCount",
    "RequirementCount" = EXCLUDED."RequirementCount",
    "WorkItemCount" = EXCLUDED."WorkItemCount",
    "RequirementWorkItemStatus" = EXCLUDED."RequirementWorkItemStatus",
    "OverLimitSince" = EXCLUDED."OverLimitSince",
    "EnforcementStartsAt" = EXCLUDED."EnforcementStartsAt",
    "CalculatedAt" = EXCLUDED."CalculatedAt";

-- 3. Over requirement/work-item limit, inside grace period, still allowed to create.
INSERT INTO "Users" ("Id", "Email", "DisplayName", "CreatedAt")
VALUES ('11111111-1111-1111-1111-111111111003', 'quota-grace@grundet.test', 'Quota Grace', :now_utc)
ON CONFLICT ("Email") DO UPDATE SET "DisplayName" = EXCLUDED."DisplayName";

INSERT INTO "Teams" ("Id", "Name", "Plan", "Region", "CreatedAt", "DeletedAt")
VALUES ('22222222-2222-2222-2222-222222222003', 'Quota: grace', 'TeamS', 'eu', :now_utc, NULL)
ON CONFLICT ("Id") DO UPDATE SET "Name" = EXCLUDED."Name", "Plan" = EXCLUDED."Plan", "DeletedAt" = NULL;

INSERT INTO "TeamMembers" ("TeamId", "UserId", "Role")
VALUES ('22222222-2222-2222-2222-222222222003', '11111111-1111-1111-1111-111111111003', 'Lead')
ON CONFLICT ("TeamId", "UserId") DO UPDATE SET "Role" = EXCLUDED."Role";

INSERT INTO "TeamUsageSnapshots" (
    "TeamId", "Plan", "ProjectCount", "MemberCount", "RequirementCount", "WorkItemCount",
    "RequirementWorkItemStatus", "OverLimitSince", "EnforcementStartsAt", "CalculatedAt")
VALUES ('22222222-2222-2222-2222-222222222003', 'TeamS', 1, 1, 9000, 1500, 'OverLimitGrace', :now_utc - INTERVAL '3 days', :now_utc + INTERVAL '11 days', :now_utc)
ON CONFLICT ("TeamId") DO UPDATE SET
    "Plan" = EXCLUDED."Plan",
    "ProjectCount" = EXCLUDED."ProjectCount",
    "MemberCount" = EXCLUDED."MemberCount",
    "RequirementCount" = EXCLUDED."RequirementCount",
    "WorkItemCount" = EXCLUDED."WorkItemCount",
    "RequirementWorkItemStatus" = EXCLUDED."RequirementWorkItemStatus",
    "OverLimitSince" = EXCLUDED."OverLimitSince",
    "EnforcementStartsAt" = EXCLUDED."EnforcementStartsAt",
    "CalculatedAt" = EXCLUDED."CalculatedAt";

-- 4. Enforcement active; requirement/work-item creation should be blocked.
INSERT INTO "Users" ("Id", "Email", "DisplayName", "CreatedAt")
VALUES ('11111111-1111-1111-1111-111111111004', 'quota-enforced@grundet.test', 'Quota Enforced', :now_utc)
ON CONFLICT ("Email") DO UPDATE SET "DisplayName" = EXCLUDED."DisplayName";

INSERT INTO "Teams" ("Id", "Name", "Plan", "Region", "CreatedAt", "DeletedAt")
VALUES ('22222222-2222-2222-2222-222222222004', 'Quota: enforced', 'TeamS', 'eu', :now_utc, NULL)
ON CONFLICT ("Id") DO UPDATE SET "Name" = EXCLUDED."Name", "Plan" = EXCLUDED."Plan", "DeletedAt" = NULL;

INSERT INTO "TeamMembers" ("TeamId", "UserId", "Role")
VALUES ('22222222-2222-2222-2222-222222222004', '11111111-1111-1111-1111-111111111004', 'Lead')
ON CONFLICT ("TeamId", "UserId") DO UPDATE SET "Role" = EXCLUDED."Role";

INSERT INTO "TeamUsageSnapshots" (
    "TeamId", "Plan", "ProjectCount", "MemberCount", "RequirementCount", "WorkItemCount",
    "RequirementWorkItemStatus", "OverLimitSince", "EnforcementStartsAt", "CalculatedAt")
VALUES ('22222222-2222-2222-2222-222222222004', 'TeamS', 1, 1, 9000, 1500, 'EnforcementActive', :now_utc - INTERVAL '20 days', :now_utc - INTERVAL '6 days', :now_utc)
ON CONFLICT ("TeamId") DO UPDATE SET
    "Plan" = EXCLUDED."Plan",
    "ProjectCount" = EXCLUDED."ProjectCount",
    "MemberCount" = EXCLUDED."MemberCount",
    "RequirementCount" = EXCLUDED."RequirementCount",
    "WorkItemCount" = EXCLUDED."WorkItemCount",
    "RequirementWorkItemStatus" = EXCLUDED."RequirementWorkItemStatus",
    "OverLimitSince" = EXCLUDED."OverLimitSince",
    "EnforcementStartsAt" = EXCLUDED."EnforcementStartsAt",
    "CalculatedAt" = EXCLUDED."CalculatedAt";

-- 5. Project hard limit reached. This uses real projects because project limits are synchronous gates.
INSERT INTO "Users" ("Id", "Email", "DisplayName", "CreatedAt")
VALUES ('11111111-1111-1111-1111-111111111005', 'quota-projects-full@grundet.test', 'Quota Projects Full', :now_utc)
ON CONFLICT ("Email") DO UPDATE SET "DisplayName" = EXCLUDED."DisplayName";

INSERT INTO "Teams" ("Id", "Name", "Plan", "Region", "CreatedAt", "DeletedAt")
VALUES ('22222222-2222-2222-2222-222222222005', 'Quota: projects full', 'TeamS', 'eu', :now_utc, NULL)
ON CONFLICT ("Id") DO UPDATE SET "Name" = EXCLUDED."Name", "Plan" = EXCLUDED."Plan", "DeletedAt" = NULL;

INSERT INTO "TeamMembers" ("TeamId", "UserId", "Role")
VALUES ('22222222-2222-2222-2222-222222222005', '11111111-1111-1111-1111-111111111005', 'Lead')
ON CONFLICT ("TeamId", "UserId") DO UPDATE SET "Role" = EXCLUDED."Role";

INSERT INTO "Projects" ("Id", "TeamId", "Name", "Description", "CreatedAt", "DeletedAt")
SELECT
    ('33333333-3333-3333-3333-33333333300' || project_number)::uuid,
    '22222222-2222-2222-2222-222222222005'::uuid,
    'Seeded project ' || project_number,
    'Created by billing usage-states scenario.',
    :now_utc,
    NULL
FROM generate_series(1, 6) AS project_number
ON CONFLICT ("Id") DO UPDATE SET "DeletedAt" = NULL;

INSERT INTO "TeamUsageSnapshots" (
    "TeamId", "Plan", "ProjectCount", "MemberCount", "RequirementCount", "WorkItemCount",
    "RequirementWorkItemStatus", "OverLimitSince", "EnforcementStartsAt", "CalculatedAt")
VALUES ('22222222-2222-2222-2222-222222222005', 'TeamS', 6, 1, 0, 0, 'WithinLimit', NULL, NULL, :now_utc)
ON CONFLICT ("TeamId") DO UPDATE SET
    "Plan" = EXCLUDED."Plan",
    "ProjectCount" = EXCLUDED."ProjectCount",
    "MemberCount" = EXCLUDED."MemberCount",
    "RequirementCount" = EXCLUDED."RequirementCount",
    "WorkItemCount" = EXCLUDED."WorkItemCount",
    "RequirementWorkItemStatus" = EXCLUDED."RequirementWorkItemStatus",
    "OverLimitSince" = EXCLUDED."OverLimitSince",
    "EnforcementStartsAt" = EXCLUDED."EnforcementStartsAt",
    "CalculatedAt" = EXCLUDED."CalculatedAt";
