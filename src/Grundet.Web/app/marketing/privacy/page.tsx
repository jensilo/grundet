import { Logo } from "@/components/brand/logo";
import { MarketingFooter } from "@/components/marketing/marketing-footer";
import { MarketingNav } from "@/components/marketing/marketing-nav";

export default async function PrivacyPage(): Promise<React.JSX.Element> {
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
              Privacy Policy
            </h1>
          </header>

          <div className="text-foreground mt-10 space-y-8 text-[15px] leading-relaxed">
            <p>Effective date: 1 June 2026</p>

            <p>
              This Privacy Policy describes how Jens Heise (&ldquo;we&rdquo;, &ldquo;our&rdquo;, or
              &ldquo;us&rdquo;) collects, uses, and discloses your information when you use Grundet
              (&ldquo;Service&rdquo;).
            </p>

            <p>
              By using the Service, you accept and agree to this Privacy Policy. If you do not agree
              with our policies and practices, do not use the Service.
            </p>

            <h2 className="pt-6 text-2xl leading-[1.2] font-semibold tracking-tight">
              Information We Collect
            </h2>

            <p>
              We collect information you provide directly to us, such as when you create an account,
              request access, or contact us for support. This includes your name, email address, and
              any other information you choose to provide.
            </p>

            <p>
              We also collect limited technical information automatically, including your IP address
              and basic browser information, solely for the purpose of providing and securing the
              Service.
            </p>

            <h2 className="pt-6 text-2xl leading-[1.2] font-semibold tracking-tight">
              Legal Basis for Processing
            </h2>

            <p>
              We process your personal data based on contract performance (to provide the Service
              you requested) and legitimate interest (to improve and secure the Service).
            </p>

            <h2 className="pt-6 text-2xl leading-[1.2] font-semibold tracking-tight">
              How We Use Your Information
            </h2>

            <p>We use the information we collect to:</p>

            <ul className="list-inside list-disc space-y-2 pl-4">
              <li>Provide, maintain, and improve the Service;</li>
              <li>Respond to your comments, questions, and requests;</li>
              <li>Send you technical notices and support messages;</li>
              <li>Monitor and analyze usage to improve the Service;</li>
              <li>Detect and prevent fraudulent or illegal activities;</li>
              <li>Protect the rights and property of Grundet and others.</li>
            </ul>

            <h2 className="pt-6 text-2xl leading-[1.2] font-semibold tracking-tight">
              Data Storage and Transfers
            </h2>

            <p>
              Your data is primarily stored and processed within the European Union on
              infrastructure hosted by Hetzner. To provide and secure the Service, we use
              third-party infrastructure providers such as Cloudflare for DNS resolution, DDoS
              protection, and proxy services. These providers may process data, including IP
              addresses and request metadata, outside the EU. We ensure appropriate safeguards are
              in place for any international transfers.
            </p>

            <p>
              <strong>
                We do not use third-party analytics, tracking, or advertising services.
              </strong>
              We use a minimal number of service providers for hosting and infrastructure, who
              process data only on our behalf and under strict confidentiality obligations.
            </p>

            <h2 className="pt-6 text-2xl leading-[1.2] font-semibold tracking-tight">Cookies</h2>

            <p>
              We do not use cookies for tracking or analytics. We use essential cookies only for
              authentication and session management.
            </p>

            <h2 className="pt-6 text-2xl leading-[1.2] font-semibold tracking-tight">
              Sharing of Information
            </h2>

            <p>
              We do not share your personal information with third parties except as described in
              this Privacy Policy or with your consent.
            </p>

            <p>
              We may share information with service providers who perform services on our behalf,
              such as hosting, infrastructure security, and content delivery. These service
              providers have access to your information only to perform these tasks on our behalf
              and are obligated not to disclose or use it for any other purpose.
            </p>

            <p>
              We may also share information if we believe disclosure is reasonably necessary to:
              comply with any applicable law, regulation, legal process, or governmental request;
              enforce our agreements, policies, and Terms of Service; protect the security or
              integrity of our Service; or protect Grundet, our users, or others from harm.
            </p>

            <h2 className="pt-6 text-2xl leading-[1.2] font-semibold tracking-tight">
              Data Retention
            </h2>

            <p>
              We will retain your information for as long as your account is active or as needed to
              provide you with the Service. After account termination, we will delete your personal
              data within 30 days, unless required to retain it by law.
            </p>

            <h2 className="pt-6 text-2xl leading-[1.2] font-semibold tracking-tight">Security</h2>

            <p>
              We take reasonable technical and organizational measures to help protect your
              information from loss, theft, misuse, and unauthorized access, disclosure, alteration,
              and destruction. However, no internet transmission or electronic storage is 100%
              secure, and we cannot guarantee absolute security.
            </p>

            <h2 className="pt-6 text-2xl leading-[1.2] font-semibold tracking-tight">
              Your Rights
            </h2>

            <p>
              Under applicable data protection laws (including GDPR), you have the following rights
              regarding your personal data:
            </p>

            <ul className="list-inside list-disc space-y-2 pl-4">
              <li>
                <strong>Access:</strong> Request a copy of the personal data we hold about you;
              </li>
              <li>
                <strong>Rectification:</strong> Request correction of inaccurate or incomplete data;
              </li>
              <li>
                <strong>Erasure:</strong> Request deletion of your personal data, subject to legal
                requirements;
              </li>
              <li>
                <strong>Restriction:</strong> Request restriction of processing in certain
                circumstances;
              </li>
              <li>
                <strong>Object:</strong> Object to processing based on legitimate interest;
              </li>
              <li>
                <strong>Portability:</strong> Request transfer of your data to another service;
              </li>
              <li>
                <strong>Withdraw consent:</strong> Withdraw consent where processing is based on
                consent.
              </li>
            </ul>

            <p>
              To exercise any of these rights, please contact us at support@example.com. We will
              respond to your request within one month.
            </p>

            <p>
              During our alpha period, data erasure and export requests are handled manually. To
              request deletion of your account and personal data, or to export your data, please
              contact support@example.com. We will manually anonymize and remove your account
              information.
            </p>

            <h2 className="pt-6 text-2xl leading-[1.2] font-semibold tracking-tight">
              Children&rsquo;s Privacy
            </h2>

            <p>
              The Service is not intended for use by children under the age of 18. We do not
              knowingly collect personally identifiable information from children under 18. If we
              become aware that we have collected personal data from a child under age 18, we will
              delete that information as soon as possible.
            </p>

            <h2 className="pt-6 text-2xl leading-[1.2] font-semibold tracking-tight">
              Changes to This Privacy Policy
            </h2>

            <p>
              We may update this Privacy Policy from time to time. We will notify you of any changes
              by posting the new Privacy Policy on this page. You are advised to review this Privacy
              Policy periodically for any changes.
            </p>

            <h2 className="pt-6 text-2xl leading-[1.2] font-semibold tracking-tight">Contact Us</h2>

            <p>
              If you have any questions about this Privacy Policy, please contact us at
              support@example.com.
            </p>
          </div>
        </article>
      </main>

      <MarketingFooter showAccessCta={false} />
    </div>
  );
}
