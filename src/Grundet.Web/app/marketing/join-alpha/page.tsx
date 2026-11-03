import { ArrowRight, CheckCircle2 } from "lucide-react";
import { headers } from "next/headers";

import { AlphaWaitlistForm } from "@/components/marketing/alpha-waitlist-form";
import { MarketingFooter } from "@/components/marketing/marketing-footer";
import { MarketingNav } from "@/components/marketing/marketing-nav";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { buildAppUrl } from "@/lib/domains";

export default async function JoinAlphaPage({
  searchParams,
}: {
  searchParams: Promise<{ email?: string }>;
}) {
  const headerStore = await headers();
  const host = headerStore.get("host") ?? "grundet.app";
  const loginUrl = buildAppUrl(host, "/login");
  const { email } = await searchParams;
  return (
    <div className="bg-background text-foreground min-h-screen">
      <MarketingNav dashboardUrl={null} />
      <main>
        <section className="mx-auto grid max-w-6xl gap-16 px-6 py-24 lg:grid-cols-[0.9fr_1fr] lg:items-start">
          <div className="space-y-10 lg:pt-12">
            <div className="space-y-6">
              <p className="text-primary text-xs font-medium tracking-[0.04em] uppercase">
                Private alpha
              </p>
              <h1 className="text-5xl leading-[1.1] font-semibold tracking-[-0.02em] text-slate-950">
                Help shape the versioned intent layer.
              </h1>
              <p className="text-muted-foreground max-w-xl text-lg leading-relaxed">
                Grundet is in a focused alpha for teams that feel the cost of scattered product
                truth. Tell us what you are building, where intent gets lost, and whether the early
                product can help.
              </p>
            </div>

            <Card>
              <CardHeader>
                <CardTitle className="text-sm">What happens after you request access</CardTitle>
              </CardHeader>
              <CardContent>
                <div className="grid gap-3 text-sm">
                  {[
                    "We review requests manually and prioritize real product intent pain.",
                    "Approved users get account creation access and magic-link sign-in.",
                    "If it is not a fit yet, we will still use the signal to shape the alpha.",
                  ].map((item) => (
                    <div key={item} className="flex gap-3">
                      <CheckCircle2
                        className="text-primary mt-0.5 size-4 shrink-0"
                        aria-hidden="true"
                      />
                      <p className="text-muted-foreground">{item}</p>
                    </div>
                  ))}
                </div>
              </CardContent>
            </Card>

            <a
              href={loginUrl}
              className="text-muted-foreground hover:text-foreground inline-flex items-center gap-1.5 text-sm underline-offset-4 hover:underline"
            >
              Already have access? Sign in with your email
              <ArrowRight className="size-3.5" aria-hidden="true" />
            </a>
          </div>

          <AlphaWaitlistForm initialEmail={email} />
        </section>
      </main>
      <MarketingFooter showAccessCta={false} />
    </div>
  );
}
