// Per-device "seen" markers for the opt-in contextual guides. Low-stakes by design:
// they never gate anything, so localStorage (no cross-device sync) is acceptable.
// The versioned key lets a future schema change start fresh instead of misreading.
// Keys are scoped per user so a new user on a shared device starts unseen. Without a
// user id (pre-hydration) state is always "unseen" and writes are no-ops — nothing is
// ever stored under an unscoped key.

const BASE_STORAGE_KEY = "grundet.onboarding.v1";

export type GuideId = "project" | "feature" | "intent" | "work";

const GUIDE_IDS: readonly GuideId[] = ["project", "feature", "intent", "work"];

type OnboardingGuideState = {
  seenGuides: Partial<Record<GuideId, boolean>>;
};

function storageKey(userId: string): string {
  return `${BASE_STORAGE_KEY}.${userId}`;
}

// Session-scoped fallback when storage is unavailable (private mode, quota, disabled).
// Guides must never block rendering, so storage failures degrade to in-memory state.
const memoryFallback = new Map<string, OnboardingGuideState>();

// Change listeners so components can read seen-state via useSyncExternalStore
// (hydration-safe: the server snapshot is always "unseen").
const listeners = new Set<() => void>();

export function subscribeToGuideState(listener: () => void): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

function defaultState(): OnboardingGuideState {
  return { seenGuides: {} };
}

function parseState(raw: string | null): OnboardingGuideState {
  if (!raw) {
    return defaultState();
  }

  try {
    const parsed: unknown = JSON.parse(raw);
    if (typeof parsed !== "object" || parsed === null) {
      return defaultState();
    }

    const seen: unknown = (parsed as { seenGuides?: unknown }).seenGuides;
    if (typeof seen !== "object" || seen === null) {
      return defaultState();
    }

    const seenGuides: Partial<Record<GuideId, boolean>> = {};
    for (const id of GUIDE_IDS) {
      if ((seen as Record<string, unknown>)[id] === true) {
        seenGuides[id] = true;
      }
    }
    return { seenGuides };
  } catch {
    return defaultState();
  }
}

function readState(userId: string | null): OnboardingGuideState {
  if (typeof window === "undefined" || userId === null) {
    return defaultState();
  }

  const key = storageKey(userId);
  const fallback = memoryFallback.get(key);
  if (fallback) {
    return fallback;
  }

  try {
    return parseState(window.localStorage.getItem(key));
  } catch {
    const state = defaultState();
    memoryFallback.set(key, state);
    return state;
  }
}

export function isGuideSeen(id: GuideId, userId: string | null): boolean {
  return readState(userId).seenGuides[id] === true;
}

export function markGuideSeen(id: GuideId, userId: string | null): void {
  // Before user-context hydration, userId is null. Writing here would land in the
  // unscoped legacy key and leak "seen" state across whichever user hydrates next.
  if (userId === null) {
    return;
  }

  const state = readState(userId);
  state.seenGuides[id] = true;

  if (typeof window === "undefined") {
    return;
  }

  const key = storageKey(userId);
  try {
    window.localStorage.setItem(key, JSON.stringify(state));
  } catch {
    memoryFallback.set(key, state);
  }

  for (const listener of listeners) {
    listener();
  }
}
