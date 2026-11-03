"use client";

import { Toast } from "@base-ui/react/toast";

// One global manager, shared by the <Toaster/> mounted in the root layout and by
// every caller of `toast(...)`. Living outside React's render tree means a toast
// raised right before a router.refresh() or navigation still appears and persists
// across it — the manager survives, the viewport re-subscribes.
export const toastManager = Toast.createToastManager();

export type ToastType = "success" | "error" | "info";

type ToastOptions = {
  title: string;
  description?: string;
  type?: ToastType;
  timeout?: number;
};

export function toast(options: ToastOptions): string {
  const type = options.type ?? "info";
  return toastManager.add({
    title: options.title,
    description: options.description,
    type,
    // Ruleset auto-dismiss: 4s for success/info, 8s for error.
    timeout: options.timeout ?? (type === "error" ? 8000 : 4000),
    priority: type === "error" ? "high" : "low",
  });
}
