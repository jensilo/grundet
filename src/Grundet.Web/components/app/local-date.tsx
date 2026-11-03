"use client";

import { useEffect, useState } from "react";

import { formatDayLabel } from "@/lib/format-date";

// Locale left undefined (browser default) rather than pinned to "en-GB": once the
// label is tz-local it should also read naturally in the viewer's own locale. The
// day/month(long)/year shape matches the UTC fallback so the swap doesn't reflow.
const localDayLabelFormatter = new Intl.DateTimeFormat(undefined, {
  day: "numeric",
  month: "long",
  year: "numeric",
});

function localDayKey(date: Date): string {
  return `${date.getFullYear()}-${date.getMonth() + 1}-${date.getDate()}`;
}

function computeLocalDayLabel(iso: string): string {
  const date = new Date(iso);
  const now = new Date();
  const key = localDayKey(date);
  if (key === localDayKey(now)) return "Today";

  const yesterday = new Date(now);
  yesterday.setDate(now.getDate() - 1);
  if (key === localDayKey(yesterday)) return "Yesterday";

  return localDayLabelFormatter.format(date);
}

/**
 * Calendar day label ("Today" / "Yesterday" / "13 June 2026") in the viewer's
 * own browser time zone, auto-detected — no stored preference, no settings UI.
 *
 * The server can't know the viewer's time zone, so it renders the existing
 * UTC-based {@link formatDayLabel} as a stable SSR fallback; that's also this
 * component's initial client state, so the two match at hydration time. Once
 * mounted, an effect recomputes the label against the browser's local
 * calendar day and swaps it in — a real, expected text change after hydration
 * has already committed, so the wrapping element carries
 * `suppressHydrationWarning` as a safeguard.
 */
export function LocalDayLabel({ iso }: { iso: string }): React.JSX.Element {
  const [label, setLabel] = useState(() => formatDayLabel(iso));

  useEffect(() => {
    // Swaps the SSR-matching UTC fallback for the browser-local label once mounted.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setLabel(computeLocalDayLabel(iso));
  }, [iso]);

  return <span suppressHydrationWarning>{label}</span>;
}
