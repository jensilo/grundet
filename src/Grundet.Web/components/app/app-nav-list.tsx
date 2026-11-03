"use client";

import { CheckSquare, FolderOpen, History, House, Layers3, ListChecks } from "lucide-react";
import Link from "next/link";
import { usePathname } from "next/navigation";

import { cn } from "@/lib/utils";

// Shared primary nav definition — single source of truth for both the desktop sidebar
// (`AppSidebar`) and the mobile drawer (`MobileNavDrawer`). Project-scoped items are disabled
// (href: null) until a project is resolved.
export function AppNavList({
  currentProjectId,
  className,
}: {
  currentProjectId: string | null | undefined;
  className?: string;
}): React.JSX.Element {
  const pathname = usePathname();

  const navItems = [
    { label: "Home", href: "/dashboard", icon: House, exact: true },
    { label: "Projects", href: "/projects", icon: FolderOpen, exact: true },
    {
      label: "Features",
      href: currentProjectId ? `/projects/${currentProjectId}/features` : null,
      icon: Layers3,
      exact: false,
    },
    {
      label: "Intent",
      href: currentProjectId ? `/projects/${currentProjectId}/requirements` : null,
      icon: ListChecks,
      exact: false,
    },
    {
      label: "Work",
      href: currentProjectId ? `/projects/${currentProjectId}/work-items` : null,
      icon: CheckSquare,
      exact: false,
    },
    {
      label: "Changelog",
      href: currentProjectId ? `/projects/${currentProjectId}/changelog` : null,
      icon: History,
      exact: false,
    },
  ];

  return (
    <nav className={className}>
      <ul className="space-y-0.5">
        {navItems.map((item) => {
          const Icon = item.icon;

          if (item.href === null) {
            return (
              <li key={item.label}>
                <span className="text-muted-foreground flex cursor-not-allowed items-center gap-2 rounded-md px-2 py-1.5 text-sm opacity-40">
                  <Icon className="size-4" />
                  {item.label}
                </span>
              </li>
            );
          }

          const active = item.exact ? pathname === item.href : pathname.startsWith(item.href);

          return (
            <li key={item.label}>
              <Link
                href={item.href}
                className={cn(
                  "flex items-center gap-2 rounded-md px-2 py-1.5 text-sm transition-colors",
                  active
                    ? "bg-accent text-accent-foreground"
                    : "text-muted-foreground hover:bg-muted hover:text-foreground"
                )}
              >
                <Icon
                  className={cn(
                    "size-4",
                    active ? "text-accent-foreground" : "text-muted-foreground"
                  )}
                />
                {item.label}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
