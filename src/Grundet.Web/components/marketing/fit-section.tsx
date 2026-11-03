import { cn } from "@/lib/utils";

import { MARKETING_CONTAINER } from "./layout";

const goodFits = [
  "Product decisions outlive a single ticket, meeting, or Slack thread. You need intent that persists.",
  "Your team wants better coordination between developers, PMs, and stakeholders – without process weight.",
  "You value lightweight structure: labels, search, and history that humans and agents can both trust.",
];

const poorFits = [
  "You are building a throwaway prototype and do not want to preserve intent.",
  "Slack and Linear already give your team enough product truth, or your main problem is replacing Jira.",
  "You want a heavyweight requirements engineering or governance framework.",
];

export function FitSection() {
  return (
    <section id="fit" className="border-border bg-muted border-b">
      <div className={MARKETING_CONTAINER}>
        <div className="max-w-2xl">
          <p className="text-muted-foreground font-mono text-[11px] tracking-[0.18em] uppercase">
            Honest fit
          </p>
          <h2 className="mt-3 font-serif text-4xl leading-[1.05] tracking-tight text-slate-950 md:text-5xl 2xl:text-6xl">
            Useful when product truth matters.
          </h2>
          <p className="mt-6 text-[15px] leading-relaxed text-slate-600 2xl:text-base">
            Grundet is opinionated about preserving intent. That makes it valuable for some teams
            and unnecessary for others.
          </p>
        </div>

        <div className="mt-16 grid grid-cols-1 gap-10 md:grid-cols-2 md:gap-16 2xl:mt-20 2xl:gap-24">
          <FitColumn
            title="Grundet is for you when"
            items={goodFits}
            accentColor="border-indigo-200"
            dotColor="bg-indigo-500"
          />
          <FitColumn
            title="Grundet is maybe not for you when"
            items={poorFits}
            accentColor="border-slate-300"
            dotColor="bg-slate-400"
            offset
          />
        </div>
      </div>
    </section>
  );
}

function FitColumn({
  title,
  items,
  accentColor,
  dotColor,
  offset = false,
}: {
  title: string;
  items: string[];
  accentColor: string;
  dotColor: string;
  offset?: boolean;
}) {
  return (
    <div className={cn(offset && "md:mt-16")}>
      <div className="flex items-center gap-3">
        <span className={cn("size-2.5 rounded-full", dotColor)} />
        <h3 className="font-serif text-2xl leading-tight tracking-tight text-slate-950 2xl:text-3xl">
          {title}
        </h3>
      </div>
      <div className="mt-10 space-y-10">
        {items.map((item) => (
          <p
            key={item}
            className={cn(
              "pl-6 text-[1.0625rem] leading-relaxed text-slate-700 2xl:text-lg",
              "border-l-2",
              accentColor
            )}
          >
            {item}
          </p>
        ))}
      </div>
    </div>
  );
}
