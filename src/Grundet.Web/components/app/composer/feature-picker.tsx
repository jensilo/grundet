"use client";

import { ChevronDownIcon, Loader2 } from "lucide-react";
import { useId, useRef, useState } from "react";

import { listFeatures } from "@/lib/api/generated/sdk.gen";
import { cn } from "@/lib/utils";

export type FeatureOption = {
  id: string;
  name: string;
};

const DEBOUNCE_MS = 250;
const RESULT_LIMIT = "8";
// Sentinel id for the optional "clear" entry (e.g. "All features"); selecting it emits null.
const CLEAR_ID = "__clear__";

/**
 * Single-select, searchable feature picker. Replaces the eager dropdown that loaded every feature
 * up front: the current selection is supplied by the caller and matches are fetched on demand from
 * the project features endpoint (server-side `q`). Used by the intent composer (where a feature is
 * always selected) and the browse filter panel (where `clearLabel` adds an "All features" entry and
 * the value may be null).
 */
export function FeaturePicker({
  id,
  projectId,
  value,
  onChange,
  disabled,
  clearLabel,
}: {
  id: string;
  projectId: string;
  value: FeatureOption | null;
  onChange: (value: FeatureOption | null) => void;
  disabled?: boolean;
  // When set, a pinned entry with this label clears the selection (emits null) and is shown as the
  // trigger label while nothing is selected.
  clearLabel?: string;
}): React.JSX.Element {
  const listboxId = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  // Guards against an out-of-order response overwriting newer results.
  const requestSeqRef = useRef(0);

  const [open, setOpen] = useState(false);
  const [input, setInput] = useState("");
  const [results, setResults] = useState<FeatureOption[]>([]);
  const [loading, setLoading] = useState(false);
  const [activeIndex, setActiveIndex] = useState(-1);

  // The clear entry (when present) is pinned above the fetched results and not subject to search.
  const entries: FeatureOption[] =
    clearLabel !== undefined ? [{ id: CLEAR_ID, name: clearLabel }, ...results] : results;
  const showPopover = open && (loading || entries.length > 0);
  const triggerLabel = value ? value.name : (clearLabel ?? "");

  async function runSearch(term: string): Promise<void> {
    const seq = ++requestSeqRef.current;
    setLoading(true);
    const { data } = await listFeatures({
      path: { projectId },
      query: { q: term.trim() || undefined, pageSize: RESULT_LIMIT },
    });
    if (seq !== requestSeqRef.current) return;
    setResults((data?.items ?? []).map((feature) => ({ id: feature.id, name: feature.name })));
    setLoading(false);
  }

  function scheduleSearch(term: string): void {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => void runSearch(term), DEBOUNCE_MS);
  }

  function openAndLoad(): void {
    setOpen(true);
    setActiveIndex(-1);
    if (results.length === 0 && !loading) void runSearch("");
    // Defer focus until the search input is mounted.
    requestAnimationFrame(() => inputRef.current?.focus());
  }

  function select(option: FeatureOption): void {
    onChange(option.id === CLEAR_ID ? null : option);
    setOpen(false);
    setInput("");
    setResults([]);
    setActiveIndex(-1);
  }

  function handleKeyDown(event: React.KeyboardEvent<HTMLInputElement>): void {
    switch (event.key) {
      case "Enter": {
        if (showPopover && activeIndex >= 0 && entries[activeIndex]) {
          event.preventDefault();
          select(entries[activeIndex]);
        }
        break;
      }
      case "ArrowDown": {
        if (!showPopover || entries.length === 0) return;
        event.preventDefault();
        setActiveIndex((index) => (index + 1) % entries.length);
        break;
      }
      case "ArrowUp": {
        if (!showPopover || entries.length === 0) return;
        event.preventDefault();
        setActiveIndex((index) => (index <= 0 ? entries.length - 1 : index - 1));
        break;
      }
      case "Escape": {
        if (open) {
          // Swallow Escape so it closes the popover, not the surrounding dialog.
          event.stopPropagation();
          setOpen(false);
          setActiveIndex(-1);
        }
        break;
      }
    }
  }

  function isEntrySelected(entry: FeatureOption): boolean {
    return entry.id === CLEAR_ID ? value === null : value?.id === entry.id;
  }

  return (
    <div
      className="relative"
      onBlur={(event) => {
        if (!event.currentTarget.contains(event.relatedTarget as Node | null)) {
          setOpen(false);
          setActiveIndex(-1);
        }
      }}
    >
      <button
        type="button"
        id={id}
        disabled={disabled}
        aria-expanded={open}
        aria-haspopup="listbox"
        className={cn(
          "border-input focus-visible:border-ring focus-visible:ring-ring/50 flex h-8 w-full items-center justify-between gap-1.5 rounded-lg border bg-transparent py-2 pr-2 pl-2.5 text-left text-sm transition-colors outline-none focus-visible:ring-3 disabled:cursor-not-allowed disabled:opacity-50",
          open && "hidden"
        )}
        onClick={openAndLoad}
      >
        <span className="line-clamp-1">{triggerLabel}</span>
        <ChevronDownIcon className="text-muted-foreground pointer-events-none size-4 shrink-0" />
      </button>

      {open && (
        <div className="border-input focus-within:border-ring focus-within:ring-ring/50 flex h-8 w-full items-center rounded-lg border bg-transparent px-3 transition-colors focus-within:ring-3">
          <input
            ref={inputRef}
            type="text"
            role="combobox"
            aria-expanded={showPopover}
            aria-controls={listboxId}
            aria-autocomplete="list"
            aria-activedescendant={
              showPopover && activeIndex >= 0 ? `${listboxId}-option-${activeIndex}` : undefined
            }
            autoComplete="off"
            value={input}
            placeholder="Search features…"
            className="text-foreground placeholder:text-muted-foreground h-full w-full bg-transparent text-sm outline-none"
            onChange={(event) => {
              setInput(event.target.value);
              setActiveIndex(-1);
              scheduleSearch(event.target.value);
            }}
            onKeyDown={handleKeyDown}
          />
          {loading && (
            <Loader2 className="text-muted-foreground size-3.5 shrink-0 animate-spin" aria-hidden />
          )}
        </div>
      )}

      {showPopover && (
        <ul
          id={listboxId}
          role="listbox"
          className="bg-popover ring-foreground/10 absolute z-50 mt-1 max-h-64 w-full overflow-y-auto rounded-lg p-1 text-sm shadow-md ring-1"
        >
          {entries.length === 0 && !loading ? (
            <li className="text-muted-foreground px-2.5 py-1.5 text-xs">No matching features.</li>
          ) : (
            entries.map((entry, index) => (
              <li
                key={entry.id}
                id={`${listboxId}-option-${index}`}
                role="option"
                aria-selected={isEntrySelected(entry)}
                className={cn(
                  "flex cursor-default items-center gap-2 rounded-md px-2.5 py-1.5",
                  index === activeIndex ? "bg-accent text-accent-foreground" : "text-foreground",
                  entry.id === CLEAR_ID && "text-muted-foreground"
                )}
                onMouseEnter={() => setActiveIndex(index)}
                onMouseDown={(event) => {
                  // Keep focus so the blur handler does not close the popover before the click.
                  event.preventDefault();
                  select(entry);
                }}
              >
                <span className="min-w-0 flex-1 truncate">{entry.name}</span>
              </li>
            ))
          )}
        </ul>
      )}
    </div>
  );
}
