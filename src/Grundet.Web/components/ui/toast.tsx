"use client";

import { Toast as ToastPrimitive } from "@base-ui/react/toast";
import { CheckCircle2, Info, type LucideIcon, TriangleAlert, X } from "lucide-react";

import { toastManager, type ToastType } from "@/lib/toast";
import { cn } from "@/lib/utils";

const TYPE_ICON: Record<ToastType, LucideIcon> = {
  success: CheckCircle2,
  error: TriangleAlert,
  info: Info,
};

const TYPE_ICON_COLOR: Record<ToastType, string> = {
  success: "text-success-500",
  error: "text-destructive",
  info: "text-muted-foreground",
};

function ToastList() {
  const { toasts } = ToastPrimitive.useToastManager();

  return toasts.map((item) => {
    const type: ToastType = (item.type as ToastType | undefined) ?? "info";
    const Icon = TYPE_ICON[type] ?? Info;

    return (
      <ToastPrimitive.Root
        key={item.id}
        toast={item}
        // Ruleset motion: slide in from translate-x-6, fade out on dismiss. Gated
        // behind motion-safe so reduced-motion users get an instant appear/remove.
        className={cn(
          "bg-popover border-border flex items-start gap-3 rounded-md border p-4 shadow-md",
          "motion-safe:transition motion-safe:duration-200 motion-safe:ease-out",
          "motion-safe:data-[starting-style]:translate-x-6 motion-safe:data-[starting-style]:opacity-0",
          "motion-safe:data-[ending-style]:opacity-0"
        )}
      >
        <Icon className={cn("mt-0.5 size-5 shrink-0", TYPE_ICON_COLOR[type])} aria-hidden />
        <div className="flex-1 space-y-0.5">
          <ToastPrimitive.Title className="text-foreground text-sm font-medium" />
          {item.description ? (
            <ToastPrimitive.Description className="text-muted-foreground text-sm" />
          ) : null}
        </div>
        <ToastPrimitive.Close
          aria-label="Dismiss"
          className="text-muted-foreground hover:text-foreground -m-1 rounded-md p-1 transition-colors"
        >
          <X className="size-4" />
        </ToastPrimitive.Close>
      </ToastPrimitive.Root>
    );
  });
}

/**
 * Toast viewport — mount once at the root layout. Reads from the shared global
 * `toastManager`, so any module can raise a toast without prop-drilling a context.
 */
export function Toaster() {
  return (
    <ToastPrimitive.Provider toastManager={toastManager} limit={3}>
      <ToastPrimitive.Portal>
        <ToastPrimitive.Viewport className="fixed top-0 right-0 z-[100] flex w-full max-w-[420px] flex-col gap-3 p-3 outline-none">
          <ToastList />
        </ToastPrimitive.Viewport>
      </ToastPrimitive.Portal>
    </ToastPrimitive.Provider>
  );
}
