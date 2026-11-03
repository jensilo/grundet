"use client";

import { ChevronDown, Search, SlidersHorizontal, X } from "lucide-react";
import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import type * as React from "react";
import { useEffect, useState } from "react";

import { type FeatureOption, FeaturePicker } from "@/components/app/composer/feature-picker";
import { UserAvatar } from "@/components/app/user-avatar";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { cn } from "@/lib/utils";

type FilterOption = {
  value: string;
  label: string;
};

const AnyAssigneeValue = "__any";
const UnassignedValue = "none";
const PanelStoragePrefix = "grundet:browse-filter-panel";
const LABELS_COLLAPSE_THRESHOLD = 20;
const LABELS_VISIBLE_COUNT = 18;
const SORT_OPTIONS = [
  { value: "priority", label: "Priority" },
  { value: "updated", label: "Recently updated" },
  { value: "created", label: "Recently created" },
] as const;

export function BrowseFilterPanel({
  kind,
  query,
  sort,
  projectId,
  activeFeatureId,
  activeFeatureName,
  statusOptions,
  selectedStatuses,
  priorityOptions,
  selectedPriorities,
  labelOptions,
  selectedLabels,
  typeOptions,
  selectedTypes,
  blocked,
  assignees,
  activeAssignee,
}: {
  kind: "intent" | "work";
  query: string;
  sort: string;
  projectId: string;
  activeFeatureId?: string;
  // Display name of the active feature filter, when one is set — the picker shows it without a fetch.
  activeFeatureName?: string;
  // Assignee filtering is work-only; omit `assignees` to hide the control.
  assignees?: FilterOption[];
  activeAssignee?: string;
  statusOptions: FilterOption[];
  selectedStatuses: string[];
  priorityOptions: FilterOption[];
  selectedPriorities: string[];
  labelOptions: FilterOption[];
  selectedLabels: string[];
  typeOptions?: FilterOption[];
  selectedTypes?: string[];
  blocked?: boolean;
}): React.JSX.Element {
  const pathname = usePathname();
  const router = useRouter();
  const searchParams = useSearchParams();
  const panelStorageKey = `${PanelStoragePrefix}:${kind}`;
  const types = selectedTypes ?? [];
  const blockedActive = blocked !== undefined;
  const activeAdvancedFilterCount =
    selectedStatuses.length + selectedPriorities.length + types.length + (blockedActive ? 1 : 0);
  const hasActiveState = Boolean(
    query.trim() ||
    sort !== "updated" ||
    activeFeatureId ||
    activeAssignee ||
    activeAdvancedFilterCount > 0 ||
    selectedLabels.length > 0 ||
    types.length > 0 ||
    blockedActive
  );
  const defaultPanelOpen = activeAdvancedFilterCount > 0;
  const [filtersOpen, setFiltersOpen] = useState(defaultPanelOpen);
  const [labelsExpanded, setLabelsExpanded] = useState(false);

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      setFiltersOpen(readStoredBoolean(panelStorageKey, defaultPanelOpen));
    }, 0);

    return () => window.clearTimeout(timeoutId);
  }, [defaultPanelOpen, panelStorageKey]);

  function setPanelOpen(open: boolean): void {
    setFiltersOpen(open);
    window.localStorage.setItem(panelStorageKey, String(open));
  }

  function hrefFor(updates: Record<string, string | string[] | undefined>): string {
    const params = new URLSearchParams(searchParams.toString());
    params.delete("page");

    for (const [key, value] of Object.entries(updates)) {
      params.delete(key);
      if (Array.isArray(value)) {
        for (const item of value) {
          params.append(key, item);
        }
      } else if (value) {
        params.set(key, value);
      }
    }

    const queryString = params.toString();
    return queryString ? `${pathname}?${queryString}` : pathname;
  }

  function toggleValue(param: string, value: string, selected: string[]): string {
    const next = selected.includes(value)
      ? selected.filter((item) => item !== value)
      : [...selected, value];
    return hrefFor({ [param]: next.length > 0 ? next : undefined });
  }

  function handleFeaturePick(feature: FeatureOption | null): void {
    router.push(hrefFor({ featureId: feature?.id }));
  }

  function handleSortChange(value: string | null): void {
    if (!value) {
      return;
    }

    router.push(hrefFor({ sort: value === "updated" ? undefined : value }));
  }

  function handleAssigneeChange(value: string | null): void {
    if (!value) {
      return;
    }

    router.push(hrefFor({ assignedTo: value === AnyAssigneeValue ? undefined : value }));
  }

  function assigneeTriggerLabel(): string {
    if (!activeAssignee) {
      return "Anyone";
    }
    if (activeAssignee === UnassignedValue) {
      return "Unassigned";
    }
    return assignees?.find((a) => a.value === activeAssignee)?.label ?? activeAssignee;
  }

  return (
    <section className="bg-card ring-foreground/10 rounded-xl p-3 ring-1">
      <div className="space-y-2">
        <form action={pathname} method="get" className="flex w-full min-w-0 items-center gap-2">
          {activeFeatureId && <input type="hidden" name="featureId" value={activeFeatureId} />}
          {activeAssignee && <input type="hidden" name="assignedTo" value={activeAssignee} />}
          {selectedStatuses.map((status) => (
            <input key={status} type="hidden" name="status" value={status} />
          ))}
          {selectedPriorities.map((priority) => (
            <input key={priority} type="hidden" name="priority" value={priority} />
          ))}
          {selectedLabels.map((label) => (
            <input key={label} type="hidden" name="labels" value={label} />
          ))}
          {types.map((type) => (
            <input key={type} type="hidden" name="type" value={type} />
          ))}
          {blockedActive && <input type="hidden" name="blocked" value={String(blocked)} />}
          {sort !== "updated" && <input type="hidden" name="sort" value={sort} />}
          <label className="sr-only" htmlFor={`${kind}-browse-q`}>
            Search {kind}
          </label>
          <div className="relative min-w-0 flex-1">
            <Search
              className="text-muted-foreground pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2"
              aria-hidden={true}
            />
            <Input
              key={query}
              id={`${kind}-browse-q`}
              name="q"
              defaultValue={query}
              className="pl-8"
              placeholder={
                kind === "intent" ? "Search intent in this project" : "Search work in this project"
              }
            />
          </div>
          <Button type="submit" variant="outline">
            Search
          </Button>
        </form>

        <div className="flex flex-wrap items-center gap-2">
          <label className="sr-only" htmlFor={`${kind}-sort`}>
            Sort by
          </label>
          <Select value={sort} onValueChange={handleSortChange}>
            <SelectTrigger id={`${kind}-sort`} className="w-44 flex-none">
              <SelectValue>{getOptionLabel([...SORT_OPTIONS], sort)}</SelectValue>
            </SelectTrigger>
            <SelectContent align="start">
              {SORT_OPTIONS.map((option) => (
                <SelectItem key={option.value} value={option.value}>
                  {option.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>

          <label className="sr-only" htmlFor={`${kind}-feature-scope`}>
            Feature scope
          </label>
          <div className="min-w-0 flex-1">
            <FeaturePicker
              id={`${kind}-feature-scope`}
              projectId={projectId}
              value={
                activeFeatureId
                  ? { id: activeFeatureId, name: activeFeatureName ?? activeFeatureId }
                  : null
              }
              onChange={handleFeaturePick}
              clearLabel="All features"
            />
          </div>

          {assignees && (
            <>
              <label className="sr-only" htmlFor={`${kind}-assignee`}>
                Assignee
              </label>
              <Select
                value={activeAssignee ?? AnyAssigneeValue}
                onValueChange={handleAssigneeChange}
              >
                <SelectTrigger id={`${kind}-assignee`} className="w-44 flex-none">
                  <SelectValue>
                    {activeAssignee && activeAssignee !== UnassignedValue ? (
                      <>
                        <UserAvatar
                          userId={activeAssignee}
                          name={assigneeTriggerLabel()}
                          size="sm"
                          className="size-4 shrink-0"
                        />
                        <span className="truncate">{assigneeTriggerLabel()}</span>
                      </>
                    ) : (
                      assigneeTriggerLabel()
                    )}
                  </SelectValue>
                </SelectTrigger>
                <SelectContent align="start">
                  <SelectItem value={AnyAssigneeValue}>Anyone</SelectItem>
                  <SelectItem value={UnassignedValue}>Unassigned</SelectItem>
                  {assignees.map((assignee) => (
                    <SelectItem key={assignee.value} value={assignee.value}>
                      <UserAvatar
                        userId={assignee.value}
                        name={assignee.label}
                        size="default"
                        className="size-5"
                      />
                      {assignee.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </>
          )}

          <Button
            type="button"
            variant="outline"
            className="gap-2"
            aria-expanded={filtersOpen}
            onClick={() => setPanelOpen(!filtersOpen)}
          >
            <SlidersHorizontal className="size-4" aria-hidden={true} />
            Filters
            {activeAdvancedFilterCount > 0 && (
              <span className="bg-accent text-accent-foreground rounded-sm px-1.5 text-xs tabular-nums">
                {activeAdvancedFilterCount}
              </span>
            )}
          </Button>
          {hasActiveState && (
            <Button
              variant="ghost"
              nativeButton={false}
              render={
                <Link
                  href={hrefFor({
                    assignedTo: undefined,
                    blocked: undefined,
                    featureId: undefined,
                    labels: undefined,
                    priority: undefined,
                    q: undefined,
                    sort: undefined,
                    status: undefined,
                    type: undefined,
                  })}
                >
                  Clear all
                </Link>
              }
            />
          )}
        </div>
      </div>

      {labelOptions.length > 0 && (
        <div className="border-border mt-3 border-t pt-3">
          <div className="flex flex-wrap gap-1.5">
            {(labelOptions.length >= LABELS_COLLAPSE_THRESHOLD && !labelsExpanded
              ? labelOptions.slice(0, LABELS_VISIBLE_COUNT)
              : labelOptions
            ).map((option) => (
              <FilterChip
                key={option.value}
                href={toggleValue("labels", option.value, selectedLabels)}
                selected={selectedLabels.includes(option.value)}
              >
                {option.label}
              </FilterChip>
            ))}
            {labelOptions.length >= LABELS_COLLAPSE_THRESHOLD && (
              <button
                type="button"
                className="text-muted-foreground hover:text-foreground inline-flex h-6 items-center gap-1 rounded-sm px-2 text-xs font-medium transition-colors"
                onClick={() => setLabelsExpanded(!labelsExpanded)}
              >
                {labelsExpanded ? (
                  "Show less"
                ) : (
                  <>
                    <ChevronDown className="size-3" aria-hidden={true} />
                    {`+${labelOptions.length - LABELS_VISIBLE_COUNT} more`}
                  </>
                )}
              </button>
            )}
          </div>
        </div>
      )}

      {filtersOpen ? (
        <div className="border-border mt-3 grid gap-x-8 gap-y-4 border-t pt-3 sm:grid-cols-2">
          <FilterGroup title="Status">
            <div className="flex flex-wrap gap-1.5">
              {statusOptions.map((option) => (
                <FilterChip
                  key={option.value}
                  href={toggleValue("status", option.value, selectedStatuses)}
                  selected={selectedStatuses.includes(option.value)}
                >
                  {option.label}
                </FilterChip>
              ))}
            </div>
          </FilterGroup>

          <FilterGroup title="Priority">
            <div className="flex flex-wrap gap-1.5">
              {priorityOptions.map((option) => (
                <FilterChip
                  key={option.value}
                  href={toggleValue("priority", option.value, selectedPriorities)}
                  selected={selectedPriorities.includes(option.value)}
                >
                  {option.label}
                </FilterChip>
              ))}
            </div>
          </FilterGroup>

          {typeOptions && (
            <FilterGroup
              title="Type"
              description="Filter by what a work item is — a Bug fixes broken behaviour, a Feature adds capability, a Task is everything else."
            >
              <div className="flex flex-wrap gap-1.5">
                {typeOptions.map((option) => (
                  <FilterChip
                    key={option.value}
                    href={toggleValue("type", option.value, types)}
                    selected={types.includes(option.value)}
                  >
                    {option.label}
                  </FilterChip>
                ))}
              </div>
            </FilterGroup>
          )}

          {typeOptions && (
            <FilterGroup title="Blocked">
              <div className="flex flex-wrap gap-1.5">
                <FilterChip
                  href={hrefFor({ blocked: blocked === true ? undefined : "true" })}
                  selected={blocked === true}
                >
                  Blocked
                </FilterChip>
                <FilterChip
                  href={hrefFor({ blocked: blocked === false ? undefined : "false" })}
                  selected={blocked === false}
                >
                  Not blocked
                </FilterChip>
              </div>
            </FilterGroup>
          )}
        </div>
      ) : (
        activeAdvancedFilterCount > 0 && (
          <div className="border-border mt-3 flex flex-wrap items-center gap-1.5 border-t pt-3">
            <span className="text-muted-foreground pr-1 text-xs font-medium">Active filters</span>
            {selectedStatuses.map((status) => (
              <FilterChip
                key={status}
                href={toggleValue("status", status, selectedStatuses)}
                selected={true}
              >
                Status: {getOptionLabel(statusOptions, status)}
              </FilterChip>
            ))}
            {selectedPriorities.map((priority) => (
              <FilterChip
                key={priority}
                href={toggleValue("priority", priority, selectedPriorities)}
                selected={true}
              >
                Priority: {getOptionLabel(priorityOptions, priority)}
              </FilterChip>
            ))}
            {types.map((type) => (
              <FilterChip key={type} href={toggleValue("type", type, types)} selected={true}>
                Type: {getOptionLabel(typeOptions ?? [], type)}
              </FilterChip>
            ))}
            {blockedActive && (
              <FilterChip href={hrefFor({ blocked: undefined })} selected={true}>
                {blocked ? "Blocked" : "Not blocked"}
              </FilterChip>
            )}
          </div>
        )
      )}
    </section>
  );
}

function readStoredBoolean(storageKey: string, fallback: boolean): boolean {
  if (typeof window === "undefined") {
    return fallback;
  }

  const raw = window.localStorage.getItem(storageKey);
  if (raw === null) {
    return fallback;
  }

  return raw === "true";
}

function getOptionLabel(options: FilterOption[], value: string): string {
  return options.find((option) => option.value === value)?.label ?? value;
}

function FilterGroup({
  title,
  description,
  className,
  children,
}: {
  title: string;
  description?: string;
  className?: string;
  children: React.ReactNode;
}): React.JSX.Element {
  return (
    <div className={cn("min-w-0 space-y-2", className)}>
      <p className="text-muted-foreground text-xs font-medium">{title}</p>
      {children}
      {description && (
        <p className="text-muted-foreground text-xs leading-relaxed">{description}</p>
      )}
    </div>
  );
}

function FilterChip({
  href,
  selected,
  children,
}: {
  href: string;
  selected: boolean;
  children: React.ReactNode;
}): React.JSX.Element {
  return (
    <Link
      href={href}
      className={cn(
        "border-border bg-background text-muted-foreground hover:bg-muted hover:text-foreground inline-flex h-6 max-w-full items-center gap-1 rounded-sm border px-2 text-xs font-medium transition-colors",
        selected &&
          "bg-accent text-accent-foreground hover:bg-accent hover:text-accent-foreground border-transparent"
      )}
    >
      <span className="truncate">{children}</span>
      {selected && <X className="size-3 shrink-0" aria-hidden={true} />}
    </Link>
  );
}
