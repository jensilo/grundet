import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { dayKey, formatDayLabel, formatRelativeDate, relativeDate } from "../format-date";

describe("formatRelativeDate", () => {
  const NOW = new Date("2026-05-20T12:00:00Z");

  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(NOW);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('returns "Today" for the current date', () => {
    const result = formatRelativeDate(new Date(NOW.getTime()).toISOString());
    expect(result).toBe("Today");
  });

  it('returns "Yesterday" for one day ago', () => {
    const yesterday = new Date(NOW.getTime() - 86_400_000).toISOString();
    expect(formatRelativeDate(yesterday)).toBe("Yesterday");
  });

  it('returns "3 days ago" for three days ago', () => {
    const threeDaysAgo = new Date(NOW.getTime() - 3 * 86_400_000).toISOString();
    expect(formatRelativeDate(threeDaysAgo)).toBe("3 days ago");
  });

  it('returns "1 month ago" for 31 days ago', () => {
    const date = new Date(NOW.getTime() - 31 * 86_400_000).toISOString();
    expect(formatRelativeDate(date)).toBe("1 month ago");
  });

  it('returns "2 months ago" for 60 days ago', () => {
    const date = new Date(NOW.getTime() - 60 * 86_400_000).toISOString();
    expect(formatRelativeDate(date)).toBe("2 months ago");
  });

  it('returns "1 year ago" for 365 days ago', () => {
    const date = new Date(NOW.getTime() - 365 * 86_400_000).toISOString();
    expect(formatRelativeDate(date)).toBe("1 year ago");
  });

  it('returns "2 years ago" for 730 days ago', () => {
    const date = new Date(NOW.getTime() - 730 * 86_400_000).toISOString();
    expect(formatRelativeDate(date)).toBe("2 years ago");
  });
});

describe("relativeDate", () => {
  const NOW = new Date("2026-05-20T12:00:00Z");

  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(NOW);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('returns "just now" for less than 2 minutes', () => {
    const date = new Date(NOW.getTime() - 60_000).toISOString();
    expect(relativeDate(date)).toBe("just now");
  });

  it('returns "5m ago" for 5 minutes ago', () => {
    const date = new Date(NOW.getTime() - 5 * 60_000).toISOString();
    expect(relativeDate(date)).toBe("5m ago");
  });

  it('returns "2h ago" for 2 hours ago', () => {
    const date = new Date(NOW.getTime() - 2 * 3_600_000).toISOString();
    expect(relativeDate(date)).toBe("2h ago");
  });

  it('returns "3d ago" for 3 days ago', () => {
    const date = new Date(NOW.getTime() - 3 * 86_400_000).toISOString();
    expect(relativeDate(date)).toBe("3d ago");
  });

  it('returns "2mo ago" for 60 days ago', () => {
    const date = new Date(NOW.getTime() - 60 * 86_400_000).toISOString();
    expect(relativeDate(date)).toBe("2mo ago");
  });

  it('returns "1y ago" for 365 days ago', () => {
    const date = new Date(NOW.getTime() - 365 * 86_400_000).toISOString();
    expect(relativeDate(date)).toBe("1y ago");
  });
});

describe("dayKey", () => {
  it("is identical for the same instant", () => {
    const iso = "2026-06-13T12:00:00.000Z";
    expect(dayKey(iso)).toBe(dayKey(iso));
  });

  it("differs for instants three days apart", () => {
    expect(dayKey("2026-06-13T12:00:00.000Z")).not.toBe(dayKey("2026-06-10T12:00:00.000Z"));
  });

  it("buckets by UTC day, not the runtime time zone", () => {
    // 00:30 UTC on June 13 is still June 12 in America/Los_Angeles. The key must
    // follow UTC so server and browser agree (no hydration mismatch) and grouping
    // stays aligned with the backend's UTC date filters.
    expect(dayKey("2026-06-13T00:30:00.000Z")).toBe("2026-6-13");
  });
});

describe("formatDayLabel", () => {
  const NOW = new Date("2026-05-20T12:00:00Z");

  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(NOW);
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('returns "Today" for the current instant', () => {
    expect(formatDayLabel(NOW.toISOString())).toBe("Today");
  });

  it('returns "Yesterday" for exactly 24 hours earlier', () => {
    const yesterday = new Date(NOW.getTime() - 86_400_000).toISOString();
    expect(formatDayLabel(yesterday)).toBe("Yesterday");
  });

  it("returns an absolute label for older dates", () => {
    const older = new Date(NOW.getTime() - 30 * 86_400_000).toISOString();
    const label = formatDayLabel(older);
    expect(label).not.toBe("Today");
    expect(label).not.toBe("Yesterday");
    expect(label).toContain("2026");
  });
});
