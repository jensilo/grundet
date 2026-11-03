"use client";

import { Dialog as DialogPrimitive } from "@base-ui/react/dialog";
import { Search, SearchX } from "lucide-react";
import { usePathname, useRouter } from "next/navigation";
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";

import { StatusBadge } from "@/components/app/status-badge";
import { searchWorkspace } from "@/lib/api/generated/sdk.gen";
import type {
  WorkspaceSearchResponse,
  WorkspaceSearchResultResponse,
} from "@/lib/api/generated/types.gen";
import { cn } from "@/lib/utils";

import type { ShellProject } from "./app-shell-types";

const DebounceMs = 180;

type SearchScope = "all" | "project";
type SearchStatus = "idle" | "loading" | "done" | "error";

type CommandPaletteContextValue = {
  open: () => void;
};

const CommandPaletteContext = createContext<CommandPaletteContextValue | null>(null);

export function useCommandPalette(): CommandPaletteContextValue {
  const context = useContext(CommandPaletteContext);
  if (!context) {
    throw new Error("useCommandPalette must be used within a CommandPaletteProvider");
  }

  return context;
}

function projectIdFromPath(pathname: string): string | null {
  const match = pathname.match(/\/projects\/([^/]+)/);
  return match ? match[1] : null;
}

function hrefForResult(result: WorkspaceSearchResultResponse): string {
  switch (result.type) {
    case "requirement":
      return `/projects/${result.projectId}/requirements/${result.id}`;
    case "workitem":
      return `/projects/${result.projectId}/work-items/${result.id}`;
    default:
      return `/projects/${result.projectId}/features`;
  }
}

