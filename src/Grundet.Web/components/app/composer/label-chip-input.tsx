"use client";

import { XIcon } from "lucide-react";
import { useId, useRef, useState } from "react";

import { getProjectLabels } from "@/lib/api/generated/sdk.gen";
import { TEXT_LIMITS } from "@/lib/text-limits";
import { cn } from "@/lib/utils";

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

type Suggestion = { value: string; count: number };

function fold(value: string): string {
  return value.trim().toLowerCase();
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

/**
 * Token/chip input for labels with a project-aware suggestion popover. Suggestions are
 * fetched once when the field is first focused and filtered client-side as the user types.
 * Free entry is always allowed — suggestions only speed up reuse of existing labels.
 */
export function LabelChipInput({
  id,
  values,
  onChange,
  projectId,
  disabled,
  placeholder = "Add a label and press Enter",
}: {
  id: string;
  values: string[];
  onChange: (values: string[]) => void;
  projectId: string;
  disabled?: boolean;
  placeholder?: string;
}): React.JSX.Element {
  const listboxId = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const loadedRef = useRef(false);

  const [input, setInput] = useState("");
  const [suggestions, setSuggestions] = useState<Suggestion[]>([]);
  const [open, setOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(-1);

  const selectedFolded = new Set(values.map(fold));
  const atMaxCount = values.length >= TEXT_LIMITS.labels.maxCount;

  const filtered = suggestions
    .filter((s) => !selectedFolded.has(fold(s.value)))
    .filter((s) => (input.trim() ? fold(s.value).includes(fold(input)) : true))
    .slice(0, 8);

  const showPopover = open && filtered.length > 0;

  async function loadSuggestions(): Promise<void> {
    if (loadedRef.current) return;
    loadedRef.current = true;
    const { data } = await getProjectLabels({ path: { projectId } });
    if (data) {
      // count comes through typed as number | string (OpenAPI int quirk) — coerce it.
      setSuggestions(
        data.labels.map((label) => ({ value: label.value, count: Number(label.count) }))
      );
    }
  }

  function addLabel(raw: string): void {
    const trimmed = raw.trim();
    if (!trimmed || selectedFolded.has(fold(trimmed)) || atMaxCount) {
      setInput("");
      setActiveIndex(-1);
      return;
    }
    onChange([...values, trimmed]);
    setInput("");
    setActiveIndex(-1);
  }

  function removeLabel(index: number): void {
    onChange(values.filter((_, i) => i !== index));
    inputRef.current?.focus();
  }

  function handleKeyDown(event: React.KeyboardEvent<HTMLInputElement>): void {
    switch (event.key) {
      case "Enter":
      case ",": {
        event.preventDefault();
        if (showPopover && activeIndex >= 0) {
          addLabel(filtered[activeIndex].value);
        } else {
          addLabel(input);
        }
        break;
      }
      case "ArrowDown": {
        if (!showPopover) return;
        event.preventDefault();
        setActiveIndex((index) => (index + 1) % filtered.length);
        break;
      }
      case "ArrowUp": {
        if (!showPopover) return;
        event.preventDefault();
        setActiveIndex((index) => (index <= 0 ? filtered.length - 1 : index - 1));
        break;
      }
      case "Escape": {
        if (open) {
          // Close the popover without letting Escape bubble up to close the dialog.
          event.stopPropagation();
          setOpen(false);
          setActiveIndex(-1);
        }
        break;
      }
      case "Backspace": {
        if (input === "" && values.length > 0) {
          event.preventDefault();
          removeLabel(values.length - 1);
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
      <div
        className={cn(
          "border-input focus-within:border-ring focus-within:ring-ring/50 flex min-h-8 w-full flex-wrap items-center gap-1.5 rounded-lg border bg-transparent px-2 py-1.5 transition-colors focus-within:ring-3",
          disabled && "pointer-events-none opacity-50"
        )}
        onClick={() => inputRef.current?.focus()}
      >
        {values.map((value, index) => (
          <span
            key={value}
            className="bg-muted text-foreground inline-flex items-center gap-1 rounded-sm py-0.5 pr-1 pl-2 text-xs font-medium"
          >
            {value}
            <button
              type="button"
              disabled={disabled}
              aria-label={`Remove ${value}`}
              className="text-muted-foreground hover:text-foreground rounded-sm"
              onClick={(event) => {
                event.stopPropagation();
                removeLabel(index);
              }}
            >
              <XIcon className="size-3" />
            </button>
          </span>
        ))}
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
          disabled={disabled || atMaxCount}
          value={input}
          maxLength={TEXT_LIMITS.labels.perLabelMaxLength}
          placeholder={
            atMaxCount
              ? `Limit reached (${TEXT_LIMITS.labels.maxCount} labels)`
              : values.length === 0
                ? placeholder
                : ""
          }
          className="text-foreground placeholder:text-muted-foreground h-5 min-w-[8rem] flex-1 bg-transparent text-sm outline-none"
          onFocus={() => {
            void loadSuggestions();
            setOpen(true);
          }}
          onChange={(event) => {
            setInput(event.target.value);
            setOpen(true);
            setActiveIndex(-1);
          }}
          onKeyDown={handleKeyDown}
        />
      </div>

      {showPopover && (
        <ul
          id={listboxId}
          role="listbox"
          className="bg-popover ring-foreground/10 absolute z-50 mt-1 max-h-56 w-full overflow-y-auto rounded-lg p-1 text-sm shadow-md ring-1"
        >
          {filtered.map((suggestion, index) => (
            <li
              key={suggestion.value}
              id={`${listboxId}-option-${index}`}
              role="option"
              aria-selected={index === activeIndex}
              className={cn(
                "flex cursor-default items-center justify-between gap-2 rounded-md px-2.5 py-1.5",
                index === activeIndex ? "bg-accent text-accent-foreground" : "text-foreground"
              )}
              onMouseEnter={() => setActiveIndex(index)}
              onMouseDown={(event) => {
                // Prevent the input blur from closing the popover before the click lands.
                event.preventDefault();
                addLabel(suggestion.value);
              }}
            >
              <span className="truncate">{suggestion.value}</span>
              <span className="text-muted-foreground shrink-0 font-mono text-xs tabular-nums">
                {suggestion.count}
              </span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
