"use client";

import { Loader2, XIcon } from "lucide-react";
import { useId, useRef, useState } from "react";

import { cn } from "@/lib/utils";

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export type PickerItem = {
  // Stable identifier; also used for de-duplication and exclusion.
  id: string;
  // Mono reference label shown first on chips and options (e.g. "IN-12", "WO-4").
  primary: string;
  // Human text shown after the reference (intent text / work title).
  secondary: string;
};

const DEBOUNCE_MS = 250;

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

/**
 * Multi-select, searchable token picker. The interaction shell — debounce, listbox keyboard
 * navigation, chips, and out-of-order-response guarding — lives here; concrete pickers supply a
 * `search` function over their own entity. Used by the intent and work link pickers.
 */
export function EntityMultiPicker({
  id,
  values,
  onChange,
  search,
  disabled,
  excludeIds = [],
  placeholder,
  emptyLabel = "No matches.",
}: {
  id: string;
  values: PickerItem[];
  onChange: (values: PickerItem[]) => void;
  search: (term: string) => Promise<PickerItem[]>;
  disabled?: boolean;
  excludeIds?: string[];
  placeholder?: string;
  emptyLabel?: string;
}): React.JSX.Element {
  const listboxId = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const debounceRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  // Guards against an out-of-order response overwriting newer results.
  const requestSeqRef = useRef(0);

  const [input, setInput] = useState("");
  const [results, setResults] = useState<PickerItem[]>([]);
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [activeIndex, setActiveIndex] = useState(-1);

  const excluded = new Set([...excludeIds, ...values.map((value) => value.id)]);
  const filtered = results.filter((result) => !excluded.has(result.id));
  const showPopover = open && (loading || filtered.length > 0);

  async function runSearch(term: string): Promise<void> {
    const seq = ++requestSeqRef.current;
    setLoading(true);
    const items = await search(term);
    if (seq !== requestSeqRef.current) return;
    setResults(items);
    setLoading(false);
  }

  function scheduleSearch(term: string): void {
    if (debounceRef.current) clearTimeout(debounceRef.current);
    debounceRef.current = setTimeout(() => void runSearch(term), DEBOUNCE_MS);
  }

  function addItem(item: PickerItem): void {
    if (!excluded.has(item.id)) {
      onChange([...values, item]);
    }
    setInput("");
    setActiveIndex(-1);
    setResults([]);
    inputRef.current?.focus();
  }

  function removeItem(itemId: string): void {
    onChange(values.filter((value) => value.id !== itemId));
    inputRef.current?.focus();
  }

  function handleKeyDown(event: React.KeyboardEvent<HTMLInputElement>): void {
    switch (event.key) {
      case "Enter": {
        if (showPopover && activeIndex >= 0 && filtered[activeIndex]) {
          event.preventDefault();
          addItem(filtered[activeIndex]);
        }
        break;
      }
      case "ArrowDown": {
        if (!showPopover || filtered.length === 0) return;
        event.preventDefault();
        setActiveIndex((index) => (index + 1) % filtered.length);
        break;
      }
      case "ArrowUp": {
        if (!showPopover || filtered.length === 0) return;
        event.preventDefault();
        setActiveIndex((index) => (index <= 0 ? filtered.length - 1 : index - 1));
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
      case "Backspace": {
        if (input === "" && values.length > 0) {
          event.preventDefault();
          removeItem(values[values.length - 1].id);
        }
        break;
      }
    }
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
      {values.length > 0 && (
        <ul className="mb-2 flex flex-wrap gap-1.5">
          {values.map((value) => (
            <li
              key={value.id}
              className="bg-muted text-foreground flex max-w-full items-center gap-1.5 rounded-sm py-1 pr-1 pl-2 text-xs"
            >
              <span className="text-primary shrink-0 font-mono font-medium">{value.primary}</span>
              <span className="text-muted-foreground min-w-0 truncate">{value.secondary}</span>
              <button
                type="button"
                disabled={disabled}
                aria-label={`Remove ${value.primary}`}
                className="text-muted-foreground hover:text-foreground shrink-0 rounded-sm"
                onClick={() => removeItem(value.id)}
              >
                <XIcon className="size-3" />
              </button>
            </li>
          ))}
        </ul>
      )}

      <div
        className={cn(
          "border-input focus-within:border-ring focus-within:ring-ring/50 flex h-8 w-full items-center rounded-lg border bg-transparent px-3 transition-colors focus-within:ring-3",
          disabled && "pointer-events-none opacity-50"
        )}
      >
        <input
          ref={inputRef}
          id={id}
          type="text"
          role="combobox"
          aria-expanded={showPopover}
          aria-controls={listboxId}
          aria-autocomplete="list"
          aria-activedescendant={
            showPopover && activeIndex >= 0 ? `${listboxId}-option-${activeIndex}` : undefined
          }
          autoComplete="off"
          disabled={disabled}
          value={input}
          placeholder={placeholder}
          className="text-foreground placeholder:text-muted-foreground h-full w-full bg-transparent text-sm outline-none"
          onFocus={() => {
            setOpen(true);
            if (results.length === 0 && !loading) void runSearch(input);
          }}
          onChange={(event) => {
            setInput(event.target.value);
            setOpen(true);
            setActiveIndex(-1);
            scheduleSearch(event.target.value);
          }}
          onKeyDown={handleKeyDown}
        />
        {loading && (
          <Loader2 className="text-muted-foreground size-3.5 shrink-0 animate-spin" aria-hidden />
        )}
      </div>

      {showPopover && (
        <ul
          id={listboxId}
          role="listbox"
          className="bg-popover ring-foreground/10 absolute z-50 mt-1 max-h-64 w-full overflow-y-auto rounded-lg p-1 text-sm shadow-md ring-1"
        >
          {filtered.length === 0 && !loading ? (
            <li className="text-muted-foreground px-2.5 py-1.5 text-xs">{emptyLabel}</li>
          ) : (
            filtered.map((result, index) => (
              <li
                key={result.id}
                id={`${listboxId}-option-${index}`}
                role="option"
                aria-selected={index === activeIndex}
                className={cn(
                  "flex cursor-default items-baseline gap-2 rounded-md px-2.5 py-1.5",
                  index === activeIndex ? "bg-accent text-accent-foreground" : "text-foreground"
                )}
                onMouseEnter={() => setActiveIndex(index)}
                onMouseDown={(event) => {
                  // Keep focus so the blur handler does not close the popover before the click.
                  event.preventDefault();
                  addItem(result);
                }}
              >
                <span className="text-primary shrink-0 font-mono text-xs font-medium">
                  {result.primary}
                </span>
                <span className="min-w-0 flex-1 truncate">{result.secondary}</span>
              </li>
            ))
          )}
        </ul>
      )}
    </div>
  );
}
