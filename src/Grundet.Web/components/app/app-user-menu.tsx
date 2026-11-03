"use client";

import { LogOut, Settings, Users } from "lucide-react";
import { useRouter } from "next/navigation";

import { UserAvatar } from "@/components/app/user-avatar";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { logout } from "@/lib/api/generated/sdk.gen";

// Shared user menu — single source of truth for both the desktop sidebar (`AppSidebar`) and the
// mobile drawer (`MobileNavDrawer`). Wired to the same sign-out handler in both places.
export function AppUserMenu({
  user,
}: {
  user: { id: string; name: string; email: string };
}): React.JSX.Element {
  const router = useRouter();

  async function handleSignOut(): Promise<void> {
    try {
      await logout();
    } finally {
      router.push("/login");
    }
  }

  return (
    // Wrapper keeps this as a single child in the caller's `space-y-3` footer: Base UI injects
    // inline focus-guard spans next to the open trigger, and without this wrapper they land as
    // direct siblings there, redistributing the space-y margins and shifting neighbouring
    // elements.
    <div>
      <DropdownMenu>
        <DropdownMenuTrigger className="hover:bg-muted focus-visible:ring-ring/30 w-full rounded-md px-1 py-1.5 text-left transition-colors focus-visible:ring-2 focus-visible:outline-none">
          <div className="flex items-center gap-2.5">
            <UserAvatar userId={user.id} name={user.name} email={user.email} size="sm" />
            <div className="min-w-0">
              <div className="text-foreground truncate text-sm font-medium">{user.name}</div>
              <div className="text-muted-foreground truncate text-xs">{user.email}</div>
            </div>
          </div>
        </DropdownMenuTrigger>
        <DropdownMenuContent side="top" align="start" className="w-52">
          <DropdownMenuItem onClick={() => router.push("/profile")}>
            <Settings />
            Settings
          </DropdownMenuItem>
          <DropdownMenuItem onClick={() => router.push("/teams")}>
            <Users />
            Teams
          </DropdownMenuItem>
          <DropdownMenuSeparator />
          <DropdownMenuItem variant="destructive" onClick={handleSignOut}>
            <LogOut />
            Sign out
          </DropdownMenuItem>
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  );
}
