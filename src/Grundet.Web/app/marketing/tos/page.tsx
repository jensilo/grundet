import { Logo } from "@/components/brand/logo";
import { MarketingFooter } from "@/components/marketing/marketing-footer";
import { MarketingNav } from "@/components/marketing/marketing-nav";

export default async function TosPage(): Promise<React.JSX.Element> {
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
              Terms of Service
            </h1>
          </header>

          <div className="text-foreground mt-10 space-y-8 text-[15px] leading-relaxed">
            <p>Effective date: 1 June 2026</p>

            <p>
              These Terms of Service (&ldquo;Terms&rdquo;) govern your access to and use of Grundet
              (&ldquo;Service&rdquo;), a product intent management tool provided by Jens Heise
              (&ldquo;we&rdquo;, &ldquo;our&rdquo;, or &ldquo;us&rdquo;).
            </p>

            <p>
              By accessing or using the Service, you agree to be bound by these Terms and our
              Privacy Policy. If you do not agree to these Terms, do not access or use the Service.
            </p>

            <h2 className="pt-6 text-2xl leading-[1.2] font-semibold tracking-tight">
              Acceptance of Terms
            </h2>

            <p>
              You must be at least 18 years old to use the Service. By using the Service, you
              represent and warrant that you are of legal age to form a binding contract with us and
              meet all of the foregoing eligibility requirements.
            </p>

            <h2 className="pt-6 text-2xl leading-[1.2] font-semibold tracking-tight">
              Description of Service
            </h2>

            <p>
              Grundet provides teams with a versioned intent layer for managing product
              requirements, decisions, and work items. The Service allows you to create, edit,
              version, link, and search product intent documentation.
            </p>

            <p>
              We use third-party infrastructure providers to deliver and secure the Service. These
              providers process data only as necessary to provide their services and are bound by
              confidentiality and data protection obligations.
            </p>

            <h2 className="pt-6 text-2xl leading-[1.2] font-semibold tracking-tight">
              Your Responsibilities
            </h2>

            <p>
              You are responsible for all activity that occurs under your account. You agree to use
              the Service only for lawful purposes and in accordance with these Terms.
            </p>

            <p>
              You are solely responsible for the content you create, upload, or transmit through the
              Service. We do not claim ownership of your content. You retain all intellectual
              property rights to your content. By posting content, you grant us a limited,
              worldwide, non-exclusive, royalty-free license to host, store, process, and display
              your content solely for the purpose of providing, maintaining, and improving the
              Service.
            </p>

            <h2 className="pt-6 text-2xl leading-[1.2] font-semibold tracking-tight">
              Prohibited Conduct
            </h2>

            <p>
              You agree not to engage in any conduct that violates these Terms, including but not
              limited to: violating any applicable laws or regulations; infringing any intellectual
              property rights; transmitting any material that is unlawful, harmful, threatening,
              abusive, harassing, defamatory, vulgar, obscene, or otherwise objectionable; or
              attempting to gain unauthorized access to any part of the Service.
            </p>

            <h2 className="pt-6 text-2xl leading-[1.2] font-semibold tracking-tight">
              Termination
            </h2>

            <p>
              We may terminate or suspend your account and bar access to the Service immediately,
              without prior notice or liability, for any reason, including but not limited to a
              breach of these Terms. We will provide reasonable notice where possible, except in
              cases of material breach or legal requirement.
            </p>

            <p>
              You may terminate your account at any time by contacting us at support@example.com.
            </p>

            <h2 className="pt-6 text-2xl leading-[1.2] font-semibold tracking-tight">
              Data Ownership and Retention
            </h2>

            <p>
              You retain full ownership of all data and content you submit to the Service. We do not
              claim any ownership rights to your content.
            </p>

            <p>
              We will retain your information for as long as your account is active or as needed to
              provide you with the Service. After account termination, we will delete your data
              within 30 days, unless required to retain it by law.
            </p>

            <h2 className="pt-6 text-2xl leading-[1.2] font-semibold tracking-tight">
              Indemnification
            </h2>

            <p>
              You agree to indemnify, defend, and hold harmless Jens Heise and our affiliates,
              directors, agents, and employees from and against any and all claims, liabilities,
              damages, losses, costs, expenses, or fees (including reasonable attorneys&rsquo; fees)
              arising from or related to your use of the Service, your content, or your violation of
              these Terms.
            </p>

            <h2 className="pt-6 text-2xl leading-[1.2] font-semibold tracking-tight">
              Disclaimer of Warranties
            </h2>

            <p>
              The Service is provided on an &ldquo;AS IS&rdquo; and &ldquo;AS AVAILABLE&rdquo;
              basis. We disclaim all warranties of any kind, whether express or implied, including
              but not limited to the implied warranties of merchantability, fitness for a particular
              purpose, and non-infringement.
            </p>

            <h2 className="pt-6 text-2xl leading-[1.2] font-semibold tracking-tight">
              Limitation of Liability
            </h2>

            <p>
              In no event shall we, our directors, employees, partners, agents, suppliers, or
              affiliates, be liable for any indirect, incidental, special, consequential, or
              punitive damages, including without limitation, loss of profits, data, use, goodwill,
              or other intangible losses, resulting from your access to or use of or inability to
              access or use the Service.
            </p>

            <p>
              To the maximum extent permitted by law, our aggregate liability for any claims arising
              from or related to these Terms or the Service shall not exceed the greater of one
              hundred (100) EUR or the fees you paid to us in the twelve (12) months preceding the
              event giving rise to the claim.
            </p>

            <h2 className="pt-6 text-2xl leading-[1.2] font-semibold tracking-tight">
              Force Majeure
            </h2>

            <p>
              We shall not be liable for any failure to perform our obligations under these Terms
              where such failure results from events beyond our reasonable control, including but
              not limited to natural disasters, acts of God, war, civil unrest, government actions,
              internet outages, or failures of third-party services.
            </p>

            <h2 className="pt-6 text-2xl leading-[1.2] font-semibold tracking-tight">Assignment</h2>

            <p>
              We may assign or transfer these Terms and our rights and obligations hereunder without
              your consent. You may not assign or transfer these Terms or your rights hereunder
              without our prior written consent.
            </p>

            <h2 className="pt-6 text-2xl leading-[1.2] font-semibold tracking-tight">
              Governing Law and Jurisdiction
            </h2>

            <p>
              These Terms shall be governed and construed in accordance with the laws of Sweden,
              without regard to its conflict of law provisions. Any disputes arising from or related
              to these Terms or the Service shall be subject to the exclusive jurisdiction of the
              courts of Gothenburg, Sweden.
            </p>

            <h2 className="pt-6 text-2xl leading-[1.2] font-semibold tracking-tight">
              Severability and Entire Agreement
            </h2>

            <p>
              If any provision of these Terms is held to be invalid or unenforceable by a court, the
              remaining provisions of these Terms will remain in effect. These Terms, together with
              our Privacy Policy, constitute the entire agreement between you and us regarding the
              Service.
            </p>

            <h2 className="pt-6 text-2xl leading-[1.2] font-semibold tracking-tight">
              Changes to Terms
            </h2>

            <p>
              We reserve the right, at our sole discretion, to modify or replace these Terms at any
              time. If a revision is material, we will provide at least 30 days&rsquo; notice prior
              to any new terms taking effect. What constitutes a material change will be determined
              at our sole discretion.
            </p>

            <p>
              By continuing to access or use our Service after those revisions become effective, you
              agree to be bound by the revised terms. If you do not agree to the new terms, you must
              stop using the Service.
            </p>

            <h2 className="pt-6 text-2xl leading-[1.2] font-semibold tracking-tight">Contact Us</h2>

            <p>
              If you have any questions about these Terms, please contact us at support@example.com.
            </p>
          </div>
        </article>
      </main>

      <MarketingFooter showAccessCta={false} />
    </div>
  );
}
