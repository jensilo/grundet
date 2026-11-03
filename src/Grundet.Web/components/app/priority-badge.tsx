import type { RequirementPriority } from "@/lib/api/generated/types.gen";
import { cn } from "@/lib/utils";

const priorityStyles: Partial<Record<string, string>> = {
  Must: "bg-accent text-accent-foreground border-transparent",
  Should: "bg-cool-sky-50 text-cool-sky-700 border-transparent",
  Could: "bg-muted text-slate-700 border-transparent",
  Wont: "bg-background text-slate-500 border-border",
  "Won't": "bg-background text-slate-500 border-border",
};

const priorityLabels: Partial<Record<string, string>> = {
  Must: "Must",
  Should: "Should",
  Could: "Could",
  Wont: "Won't",
  "Won't": "Won't",
};

export function PriorityBadge({
  priority,
  className,
}: {
  priority: RequirementPriority | string;
  className?: string;
}) {
  return (
    <span
      className={cn(
        "inline-flex h-5 items-center rounded-sm border px-1.5 text-xs font-medium tracking-[0.02em]",
        priorityStyles[priority] ?? "bg-muted text-muted-foreground border-border",
        className
      )}
    >
      {priorityLabels[priority] ?? priority}
    </span>
  );
}
