import Link from "next/link";

import { StatusBadge } from "@/components/app/status-badge";
import { Logo } from "@/components/brand/logo";
import { Button } from "@/components/ui/button";

export default function NotFound() {
  return (
    <div className="min-h-screen bg-slate-50 text-slate-950">
      <header className="sticky top-0 z-50 border-b border-slate-200 bg-slate-50/80 backdrop-blur-md">
        <div className="mx-auto flex h-14 max-w-6xl items-center px-6">
          <Link href="/" aria-label="Grundet home">
            <Logo size="md" />
          </Link>
        </div>
      </header>

      <main className="mx-auto max-w-[720px] px-6 py-32 text-center">
        {/* Eyebrow pill — matches hero pill pattern */}
        <div className="mb-8 inline-flex items-center gap-2 rounded-full border border-slate-200 bg-white px-3 py-1 font-mono text-[11px] tracking-[0.14em] text-slate-500 uppercase">
          <span className="size-1.5 rounded-full bg-indigo-500" />
          IN-404
        </div>

        <h1 className="font-serif text-5xl leading-[1.05] tracking-tight text-slate-950">
          Requirement not found.
        </h1>

        <p className="mx-auto mt-6 max-w-[520px] text-lg leading-relaxed text-slate-600">
          This page was closed as Won&rsquo;t Do by an unknown stakeholder. No spec was written. No
          acceptance criteria were defined. We&rsquo;re as surprised as you are.
        </p>

        {/* Mock requirement card — styled as a product artifact */}
        <div className="bg-card ring-foreground/10 mt-12 overflow-hidden rounded-xl text-left ring-1">
          <div className="border-border flex items-center gap-2.5 border-b px-4 py-3">
            <span className="text-muted-foreground font-mono text-xs">IN-404</span>
            <StatusBadge status="WontDo" />
            <span className="text-muted-foreground ml-auto font-mono text-[11px]">
              v2 · just now
            </span>
          </div>

          <div className="px-4 py-3">
            <p className="text-foreground text-[0.9375rem] leading-relaxed">
              The page at the requested URL SHALL be accessible to the user.
            </p>
          </div>

          <div className="border-border space-y-3 border-t px-4 py-3">
            <p className="text-muted-foreground text-xs font-medium tracking-[0.04em] uppercase">
              Version history
            </p>

            <div className="flex gap-3">
              <span className="text-muted-foreground shrink-0 pt-px font-mono text-xs">v1</span>
              <div className="space-y-0.5">
                <p className="text-muted-foreground text-xs">Created · System · 4 years ago</p>
                <p className="text-muted-foreground text-xs italic">
                  &ldquo;All pages shall be accessible to users. This is obvious.&rdquo;
                </p>
              </div>
            </div>

            <div className="flex gap-3">
              <span className="text-muted-foreground shrink-0 pt-px font-mono text-xs">v2</span>
              <div className="space-y-0.5">
                <p className="text-muted-foreground text-xs">
                  Won&rsquo;t Do · Unknown Stakeholder · just now
                </p>
                <p className="text-muted-foreground text-xs italic">
                  &ldquo;URL not in scope. Nobody will find this. Close and forget.&rdquo;
                </p>
              </div>
            </div>
          </div>
        </div>

        <div className="mt-10">
          <Button size="lg" nativeButton={false} render={<Link href="/">Back to home</Link>} />
        </div>
      </main>
    </div>
  );
}
