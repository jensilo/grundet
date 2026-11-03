"use client";

import { PlusIcon } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { cn } from "@/lib/utils";

// ---------------------------------------------------------------------------
// Field wrapper
// ---------------------------------------------------------------------------

export function ComposerField({
  htmlFor,
  label,
  required,
  helper,
  error,
  children,
}: {
  htmlFor: string;
  label: string;
  required?: boolean;
  helper?: React.ReactNode;
  error?: string | null;
  children: React.ReactNode;
}): React.JSX.Element {
  return (
    <div className="space-y-1.5">
      <Label htmlFor={htmlFor} className="text-foreground block text-sm font-medium">
        {label}
        {required && <span className="text-primary ml-0.5">*</span>}
      </Label>
      {children}
      {helper && <p className="text-muted-foreground text-xs">{helper}</p>}
      {error && <p className="text-destructive text-xs">{error}</p>}
    </div>
  );
}

// ---------------------------------------------------------------------------
// Select
// ---------------------------------------------------------------------------

export function ComposerSelect<T extends string>({
  id,
  value,
  options,
  disabled,
  onValueChange,
}: {
  id: string;
  value: T;
  options: readonly { value: T; label: string }[];
  disabled?: boolean;
  onValueChange: (value: T) => void;
}): React.JSX.Element {
  const active = options.find((option) => option.value === value);
  return (
    <Select value={value} onValueChange={(next) => onValueChange(next as T)} disabled={disabled}>
      <SelectTrigger id={id} className="w-full">
        <SelectValue>{active?.label ?? value}</SelectValue>
      </SelectTrigger>
      <SelectContent align="start">
        {options.map((option) => (
          <SelectItem key={option.value} value={option.value}>
            {option.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

// ---------------------------------------------------------------------------
// Adder row — quiet progressive-disclosure control for optional fields
// ---------------------------------------------------------------------------

export type AdderField<K extends string> = { key: K; label: string };

export function AdderRow<K extends string>({
  fields,
  onReveal,
  disabled,
  className,
}: {
  fields: readonly AdderField<K>[];
  onReveal: (key: K) => void;
  disabled?: boolean;
  className?: string;
}): React.JSX.Element | null {
  if (fields.length === 0) return null;

  return (
    <div className={cn("flex flex-wrap items-center gap-x-2 gap-y-1.5", className)}>
      <span className="text-muted-foreground text-xs">Add</span>
      {fields.map((field) => (
        <Button
          key={field.key}
          type="button"
          variant="ghost"
          size="xs"
          disabled={disabled}
          className="text-muted-foreground hover:text-foreground gap-1 px-2"
          onClick={() => onReveal(field.key)}
        >
          <PlusIcon className="size-3" />
          {field.label}
        </Button>
      ))}
    </div>
  );
}
