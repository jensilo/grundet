import { FileQuestion } from "lucide-react";
import Link from "next/link";

import { Button } from "@/components/ui/button";

export default function NotFound() {
  return (
    <div className="flex flex-col items-center justify-center py-16 text-center">
      <FileQuestion className="text-muted-foreground/40 mb-6 size-12" aria-hidden="true" />
      <h1 className="text-foreground mb-2 text-base font-semibold">Page not found</h1>
      <p className="text-muted-foreground mb-6 max-w-[380px] text-sm">
        This page doesn&rsquo;t exist, or you don&rsquo;t have access to it.
      </p>
      <Button nativeButton={false} render={<Link href="/dashboard">Back to dashboard</Link>} />
    </div>
  );
}
