import { notFound } from "next/navigation";

import { SiteNav } from "@/components/site-nav";

export default function DesignLayout({ children }: { children: React.ReactNode }) {
  if (process.env.NODE_ENV === "production") {
    notFound();
  }
  return (
    <div className="flex min-h-screen flex-col">
      <SiteNav />
      <main className="container mx-auto flex-1 px-4 py-8">{children}</main>
    </div>
  );
}
