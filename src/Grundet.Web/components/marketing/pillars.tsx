import { cn } from "@/lib/utils";

import { MARKETING_CONTAINER } from "./layout";

const pillars = [
  {
    dot: "bg-espresso-500",
    eyebrow: "Product truth",
    title: "Every version has a memory.",
    body: "Intent changes. Grundet preserves what changed, who changed it, and why, so the current truth never erases the path that got you there.",
  },
  {
    dot: "bg-cool-sky-500",
    eyebrow: "Shared context",
    title: "Readable where work happens.",
    body: "Anyone on the team can shape intent in the UI. Developers and agents can bring the same trusted context into their development environment instead of relying on stale docs, prompt fragments, or guesswork.",
  },
  {
    dot: "bg-emerald-500",
    eyebrow: "Execution",
    title: "Work stays connected to intent.",
    body: "Work coordinates ownership and status around intent. It helps teams deliver without turning Grundet into a backlog machine or process tool.",
  },
];

export function Pillars() {
  return (
    <section id="product" className="border-border bg-background border-b">
      <div className={MARKETING_CONTAINER}>
        <div className="max-w-2xl">
          <p className="text-muted-foreground font-mono text-[11px] tracking-[0.18em] uppercase">
            What it does
          </p>
          <h2 className="mt-3 font-serif text-4xl leading-[1.05] tracking-tight text-slate-950 md:text-5xl 2xl:text-6xl">
            The foundation.
          </h2>
        </div>

        <div className="mt-16 grid grid-cols-1 gap-12 md:grid-cols-3 md:gap-10 2xl:mt-20 2xl:gap-14">
          {pillars.map((p) => (
            <div key={p.title} className="flex flex-col">
              <span className={cn("mb-6 size-2.5 rounded-full", p.dot)} />
              <p className="text-muted-foreground font-mono text-[11px] tracking-[0.18em] uppercase">
                {p.eyebrow}
              </p>
              <h3 className="mt-3 font-serif text-2xl leading-tight tracking-tight text-slate-950 2xl:text-3xl">
                {p.title}
              </h3>
              <p className="mt-4 text-[15px] leading-relaxed text-slate-600 2xl:text-base">
                {p.body}
              </p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
