"use client";

import { Info } from "lucide-react";

import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";
import { cn } from "@/lib/utils";

// Small standalone info affordance: a muted (i) that reveals a short explanation on
// hover/focus. Used to surface concepts (e.g. snapshots) at the point they appear.
export function InfoHint({
  label,
  children,
  className,
}: {
  label: string;
  children: React.ReactNode;
  className?: string;
}): React.JSX.Element {
  return (
    <TooltipProvider>
      <Tooltip>
        <TooltipTrigger
          aria-label={label}
          className={cn(
            "text-muted-foreground hover:text-foreground focus-visible:ring-ring/30 inline-flex items-center justify-center rounded-sm focus-visible:ring-2 focus-visible:outline-none",
            className
          )}
        >
          <Info className="size-3.5" />
        </TooltipTrigger>
        <TooltipContent className="max-w-xs leading-relaxed text-pretty">{children}</TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}
