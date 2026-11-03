import Link from "next/link";

import { Logo } from "@/components/brand/logo";
import { Separator } from "@/components/ui/separator";

const navItems = [
  { label: "Dashboard", href: "/", disabled: true },
  { label: "Projects", href: "/projects", disabled: true },
  ...(process.env.NODE_ENV !== "production"
    ? [{ label: "Design System", href: "/design", disabled: false }]
    : []),
];

export function SiteNav() {
  return (
    <header className="bg-background sticky top-0 z-40 border-b">
      <div className="container mx-auto flex h-14 items-center gap-6 px-4">
        <Link href="/" className="flex items-center gap-2" aria-label="Grundet home">
          <Logo size="md" />
        </Link>

        <Separator orientation="vertical" className="h-5" />

        <nav className="flex items-center gap-4">
          {navItems.map((item) =>
            item.disabled ? (
              <span
                key={item.href}
                className="text-muted-foreground cursor-not-allowed text-sm"
                title="Not yet implemented"
              >
                {item.label}
              </span>
            ) : (
              <Link
                key={item.href}
                href={item.href}
                className="text-foreground hover:text-foreground/80 text-sm font-medium transition-colors"
              >
                {item.label}
              </Link>
            )
          )}
        </nav>
      </div>
    </header>
  );
}
