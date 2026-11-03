"use client";

import { Dialog as DialogPrimitive } from "@base-ui/react/dialog";
import { Menu, X } from "lucide-react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useEffect, useState } from "react";

import { AppNavList } from "@/components/app/app-nav-list";
import { type ShellProject, type ShellTeam } from "@/components/app/app-shell-types";
import { AppUserMenu } from "@/components/app/app-user-menu";
import { FeedbackDialog } from "@/components/app/feedback-dialog";
import { ProjectSwitcher } from "@/components/app/project-switcher";
import { useCurrentProject } from "@/components/app/use-current-project";
import { Logo } from "@/components/brand/logo";
import { Button } from "@/components/ui/button";
import { useUser } from "@/providers/user-provider";

// Below the `md` breakpoint the sidebar (`AppSidebar`) is hidden, so this drawer is the only way
// to reach nav, the project switcher, and the user menu on a phone. It reuses the same
// `AppNavList` / `AppUserMenu` / `ProjectSwitcher` building blocks as the sidebar rather than
// duplicating them, and is built on the existing Base UI Dialog primitive (already a project
// dependency) rather than a new sheet/drawer package, styled here as a left-edge slide-in panel
// instead of the centered modal in `components/ui/dialog.tsx`. Base UI's Dialog gives us the
// accessibility primitives for free: focus is moved into the panel on open and restored on close,
// and it closes on Escape and on backdrop click.
export function MobileNavDrawer({
  projects,
  teams,
}: {
  projects: ShellProject[];
  teams: ShellTeam[];
}): React.JSX.Element {
  const [open, setOpen] = useState(false);
  const pathname = usePathname();
  const currentProject = useCurrentProject(projects);
  const user = useUser();

  // Close the drawer on route change so a nav tap doesn't leave it open over the new page.
  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setOpen(false);
  }, [pathname]);

  return (
    <DialogPrimitive.Root open={open} onOpenChange={setOpen}>
      <DialogPrimitive.Trigger
        aria-label="Open navigation menu"
        render={<Button variant="ghost" size="icon" className="md:hidden" />}
      >
        <Menu className="size-4" aria-hidden={true} />
      </DialogPrimitive.Trigger>
      <DialogPrimitive.Portal>
        <DialogPrimitive.Backdrop className="data-open:animate-in data-open:fade-in-0 data-closed:animate-out data-closed:fade-out-0 fixed inset-0 z-50 bg-black/10 duration-200 supports-backdrop-filter:backdrop-blur-xs md:hidden" />
        <DialogPrimitive.Popup
          aria-label="Navigation"
          className="bg-background border-border data-open:animate-in data-open:slide-in-from-left data-closed:animate-out data-closed:slide-out-to-left fixed inset-y-0 left-0 z-50 flex h-full w-72 max-w-[80vw] flex-col border-r shadow-lg duration-200 ease-out outline-none md:hidden"
        >
          <div className="border-border flex h-[52px] items-center justify-between border-b px-4">
            <Link href="/dashboard" className="flex items-center" aria-label="Grundet home">
              <Logo size="sm" />
            </Link>
            <DialogPrimitive.Close
              aria-label="Close navigation menu"
              render={<Button variant="ghost" size="icon-sm" />}
            >
              <X className="size-4" aria-hidden={true} />
            </DialogPrimitive.Close>
          </div>

          <div className="px-3 pt-3">
            <ProjectSwitcher currentProject={currentProject} teams={teams} />
          </div>

          <AppNavList currentProjectId={currentProject?.id} className="flex-1 px-2 py-3" />

          <div className="border-border space-y-3 border-t p-3">
            <FeedbackDialog />
            {user && <AppUserMenu user={user} />}
          </div>
        </DialogPrimitive.Popup>
      </DialogPrimitive.Portal>
    </DialogPrimitive.Root>
  );
}