export function CommandPaletteProvider({
  projects,
  children,
}: {
  projects: ShellProject[];
  children: React.ReactNode;
}): React.JSX.Element {
  const [isOpen, setIsOpen] = useState(false);
  const open = useCallback(() => setIsOpen(true), []);
  const value = useMemo(() => ({ open }), [open]);

  useEffect(() => {
    function onKeyDown(event: KeyboardEvent): void {
      if ((event.metaKey || event.ctrlKey) && event.key.toLowerCase() === "k") {
        event.preventDefault();
        setIsOpen((previous) => !previous);
      }
    }

    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);

  return (
    <CommandPaletteContext.Provider value={value}>
      {children}
      <CommandPalette projects={projects} open={isOpen} onOpenChange={setIsOpen} />
    </CommandPaletteContext.Provider>
  );
}

function CommandPalette({
  projects,
  open,
  onOpenChange,
}: {
  projects: ShellProject[];
  open: boolean;
  onOpenChange: (open: boolean) => void;
}): React.JSX.Element {
  const router = useRouter();
  const pathname = usePathname();

  const currentProjectId = projectIdFromPath(pathname);
  const currentProject = currentProjectId
    ? (projects.find((project) => project.id === currentProjectId) ?? null)
    : null;

  const [query, setQuery] = useState("");
  const [scope, setScope] = useState<SearchScope>("all");
  const [results, setResults] = useState<WorkspaceSearchResponse | null>(null);
  const [status, setStatus] = useState<SearchStatus>("idle");
  const [activeIndex, setActiveIndex] = useState(0);

  const activeItemRef = useRef<HTMLButtonElement | null>(null);

  // Reset transient state whenever the palette closes so the next open starts fresh.
  function handleOpenChange(nextOpen: boolean): void {
    onOpenChange(nextOpen);
    if (!nextOpen) {
      setQuery("");
      setResults(null);
      setStatus("idle");
      setScope("all");
      setActiveIndex(0);
    }
  }

  // Drop stale results the moment the query or scope changes so they can never be
  // rendered or navigated to (click/Enter) against an input/scope they don't match.
  // The debounced effect refetches; until it resolves we show loading, not old matches.
  function resetResultsForInput(nextQuery: string): void {
    setResults(null);
    setActiveIndex(0);
    setStatus(nextQuery.trim().length > 0 ? "loading" : "idle");
  }

  const effectiveScope: SearchScope = currentProject ? scope : "all";
  const scopedProjectId = effectiveScope === "project" ? currentProjectId : null;

  useEffect(() => {
    if (!open) {
      return;
    }

    // Empty query renders the idle prompt (gated on the trimmed value below), so there is no
    // state to reset here — returning early avoids a synchronous setState inside the effect.
    const trimmed = query.trim();
    if (trimmed.length === 0) {
      return;
    }

    let cancelled = false;

    const handle = setTimeout(() => {
      setStatus("loading");
      void (async () => {
        try {
          const response = await searchWorkspace({
            query: {
              q: trimmed,
              ...(scopedProjectId ? { projectId: scopedProjectId } : {}),
            },
          });

          if (cancelled) {
            return;
          }

          if (response.error || !response.data) {
            setResults(null);
            setStatus("error");
            return;
          }

          setResults(response.data);
          setStatus("done");
          setActiveIndex(0);
        } catch {
          if (!cancelled) {
            setResults(null);
            setStatus("error");
          }
        }
      })();
    }, DebounceMs);

    return () => {
      cancelled = true;
      clearTimeout(handle);
    };
  }, [open, query, scopedProjectId]);

  const groups = useMemo(() => {
    if (!results) {
      return [] as { key: string; label: string; items: WorkspaceSearchResultResponse[] }[];
    }

    return [
      { key: "requirement", label: "Intent", items: results.requirements },
      { key: "workitem", label: "Work", items: results.workItems },
      { key: "feature", label: "Features", items: results.features },
    ].filter((group) => group.items.length > 0);
  }, [results]);

  const flatItems = useMemo(() => groups.flatMap((group) => group.items), [groups]);

  const groupsWithOffset = useMemo(() => {
    let offset = 0;
    return groups.map((group) => {
      const start = offset;
      offset += group.items.length;
      return { ...group, start };
    });
  }, [groups]);

  useEffect(() => {
    activeItemRef.current?.scrollIntoView?.({ block: "nearest" });
  }, [activeIndex]);

  function navigate(result: WorkspaceSearchResultResponse): void {
    handleOpenChange(false);
    router.push(hrefForResult(result));
  }

  function handleInputKeyDown(event: React.KeyboardEvent<HTMLInputElement>): void {
    if (flatItems.length === 0) {
      return;
    }

    if (event.key === "ArrowDown") {
      event.preventDefault();
      setActiveIndex((index) => Math.min(index + 1, flatItems.length - 1));
    } else if (event.key === "ArrowUp") {
      event.preventDefault();
      setActiveIndex((index) => Math.max(index - 1, 0));
    } else if (event.key === "Enter") {
      event.preventDefault();
      const result = flatItems[activeIndex];
      if (result) {
        navigate(result);
      }
    }
  }

  const trimmedQuery = query.trim();
  const hasResults = flatItems.length > 0;

  return (
    <DialogPrimitive.Root open={open} onOpenChange={handleOpenChange}>
      <DialogPrimitive.Portal>
        <DialogPrimitive.Backdrop
          className={cn(
            "data-open:animate-in data-open:fade-in-0 data-closed:animate-out data-closed:fade-out-0",
            "fixed inset-0 z-50 bg-black/10 duration-100 supports-backdrop-filter:backdrop-blur-xs"
          )}
        />
        <DialogPrimitive.Popup
          className={cn(
            "bg-popover text-popover-foreground ring-foreground/10",
            "data-open:animate-in data-open:fade-in-0 data-open:zoom-in-95",
            "data-closed:animate-out data-closed:fade-out-0 data-closed:zoom-out-95",
            "fixed top-[12vh] left-1/2 z-50 flex max-h-[70vh] w-full max-w-xl -translate-x-1/2 flex-col",
            "overflow-hidden rounded-xl ring-1 duration-100 outline-none"
          )}
          aria-label="Search workspace"
        >
          <DialogPrimitive.Title className="sr-only">Search workspace</DialogPrimitive.Title>
          <DialogPrimitive.Description className="sr-only">
            Find intent, work, and features across your accessible projects.
          </DialogPrimitive.Description>

          <div className="border-border flex items-center gap-2 border-b px-3">
            <Search
              aria-hidden={true}
              className="text-muted-foreground pointer-events-none size-4 shrink-0"
            />
            <input
              autoFocus={true}
              value={query}
              onChange={(event) => {
                resetResultsForInput(event.target.value);
                setQuery(event.target.value);
              }}
              onKeyDown={handleInputKeyDown}
              placeholder="Search intent, work, and features…"
              aria-label="Search intent, work, and features"
              className="placeholder:text-muted-foreground h-11 w-full border-0 bg-transparent text-sm outline-none"
            />
            <kbd className="border-border bg-muted text-muted-foreground pointer-events-none hidden rounded border px-1.5 py-0.5 font-mono text-[10px] sm:inline-block">
              Esc
            </kbd>
          </div>

          {currentProject && (
            <div className="border-border flex items-center gap-1.5 border-b px-3 py-2">
              <ScopeChip
                active={effectiveScope === "all"}
                onClick={() => {
                  resetResultsForInput(query);
                  setScope("all");
                }}
              >
                All projects
              </ScopeChip>
              <ScopeChip
                active={effectiveScope === "project"}
                onClick={() => {
                  resetResultsForInput(query);
                  setScope("project");
                }}
              >
                {currentProject.name}
              </ScopeChip>
            </div>
          )}

          <div className="min-h-0 flex-1 overflow-y-auto p-2">
            {trimmedQuery.length === 0 && (
              <p className="text-muted-foreground px-3 py-8 text-center text-sm">
                Type to search across your workspace.
              </p>
            )}

            {trimmedQuery.length > 0 && status === "error" && (
              <p className="text-muted-foreground px-3 py-8 text-center text-sm">
                Something went wrong. Try again.
              </p>
            )}

            {trimmedQuery.length > 0 && status !== "error" && !hasResults && (
              <div className="text-muted-foreground flex flex-col items-center gap-2 px-3 py-8 text-center text-sm">
                {status !== "done" ? (
                  <Spinner />
                ) : (
                  <>
                    <SearchX aria-hidden={true} className="size-5" />
                    <span>
                      No results for <span className="text-foreground">{trimmedQuery}</span>
                    </span>
                  </>
                )}
              </div>
            )}

            {hasResults &&
              groupsWithOffset.map((group) => (
                <div key={group.key} className="mb-1 last:mb-0">
                  <p className="text-muted-foreground px-3 pt-2 pb-1 text-xs font-medium">
                    {group.label}
                  </p>
                  <ul>
                    {group.items.map((item, itemIndex) => {
                      const index = group.start + itemIndex;
                      const isActive = index === activeIndex;
                      return (
                        <li key={`${item.type}:${item.id}`}>
                          <button
                            type="button"
                            ref={isActive ? activeItemRef : undefined}
                            onClick={() => navigate(item)}
                            onMouseMove={() => setActiveIndex(index)}
                            className={cn(
                              "flex w-full items-center gap-3 rounded-md px-3 py-2 text-left",
                              isActive ? "bg-muted" : "bg-transparent"
                            )}
                          >
                            <div className="min-w-0 flex-1">
                              <p className="text-foreground truncate text-sm font-medium">
                                {item.title}
                              </p>
                              <p className="text-muted-foreground truncate text-xs">
                                {item.subTitle ? `${item.subTitle} · ` : ""}
                                {item.projectName} · {item.teamName}
                              </p>
                            </div>
                            {item.status && (
                              <StatusBadge status={item.status} className="shrink-0" />
                            )}
                          </button>
                        </li>
                      );
                    })}
                  </ul>
                </div>
              ))}
          </div>

          <div className="border-border text-muted-foreground hidden items-center gap-4 border-t px-3 py-2 text-xs sm:flex">
            <KeyHint keys="↑↓">Navigate</KeyHint>
            <KeyHint keys="↵">Open</KeyHint>
            <KeyHint keys="Esc">Close</KeyHint>
          </div>
        </DialogPrimitive.Popup>
      </DialogPrimitive.Portal>
    </DialogPrimitive.Root>
  );
}

function ScopeChip({
  active,
  onClick,
  children,
}: {
  active: boolean;
  onClick: () => void;
  children: React.ReactNode;
}): React.JSX.Element {
  return (
    <button
      type="button"
      onClick={onClick}
      className={cn(
        "max-w-[14rem] truncate rounded-full px-2.5 py-1 text-xs font-medium transition-colors",
        active
          ? "bg-primary text-primary-foreground"
          : "bg-muted text-muted-foreground hover:text-foreground"
      )}
    >
      {children}
    </button>
  );
}

function KeyHint({
  keys,
  children,
}: {
  keys: string;
  children: React.ReactNode;
}): React.JSX.Element {
  return (
    <span className="flex items-center gap-1.5">
      <kbd className="border-border bg-muted rounded border px-1.5 py-0.5 font-mono text-[10px]">
        {keys}
      </kbd>
      {children}
    </span>
  );
}

function Spinner(): React.JSX.Element {
  return (
    <span
      aria-label="Searching"
      className="border-muted-foreground/30 border-t-muted-foreground size-4 animate-spin rounded-full border-2"
    />
  );
}
