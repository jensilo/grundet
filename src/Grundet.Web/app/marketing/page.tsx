import { headers } from "next/headers";

import { ChangelogShowcase } from "@/components/marketing/changelog-showcase";
import { FitSection } from "@/components/marketing/fit-section";
import { Hero } from "@/components/marketing/hero";
import { MarketingFooter } from "@/components/marketing/marketing-footer";
import { MarketingNav } from "@/components/marketing/marketing-nav";
import { MoveFast } from "@/components/marketing/move-fast";
import { NotList } from "@/components/marketing/not-list";
import { Pillars } from "@/components/marketing/pillars";
import { PricingSection } from "@/components/marketing/pricing-section";
import { getMe } from "@/lib/api/generated/sdk.gen";
import { createServerClient } from "@/lib/api/server";
import { buildAppUrl } from "@/lib/domains";

export default async function HomePage(): Promise<React.JSX.Element> {
  const serverClient = await createServerClient();
  const { data } = await getMe({ client: serverClient });

  let dashboardUrl: string | null = null;
  if (data) {
    const headerStore = await headers();
    const host = headerStore.get("host") ?? "";
    dashboardUrl = buildAppUrl(host, "/dashboard");
  }

  return (
    <div className="bg-slate-50 text-slate-950">
      <MarketingNav dashboardUrl={dashboardUrl} />
      <Hero dashboardUrl={dashboardUrl} />
      <NotList />
      <MoveFast />
      <Pillars />
      <FitSection />
      <ChangelogShowcase />
      <PricingSection />
      <MarketingFooter />
    </div>
  );
}
