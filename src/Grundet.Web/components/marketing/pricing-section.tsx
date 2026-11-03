import { MARKETING_CONTAINER } from "./layout";

export function PricingSection() {
  return (
    <section id="pricing" className="border-border bg-background border-b">
      <div className={MARKETING_CONTAINER}>
        <div className="bg-muted ring-foreground/10 border-t-primary rounded-xl border-t-2 p-8 ring-1 md:p-12 2xl:p-16">
          <div className="grid grid-cols-1 gap-10 lg:grid-cols-[1fr_1.3fr] lg:gap-16 2xl:gap-20">
            <div>
              <p className="text-muted-foreground font-mono text-[11px] tracking-[0.18em] uppercase">
                Pricing
              </p>
              <h2 className="mt-3 font-serif text-4xl leading-[1.05] tracking-tight text-slate-950 md:text-5xl 2xl:text-6xl">
                No paid plans during alpha.
              </h2>
            </div>

            <div className="space-y-5 text-[15px] leading-relaxed text-slate-600 2xl:text-base">
              <p>
                Pricing will be decided after the alpha, once the product has proven the right shape
                for real teams. The goal is honest, sustainable pricing – not artificial locks or
                enterprise games.
              </p>
              <p>
                Grundet should be accessible to every serious software team. Solo developers with
                manageable needs will always have a free tier.
              </p>
              <p className="font-mono text-[11px] tracking-[0.14em] text-slate-400 uppercase">
                Free alpha · fair post-alpha plans · always-free solo tier
              </p>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}
