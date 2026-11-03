import Link from "next/link";

import { Logo } from "@/components/brand/logo";
import { Button } from "@/components/ui/button";

const links = [
  { label: "Product", href: "/#product" },
  { label: "Fit", href: "/#fit" },
  { label: "Changelog", href: "/#changelog" },
  { label: "Pricing", href: "/#pricing" },
  { label: "About", href: "/about" },
  { label: "Contact", href: "/contact" },
];

export function MarketingNav({ dashboardUrl }: { dashboardUrl: string | null }) {
  return (
    <header className="sticky top-0 z-50 border-b border-slate-200 bg-slate-50/80 backdrop-blur-md">
      <div className="3xl:max-w-[96rem] 3xl:px-16 mx-auto flex h-14 max-w-6xl items-center justify-between px-6 2xl:h-16 2xl:max-w-[88rem] 2xl:px-12">
        <Link href="/" aria-label="Grundet home" className="flex items-center">
          <Logo size="md" />
        </Link>
        <nav className="hidden items-center gap-8 md:flex">
          {links.map((l) => (
            <Link
              key={l.href}
              href={l.href}
              className="text-sm text-slate-600 transition-colors hover:text-slate-950"
            >
              {l.label}
            </Link>
          ))}
        </nav>
        <Button
          size="sm"
          nativeButton={false}
          render={
            dashboardUrl ? (
              <a href={dashboardUrl}>Go to Dashboard</a>
            ) : (
              <Link href="/join-alpha">Join the alpha</Link>
            )
          }
        />
      </div>
    </header>
  );
}
