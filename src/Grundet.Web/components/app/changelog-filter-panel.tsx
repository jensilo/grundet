"use client";

import { X } from "lucide-react";
import Link from "next/link";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import type * as React from "react";

import { type FeatureOption, FeaturePicker } from "@/components/app/composer/feature-picker";
import { UserAvatar } from "@/components/app/user-avatar";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { CHANGE_TYPE_OPTIONS, ENTITY_KIND_OPTIONS } from "@/lib/changelog";
import { cn } from "@/lib/utils";

const AllValue = "__all";

type FilterOption = {
  value: string;
  label: string;
};

export function ChangelogFilterPanel({
  projectId,
  authors,
  activeFeatureId,
  activeFeatureName,
  activeAuthorId,
  selectedEntityTypes,
  selectedChangeTypes,
  from,
  to,
}: {
  projectId: string;
  authors: FilterOption[];
  activeFeatureId?: string;
  // Display name of the active feature filter, when one is set — the picker shows it without a fetch.
  activeFeatureName?: string;
  activeAuthorId?: string;
  selectedEntityTypes: string[];
  selectedChangeTypes: string[];
  from: string;
  to: string;
}): React.JSX.Element {
  const pathname = usePathname();
  const router = useRouter();
  const searchParams = useSearchParams();

  const hasActiveState = Boolean(
    activeFeatureId ||
    activeAuthorId ||
    selectedEntityTypes.length > 0 ||
    selectedChangeTypes.length > 0 ||
    from ||
    to
  );

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

  function handleSelectChange(param: string, value: string | null): void {
    if (!value) {
      return;
    }
    router.push(hrefFor({ [param]: value === AllValue ? undefined : value }));
  }

  function handleFeaturePick(feature: FeatureOption | null): void {
    router.push(hrefFor({ featureId: feature?.id }));
  }

  function handleDateChange(param: "from" | "to", value: string): void {
    router.push(hrefFor({ [param]: value || undefined }));
  }

  return (
    <section className="bg-card ring-foreground/10 space-y-4 rounded-xl p-3 ring-1">
      <div className="flex flex-wrap items-start gap-3">
        <div className="min-w-[12rem] flex-1 space-y-1.5">
          <Label htmlFor="changelog-feature">Feature</Label>
          <FeaturePicker
            id="changelog-feature"
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

        <div className="min-w-[12rem] flex-1 space-y-1.5">
          <Label htmlFor="changelog-author">Author</Label>
          <Select
            value={activeAuthorId ?? AllValue}
            onValueChange={(value) => handleSelectChange("authorId", value)}
          >
            <SelectTrigger id="changelog-author" className="w-full">
              <SelectValue>
                {activeAuthorId ? (
                  <>
                    <UserAvatar
                      userId={activeAuthorId}
                      name={
                        authors.find((a) => a.value === activeAuthorId)?.label ?? activeAuthorId
                      }
                      size="sm"
                      className="size-4 shrink-0"
                    />
                    <span className="truncate">
                      {authors.find((a) => a.value === activeAuthorId)?.label ?? activeAuthorId}
                    </span>
                  </>
                ) : (
                  "All authors"
                )}
              </SelectValue>
            </SelectTrigger>
            <SelectContent align="start">
              <SelectItem value={AllValue}>All authors</SelectItem>
              {authors.map((author) => (
                <SelectItem key={author.value} value={author.value}>
                  <UserAvatar
                    userId={author.value}
                    name={author.label}
                    size="default"
                    className="size-5"
                  />
                  {author.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="changelog-from">From</Label>
          <Input
            id="changelog-from"
            type="date"
            value={from}
            max={to || undefined}
            className="w-40"
            onChange={(event) => handleDateChange("from", event.target.value)}
          />
        </div>

        <div className="space-y-1.5">
          <Label htmlFor="changelog-to">To</Label>
          <Input
            id="changelog-to"
            type="date"
            value={to}
            min={from || undefined}
            className="w-40"
            onChange={(event) => handleDateChange("to", event.target.value)}
          />
        </div>

        {hasActiveState && (
          <Button
            variant="ghost"
            nativeButton={false}
            className="self-end"
            render={
              <Link
                href={hrefFor({
                  authorId: undefined,
                  changeTypes: undefined,
                  entityTypes: undefined,
                  featureId: undefined,
                  from: undefined,
                  to: undefined,
                })}
              >
                Clear all
              </Link>
            }
          />
        )}
      </div>

      <div className="border-border grid gap-x-8 gap-y-4 border-t pt-3 sm:grid-cols-2">
        <FilterGroup title="Type">
          <div className="flex flex-wrap gap-1.5">
            {ENTITY_KIND_OPTIONS.map((option) => (
              <FilterChip
                key={option.value}
                href={toggleValue("entityTypes", option.value, selectedEntityTypes)}
                selected={selectedEntityTypes.includes(option.value)}
              >
                {option.label}
              </FilterChip>
            ))}
          </div>
        </FilterGroup>

        <FilterGroup title="Change">
          <div className="flex flex-wrap gap-1.5">
            {CHANGE_TYPE_OPTIONS.map((option) => (
              <FilterChip
                key={option.value}
                href={toggleValue("changeTypes", option.value, selectedChangeTypes)}
                selected={selectedChangeTypes.includes(option.value)}
              >
                {option.label}
              </FilterChip>
            ))}
          </div>
        </FilterGroup>
      </div>
    </section>
  );
}

function FilterGroup({
  title,
  children,
}: {
  title: string;
  children: React.ReactNode;
}): React.JSX.Element {
  return (
    <div className="min-w-0 space-y-2">
      <p className="text-muted-foreground text-xs font-medium">{title}</p>
      {children}
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
