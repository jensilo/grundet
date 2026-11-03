import { Logo } from "@/components/brand/logo";
import { MarketingFooter } from "@/components/marketing/marketing-footer";
import { MarketingNav } from "@/components/marketing/marketing-nav";

export default async function ContactPage(): Promise<React.JSX.Element> {
  return (
    <div className="bg-background text-foreground">
      <MarketingNav dashboardUrl={null} />

      <main className="mx-auto max-w-3xl px-6 py-24 md:py-32">
        <article className="bg-muted ring-foreground/10 rounded-xl p-8 ring-1 md:p-12">
          <header className="space-y-4">
            <div className="flex items-center gap-2">
              <Logo size="sm" />
            </div>
            <h1 className="text-4xl leading-[1.05] font-semibold tracking-tight md:text-5xl">
              Contact
            </h1>
            <p className="text-muted-foreground text-[15px] leading-relaxed">
              Get in touch with the people behind Grundet
            </p>
          </header>

          <div className="text-foreground mt-10 space-y-8 text-[15px] leading-relaxed">
            <p>
              Have questions about Grundet? Want to share feedback, report an issue, or discuss your
              team&rsquo;s plans? Love to hear from you!
            </p>

            <div className="space-y-2">
              <p>For all inquiries, please send an email to:</p>
              <p>
                <a
                  href="mailto:support@example.com"
                  className="text-primary hover:text-primary/80 underline underline-offset-4"
                >
                  support@example.com
                </a>
              </p>
            </div>

            <p>
              You will receive a response ASAP; this can take 2 to 3 business days. For urgent
              matters, please include &ldquo;URGENT&rdquo; in your subject line.
            </p>

            <p>
              You can also find more about the thinking behind Grundet on&nbsp;
              <a
                href="https://jensheise.com"
                className="text-primary hover:text-primary/80 underline underline-offset-4"
              >
                jensheise.com
              </a>
              .
            </p>
          </div>
        </article>
      </main>

      <MarketingFooter showAccessCta={false} />
    </div>
  );
}
