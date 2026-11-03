import { Logo } from "@/components/brand/logo";
import { MarketingFooter } from "@/components/marketing/marketing-footer";
import { MarketingNav } from "@/components/marketing/marketing-nav";

export default async function AboutPage(): Promise<React.JSX.Element> {
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
              About Grundet
            </h1>
            <p className="text-muted-foreground text-[15px] leading-relaxed">Built by Jens Heise</p>
          </header>

          <div className="text-foreground mt-10 space-y-8 text-[15px] leading-relaxed">
            <p>
              Grundet is a versioned intent layer. It keeps product truth durable, searchable, and
              connected to work – so teams can move fast without losing track of what matters and
              why.
            </p>

            <p>
              I&rsquo;ve been a backend engineer for years, and I&rsquo;ve seen the same failure
              modes repeat: misalignment, communication friction, wiki chaos, process tax. With AI
              making implementation cheaper, these problems don&rsquo;t go away – they get more
              expensive. The bottleneck shifts from &ldquo;can we build it?&rdquo; to &ldquo;should
              we build it?&rdquo;.
            </p>

            <p>
              So I&rsquo;m building Grundet as the tool I wish existed: a place for product intent
              that doesn&rsquo;t lie about its history.
            </p>

            <p>
              I am building Grundet as a serious independent software business: no VC, no hype, no
              fake urgency, no artificial lock-in, no rugpull dynamics, no AI slop. If Grundet ever
              gets shut down, the code will be open and the data will be exportable.
            </p>

            <p>
              You can read more about the thinking behind Grundet on&nbsp;
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
