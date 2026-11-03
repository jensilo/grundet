"use client";

import { ChevronDownIcon, ChevronUpIcon, PlusIcon, XIcon } from "lucide-react";
import { useState } from "react";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { TEXT_LIMITS } from "@/lib/text-limits";

type Row = { id: string; text: string };

// Monotonic counter for stable row keys — never collides within a session.
let rowIdCounter = 0;
function nextRowId(): string {
  return `criterion-${rowIdCounter++}`;
}

function toRows(values: string[]): Row[] {
  return values.map((text) => ({ id: nextRowId(), text }));
}

function sameTexts(rows: Row[], values: string[]): boolean {
  return rows.length === values.length && rows.every((row, index) => row.text === values[index]);
}

/**
 * Structured editor for acceptance criteria. Criteria are discrete rows — added with Enter,
 * editable in place, removable, and reorderable with up/down (no drag dependency). The value
 * round-trips as a plain string[]. Rows carry stable ids internally so reordering keeps input
 * identity and focus stable.
 */
export function AcceptanceCriteriaEditor({
  id,
  values,
  onChange,
  disabled,
}: {
  id: string;
  values: string[];
  onChange: (values: string[]) => void;
  disabled?: boolean;
}): React.JSX.Element {
  const [rows, setRows] = useState<Row[]>(() => toRows(values));
  const [draft, setDraft] = useState("");

  // Adjust internal rows during render when the controlled value changes from the outside (draft
  // restore, discard) rather than from our own edits — regenerating ids only when content differs.
  // This is React's sanctioned pattern for deriving state from props without an effect.
  const [prevValues, setPrevValues] = useState(values);
  if (values !== prevValues) {
    setPrevValues(values);
    if (!sameTexts(rows, values)) setRows(toRows(values));
  }

  function emit(next: Row[]): void {
    setRows(next);
    onChange(next.map((row) => row.text));
  }

  function addCriterion(): void {
    const trimmed = draft.trim();
    if (!trimmed) return;
    emit([...rows, { id: nextRowId(), text: trimmed }]);
    setDraft("");
  }

  function updateCriterion(rowId: string, next: string): void {
    emit(rows.map((row) => (row.id === rowId ? { ...row, text: next } : row)));
  }

  function removeCriterion(rowId: string): void {
    emit(rows.filter((row) => row.id !== rowId));
  }

  function move(index: number, direction: -1 | 1): void {
    const target = index + direction;
    if (target < 0 || target >= rows.length) return;
    const next = [...rows];
    [next[index], next[target]] = [next[target], next[index]];
    emit(next);
  }

  return (
    <div className="space-y-2">
      {rows.length > 0 && (
        <ul className="space-y-2">
          {rows.map((row, index) => (
            <li key={row.id} className="flex items-center gap-1.5">
              <span className="text-muted-foreground w-4 shrink-0 text-right font-mono text-xs select-none">
                {index + 1}
              </span>
              <Input
                aria-label={`Acceptance criterion ${index + 1}`}
                value={row.text}
                disabled={disabled}
                maxLength={TEXT_LIMITS.acceptanceCriterion}
                onChange={(event) => updateCriterion(row.id, event.target.value)}
              />
              <Button
                type="button"
                variant="ghost"
                size="icon-sm"
                aria-label={`Move criterion ${index + 1} up`}
                disabled={disabled || index === 0}
                onClick={() => move(index, -1)}
              >
                <ChevronUpIcon />
              </Button>
              <Button
                type="button"
                variant="ghost"
                size="icon-sm"
                aria-label={`Move criterion ${index + 1} down`}
                disabled={disabled || index === rows.length - 1}
                onClick={() => move(index, 1)}
              >
                <ChevronDownIcon />
              </Button>
              <Button
                type="button"
                variant="ghost"
                size="icon-sm"
                aria-label={`Remove criterion ${index + 1}`}
                disabled={disabled}
                onClick={() => removeCriterion(row.id)}
              >
                <XIcon />
              </Button>
            </li>
          ))}
        </ul>
      )}

      <div className="flex items-center gap-1.5">
        <span className="w-4 shrink-0" aria-hidden />
        <Input
          id={id}
          value={draft}
          disabled={disabled}
          maxLength={TEXT_LIMITS.acceptanceCriterion}
          placeholder="Add a criterion and press Enter"
          onChange={(event) => setDraft(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === "Enter") {
              event.preventDefault();
              addCriterion();
            }
          }}
        />
        <Button
          type="button"
          variant="ghost"
          size="icon-sm"
          aria-label="Add criterion"
          disabled={disabled || !draft.trim()}
          onClick={addCriterion}
        >
          <PlusIcon />
        </Button>
      </div>
    </div>
  );
}
