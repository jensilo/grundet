import { ArrowRight } from "lucide-react";
import Link from "next/link";

import { Button } from "@/components/ui/button";

import { HeroDiffMock } from "./hero-diff-mock";

export function Hero({ dashboardUrl }: { dashboardUrl: string | null }) {
  return (
    <section className="relative overflow-hidden border-b border-slate-200">
      <div
        aria-hidden="true"
        className="pointer-events-none absolute inset-x-0 -top-32 h-[420px] bg-[radial-gradient(ellipse_at_top,_rgba(97,15,127,0.08),_transparent_60%),radial-gradient(ellipse_at_top_right,_rgba(255,105,120,0.05),_transparent_55%)]"
      />
      <div className="3xl:max-w-[96rem] 3xl:px-16 3xl:pt-48 3xl:pb-52 relative mx-auto grid max-w-6xl grid-cols-1 gap-16 px-6 pt-24 pb-28 sm:px-8 md:px-10 md:pt-32 md:pb-36 lg:min-h-[calc(100svh-3.5rem)] lg:grid-cols-[1.05fr_1fr] lg:content-center lg:items-center lg:gap-12 2xl:max-w-[88rem] 2xl:gap-20 2xl:px-12 2xl:pt-40 2xl:pb-44">
        <div>
          <div className="mb-8 inline-flex items-center gap-2 rounded-full border border-slate-200 bg-white px-3 py-1 font-mono text-[11px] tracking-[0.14em] text-slate-500 uppercase">
            <span className="size-1.5 rounded-full bg-indigo-500" />
            Closed alpha · Q4 2026
          </div>

          <h1 className="3xl:text-[5.5rem] font-serif text-5xl leading-[1.02] tracking-tight text-slate-950 md:text-6xl lg:text-7xl 2xl:text-[5rem]">
            A source of truth that{" "}
            <span className="to-bubblegum-500 inline-block bg-gradient-to-r from-indigo-500 bg-clip-text whitespace-nowrap text-transparent">
              doesn&rsquo;t lie
            </span>
            <br />
            about its history.
          </h1>

          <p className="mt-7 max-w-xl text-lg leading-relaxed text-slate-700 2xl:mt-9 2xl:text-xl">
            Grundet is where teams keep product intent. Every version, every reason, always
            searchable.
          </p>

          <div className="mt-10 flex flex-wrap items-center gap-4">
            <Button
              size="lg"
              nativeButton={false}
              render={
                dashboardUrl ? (
                  <a href={dashboardUrl}>Go to Dashboard</a>
                ) : (
                  <Link href="/join-alpha">Join the alpha</Link>
                )
              }
            />
            <Link
              href="#fit"
              className="group inline-flex items-center gap-1.5 text-sm font-medium text-slate-700 underline-offset-4 hover:text-slate-950 hover:underline"
            >
              Honest fit
              <ArrowRight className="size-3.5 transition-transform group-hover:translate-x-0.5" />
            </Link>
          </div>

          <div className="mt-12 flex flex-wrap items-center gap-x-6 gap-y-2 font-mono text-[11px] tracking-[0.14em] text-slate-400 uppercase">
            <span>Versioned intent</span>
            <span className="text-slate-300">·</span>
            <span>Agent-readable</span>
            <span className="text-slate-300">·</span>
            <span>Linked to work</span>
          </div>
        </div>

        <HeroDiffMock className="md:mx-auto md:max-w-xl lg:mx-0 lg:max-w-none lg:translate-y-2" />
      </div>
    </section>
  );
}
