import Link from "next/link";

import { Logo } from "@/components/brand/logo";

import { MARKETING_CONTAINER } from "./layout";
import { RequestAccessForm } from "./request-access-form";

export function MarketingFooter({ showAccessCta = true }: { showAccessCta?: boolean }) {
  return (
    <footer id="access">
      {showAccessCta && (
        <div className="border-border bg-background border-b">
          <div className={MARKETING_CONTAINER}>
            <div className="grid grid-cols-1 gap-12 lg:grid-cols-[1.1fr_1fr] lg:items-end lg:gap-16 2xl:gap-24">
              <div>
                <p className="text-primary font-mono text-[11px] tracking-[0.18em] uppercase">
                  Private alpha
                </p>
                <h2 className="mt-4 font-serif text-4xl leading-[1.05] tracking-tight text-slate-950 md:text-6xl 2xl:text-7xl">
                  Give your team a
                  <br />
                  place for product
                  <br />
                  truth.
                </h2>
              </div>

              <div>
                <p className="text-[15px] leading-relaxed text-slate-600 2xl:text-base">
                  We&rsquo;re onboarding small cohorts while the product is still young. Tell us
                  your work email and continue to the alpha request form.
                </p>
                <div className="mt-6">
                  <RequestAccessForm />
                </div>
                <p className="mt-3 font-mono text-[11px] text-slate-400">No marketing list.</p>
              </div>
            </div>
          </div>
        </div>
      )}

      <div className="bg-slate-50">
        <div className="3xl:max-w-[96rem] 3xl:px-16 mx-auto flex max-w-6xl flex-col items-start justify-between gap-6 px-6 py-8 sm:flex-row sm:items-center 2xl:max-w-[88rem] 2xl:px-12">
          <div className="flex items-center gap-4">
            <Logo size="sm" />
            <span className="font-mono text-[11px] text-slate-400">© 2026 Grundet</span>
          </div>
          <nav className="flex items-center gap-6 text-sm text-slate-600">
            <Link href="/about" className="hover:text-slate-950">
              About
            </Link>
            <Link href="/contact" className="hover:text-slate-950">
              Contact
            </Link>
            <Link href="/privacy" className="hover:text-slate-950">
              Privacy
            </Link>
            <Link href="/tos" className="hover:text-slate-950">
              Terms
            </Link>
          </nav>
        </div>
      </div>
    </footer>
  );
}
