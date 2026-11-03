INSERT INTO "AlphaWaitlistEntries" (
    "Id",
    "EmailOriginal",
    "EmailNormalized",
    "Name",
    "AccountType",
    "Status",
    "CreatedAt",
    "UpdatedAt",
    "ApprovedAt",
    "ApprovedBy")
SELECT
    gen_random_uuid(),
    seed."Email",
    lower(seed."Email"),
    seed."Name",
    'Personal',
    'Approved',
    now() AT TIME ZONE 'UTC',
    now() AT TIME ZONE 'UTC',
    now() AT TIME ZONE 'UTC',
    'local-dev-seed'
FROM (VALUES
    (:'dev_email', :'dev_name'),
    (:'dev_second_email', :'dev_second_name')
) AS seed("Email", "Name")
ON CONFLICT ("EmailNormalized") DO UPDATE SET
    "Status" = 'Approved',
    "UpdatedAt" = now() AT TIME ZONE 'UTC',
    "ApprovedAt" = COALESCE("AlphaWaitlistEntries"."ApprovedAt", now() AT TIME ZONE 'UTC'),
    "ApprovedBy" = COALESCE("AlphaWaitlistEntries"."ApprovedBy", 'local-dev-seed');
