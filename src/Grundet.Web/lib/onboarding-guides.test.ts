import { beforeEach, describe, expect, it, vi } from "vitest";

const USER_ID = "user-1";
const STORAGE_KEY = `grundet.onboarding.v1.${USER_ID}`;
const UNSCOPED_KEY = "grundet.onboarding.v1";

// The module keeps session-scoped fallback state, so each test imports a fresh copy.
async function importFresh() {
  vi.resetModules();
  return await import("./onboarding-guides");
}

describe("onboarding guide storage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    window.localStorage.clear();
  });

  it("defaults to unseen and persists a marked guide", async () => {
    const { isGuideSeen, markGuideSeen } = await importFresh();

    expect(isGuideSeen("project", USER_ID)).toBe(false);

    markGuideSeen("project", USER_ID);

    expect(isGuideSeen("project", USER_ID)).toBe(true);
    expect(isGuideSeen("intent", USER_ID)).toBe(false);
    expect(JSON.parse(window.localStorage.getItem(STORAGE_KEY) ?? "{}")).toEqual({
      seenGuides: { project: true },
    });
  });

  it("tracks each journey guide independently, including the feature guide", async () => {
    const { isGuideSeen, markGuideSeen } = await importFresh();

    markGuideSeen("feature", USER_ID);

    expect(isGuideSeen("feature", USER_ID)).toBe(true);
    expect(isGuideSeen("project", USER_ID)).toBe(false);
    expect(isGuideSeen("intent", USER_ID)).toBe(false);
    expect(isGuideSeen("work", USER_ID)).toBe(false);
    expect(JSON.parse(window.localStorage.getItem(STORAGE_KEY) ?? "{}")).toEqual({
      seenGuides: { feature: true },
    });
  });

  it("does not share seen-state between different users", async () => {
    const { isGuideSeen, markGuideSeen } = await importFresh();

    markGuideSeen("project", "user-1");

    expect(isGuideSeen("project", "user-1")).toBe(true);
    expect(isGuideSeen("project", "user-2")).toBe(false);
    expect(window.localStorage.getItem("grundet.onboarding.v1.user-2")).toBeNull();
  });

  it("is a no-op when no user id is available, so pre-hydration clicks never write an unscoped key", async () => {
    const { isGuideSeen, markGuideSeen } = await importFresh();

    markGuideSeen("project", null);

    expect(isGuideSeen("project", null)).toBe(false);
    expect(window.localStorage.getItem(UNSCOPED_KEY)).toBeNull();
  });

  it("survives corrupt JSON without throwing and recovers on the next write", async () => {
    window.localStorage.setItem(STORAGE_KEY, "{not json");
    const { isGuideSeen, markGuideSeen } = await importFresh();

    expect(isGuideSeen("work", USER_ID)).toBe(false);

    markGuideSeen("work", USER_ID);

    expect(isGuideSeen("work", USER_ID)).toBe(true);
    expect(JSON.parse(window.localStorage.getItem(STORAGE_KEY) ?? "{}")).toEqual({
      seenGuides: { work: true },
    });
  });

  it("ignores incompatible shapes and unknown guide ids", async () => {
    window.localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify({ seenGuides: { project: "yes", bogus: true, intent: true } })
    );
    const { isGuideSeen } = await importFresh();

    expect(isGuideSeen("project", USER_ID)).toBe(false);
    expect(isGuideSeen("intent", USER_ID)).toBe(true);
  });

  it("falls back to in-memory state for the session when storage throws", async () => {
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
      throw new Error("storage unavailable");
    });
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
      throw new Error("storage unavailable");
    });
    const { isGuideSeen, markGuideSeen } = await importFresh();

    expect(isGuideSeen("intent", USER_ID)).toBe(false);
    expect(() => markGuideSeen("intent", USER_ID)).not.toThrow();
    expect(isGuideSeen("intent", USER_ID)).toBe(true);
  });
});
