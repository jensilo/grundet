import type { LucideIcon } from "lucide-react";
import type * as React from "react";

export function EmptyState({
  icon: Icon,
  title,
  description,
  action,
  children,
}: {
  icon?: LucideIcon;
  title: string;
  description?: string;
  action?: React.ReactNode;
  children?: React.ReactNode;
}): React.JSX.Element {
  const content = children ?? action;

  return (
    <div className="flex flex-col items-center justify-center px-6 py-16 text-center">
      {Icon && <Icon className="text-muted-foreground/40 mb-4 size-12" aria-hidden={true} />}
      <p className="text-foreground mb-2 text-base font-semibold">{title}</p>
      {description && <p className="text-muted-foreground max-w-[380px] text-sm">{description}</p>}
      {content ? (
        <div className="mt-6 flex flex-wrap items-center justify-center gap-2">{content}</div>
      ) : null}
    </div>
  );
}
