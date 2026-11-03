export type ShellProject = {
  id: string;
  name: string;
  teamId: string;
  teamName: string;
};

export type ShellTeam = {
  id: string;
  name: string;
  projects: Array<{ id: string; name: string }>;
};

// The project named by the current URL, or null when the route isn't project-scoped (e.g.
// dashboard). Pure and URL-only — the off-route fallback (saved project / first project) lives in
// `AppSidebar` so the "real route project" and "fallback project" cases stay unambiguous.
export function resolveRouteProject(
  pathname: string,
  projects: ShellProject[]
): ShellProject | null {
  const match = pathname.match(/\/projects\/([^/]+)/);
  if (!match) {
    return null;
  }

  return projects.find((project) => project.id === match[1]) ?? null;
}

const LAST_PROJECT_KEY = "grundet:last-project";

// The last-selected project id is an external store (localStorage). It's read through
// `useSyncExternalStore` so the value is hydration-safe (server snapshot is null) without a
// setState-in-effect. Storage can throw in private/restricted contexts, so every access is guarded;
// persistence is best-effort and never fatal.
const lastProjectListeners = new Set<() => void>();

// Server snapshot: there is no localStorage on the server, so the saved project is unknown.
export function getServerLastProjectId(): null {
  return null;
}

// Client snapshot. Returns a primitive, so `useSyncExternalStore`'s Object.is comparison is stable
// across renders even though this reads storage each call.
export function readLastProjectId(): string | null {
  if (typeof window === "undefined") {
    return null;
  }
  try {
    return window.localStorage.getItem(LAST_PROJECT_KEY);
  } catch {
    return null;
  }
}

export function subscribeLastProjectId(onChange: () => void): () => void {
  lastProjectListeners.add(onChange);
  // Cross-tab writes fire a `storage` event; same-tab writes are dispatched manually below.
  const onStorage = (event: StorageEvent): void => {
    if (event.key === LAST_PROJECT_KEY) {
      onChange();
    }
  };
  if (typeof window !== "undefined") {
    window.addEventListener("storage", onStorage);
  }
  return () => {
    lastProjectListeners.delete(onChange);
    if (typeof window !== "undefined") {
      window.removeEventListener("storage", onStorage);
    }
  };
}

export function writeLastProjectId(projectId: string): void {
  if (typeof window === "undefined") {
    return;
  }
  try {
    window.localStorage.setItem(LAST_PROJECT_KEY, projectId);
  } catch {
    // Storage unavailable (private mode / restricted) — remembering the project is best-effort.
  }
  // `storage` events don't fire in the writing tab, so notify local subscribers directly.
  for (const listener of lastProjectListeners) {
    listener();
  }
}
