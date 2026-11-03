# Backend Configuration Contract

Scope: `src/Grundet.Api`.

This is the authoritative runtime config contract for backend startup and core features.

## Local Development

Use `src/Grundet.Api/appsettings.Development.json` for local values.

`appsettings.Local.json` is supported as an optional gitignored override file in Development.

Precedence in Development is:

1. `appsettings.json`
2. `appsettings.Development.json`
3. `appsettings.Local.json`
4. Environment variables
5. Command-line arguments

## Production Configuration

Set production values via environment variables.

Use ASP.NET Core key mapping: `Section:Key` => `Section__Key`.

Examples: `ConnectionStrings:Default` => `ConnectionStrings__Default`, `ConnectionStrings:Migration` => `ConnectionStrings__Migration`.

## Required Keys

These must be set for a healthy production deployment:

- `ConnectionStrings__Default`
- `Email__SmtpHost`
- `Email__FromAddress`
- `MagicLink__PublicApiBaseUrl`
- `MagicLink__PublicAppBaseUrl`
- `Jira__ClientId`
- `Jira__ClientSecret`
- `Jira__RedirectUri`
- `Feedback__AttachmentsRoot` when feedback screenshot uploads are enabled. Mount this as persistent server-side storage outside the web root, for example `/var/lib/grundet/feedback-attachments` inside the API container.

## Optional Keys (with defaults)

- `ConnectionStrings__Migration` (used by migration jobs/`dotnet ef`; falls back to `ConnectionStrings__Default` when unset)
- `Auth__CookieDomain` (default: empty)
- `Auth__CookieExpireDays` (default: `30`)
- `Auth__AbsoluteSessionDays` (default: `90`)
- `Email__SmtpPort` (default: `587`)
- `Email__FromName` (default: `Grundet`)
- `Email__SmtpUsername` (default: empty)
- `Email__SmtpPassword` (default: empty)
- `Email__SmtpUseSsl` (default: `false`)
- `MagicLink__ExpiryMinutes` (default: `30`)
- `RateLimit__MagicLink__PermitLimit` (default: `5`)
- `RateLimit__MagicLink__WindowMinutes` (default: `10`)
- `RateLimit__VerifyMagicLink__PermitLimit` (default: `20`)
- `RateLimit__VerifyMagicLink__WindowSeconds` (default: `60`)
- `Invites__ExpiryDays` (default: `14`)
- `Jira__IssueType` (default: `Task`)
- `Feedback__WeeklyLimit` (default: `20` submissions per user per rolling 7 days)
- `Feedback__MaxFiles` (default: `5`; screenshots are optional per submission, but V1 supports upload)
- `Feedback__MaxOriginalBytes` (default: `2097152`, 2 MB per uploaded screenshot)
- `Feedback__MaxImagePixels` (default: `8000000`)
- `BillingUsageRecalculation__Enabled` (default: `true`; local Development config sets `false`)
- `BillingUsageRecalculation__StartupDelaySeconds` (default: `300`)
- `BillingUsageRecalculation__DailyRunTimeUtc` (default: `03:30`)
- `BillingUsageRecalculation__StaleAfterHours` (default: `20`)

## Data Protection Keys

- `GRUNDET_DATA_PROTECTION_KEYS_PATH` is optional.
- If unset, keys are stored under OS local app data (`.../Grundet/DataProtection-Keys`).
- In production, set `GRUNDET_DATA_PROTECTION_KEYS_PATH` to a persistent mounted path shared by API instances for stable cookie/session behavior.
