import type { RequirementStatus } from "@/lib/api/generated/types.gen";
import { cn } from "@/lib/utils";

const statusStyles: Partial<Record<string, string>> = {
  Active: "bg-success-50 text-success-700 border-transparent",
  Draft: "bg-muted text-slate-700 border-transparent",
  Archived: "bg-slate-200 text-slate-600 border-transparent",
  WontDo: "bg-warning-50 text-warning-700 border-transparent",
  "Won't Do": "bg-warning-50 text-warning-700 border-transparent",
  Todo: "bg-muted text-slate-700 border-transparent",
  InProgress: "bg-accent text-accent-foreground border-transparent",
  Done: "bg-success-50 text-success-700 border-transparent",
};

const statusLabels: Partial<Record<string, string>> = {
  Active: "Active",
  Draft: "Draft",
  Archived: "Archived",
  WontDo: "Won't Do",
  "Won't Do": "Won't Do",
  Todo: "To do",
  InProgress: "In progress",
  Done: "Done",
};

export function StatusBadge({
  status,
  className,
}: {
  status: RequirementStatus | string;
  className?: string;
}) {
  return (
    <span
      className={cn(
        "inline-flex h-5 items-center rounded-sm border px-1.5 text-xs font-medium tracking-[0.02em]",
        statusStyles[status] ?? "bg-muted text-muted-foreground border-border",
        className
      )}
    >
      {statusLabels[status] ?? status}
    </span>
  );
}
