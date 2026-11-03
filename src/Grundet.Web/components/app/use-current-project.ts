"use client";

import { usePathname } from "next/navigation";
import { useEffect, useSyncExternalStore } from "react";

import {
  getServerLastProjectId,
  readLastProjectId,
  resolveRouteProject,
  type ShellProject,
  subscribeLastProjectId,
  writeLastProjectId,
} from "@/components/app/app-shell-types";

// Resolves the project that should currently be considered "active" for nav links and the
// project switcher. Shared by `AppSidebar` and `MobileNavDrawer` so both agree on the same
// project without duplicating the resolution logic.
//
// Project selection resumes where the user left off. On a project route the URL wins and we
// remember it; off-route (e.g. dashboard) we fall back to the last remembered project, then to
// the first project. The saved id is read via `useSyncExternalStore` so it is hydration-safe: the
// server snapshot is null (renders the first-project fallback) and the client reconciles to the
// stored value after mount.
export function useCurrentProject(projects: ShellProject[]): ShellProject | null {
  const pathname = usePathname();
  const routeProject = resolveRouteProject(pathname, projects);
  const routeProjectId = routeProject?.id ?? null;
  const savedProjectId = useSyncExternalStore(
    subscribeLastProjectId,
    readLastProjectId,
    getServerLastProjectId
  );

  // On a project route, remember it. Writing to an external system (localStorage) from React state
  // is the intended use of an effect; the write notifies the store so the snapshot stays current.
  useEffect(() => {
    if (routeProjectId) {
      writeLastProjectId(routeProjectId);
    }
  }, [routeProjectId]);

  // Validate the saved id against the live project list so a deleted/stale project falls back
  // cleanly to the first project.
  const savedProject =
    savedProjectId !== null ? (projects.find((p) => p.id === savedProjectId) ?? null) : null;
  return routeProject ?? savedProject ?? projects[0] ?? null;
}
