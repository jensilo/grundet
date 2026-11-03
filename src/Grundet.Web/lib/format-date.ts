/**
 * Formats an ISO date string as a human-readable relative date.
 * e.g. "Today", "Yesterday", "3 days ago", "2 months ago"
 */
export function formatRelativeDate(iso: string): string {
  const diffMs = Date.now() - new Date(iso).getTime();
  const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));
  if (diffDays === 0) return "Today";
  if (diffDays === 1) return "Yesterday";
  if (diffDays < 30) return `${diffDays} days ago`;
  const diffMonths = Math.floor(diffDays / 30);
  if (diffMonths === 1) return "1 month ago";
  if (diffMonths < 12) return `${diffMonths} months ago`;
  const diffYears = Math.floor(diffMonths / 12);
  return diffYears === 1 ? "1 year ago" : `${diffYears} years ago`;
}

const dayLabelFormatter = new Intl.DateTimeFormat("en-GB", {
  day: "numeric",
  month: "long",
  year: "numeric",
  timeZone: "UTC",
});

/**
 * Stable calendar-day key (UTC) for grouping, e.g. "2026-6-13".
 * Deterministic regardless of the runtime time zone, so server and browser
 * render identical groups (no hydration mismatch) and grouping stays aligned
 * with the backend's UTC date filters. This stays UTC by design — only the
 * *display* label for a day (see {@link formatDayLabel} and
 * components/app/local-date.tsx) is localized to the viewer's browser time
 * zone; the grouping itself never depends on viewer tz.
 */
export function dayKey(iso: string): string {
  const date = new Date(iso);
  return `${date.getUTCFullYear()}-${date.getUTCMonth() + 1}-${date.getUTCDate()}`;
}

/**
 * Human day heading for timelines: "Today", "Yesterday", or "13 June 2026".
 * Days are UTC, matching {@link dayKey}. This is a server-safe, tz-neutral
 * fallback — it is used directly for non-interactive/SSR-only contexts and as
 * the initial SSR-matching state for the browser-local variant in
 * components/app/local-date.tsx (LocalDayLabel), which re-renders the label in
 * the viewer's own time zone after mount.
 */
export function formatDayLabel(iso: string): string {
  const key = dayKey(iso);
  const now = new Date();
  const today = `${now.getUTCFullYear()}-${now.getUTCMonth() + 1}-${now.getUTCDate()}`;
  if (key === today) return "Today";

  const yesterdayDate = new Date(now);
  yesterdayDate.setUTCDate(now.getUTCDate() - 1);
  const yesterday = `${yesterdayDate.getUTCFullYear()}-${yesterdayDate.getUTCMonth() + 1}-${yesterdayDate.getUTCDate()}`;
  if (key === yesterday) return "Yesterday";

  return dayLabelFormatter.format(new Date(iso));
}

/**
 * Abbreviated relative date for compact UI surfaces.
 * e.g. "just now", "3m ago", "2h ago", "5d ago"
 */
export function relativeDate(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime();
  const minutes = Math.floor(diff / 60_000);
  if (minutes < 2) return "just now";
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  if (days < 30) return `${days}d ago`;
  const months = Math.floor(days / 30);
  if (months < 12) return `${months}mo ago`;
  return `${Math.floor(months / 12)}y ago`;
}
