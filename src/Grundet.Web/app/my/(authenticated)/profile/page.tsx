import { notFound } from "next/navigation";

import { UserAvatar } from "@/components/app/user-avatar";
import { getMe } from "@/lib/api/generated/sdk.gen";
import { createServerClient } from "@/lib/api/server";

import { ProfileForm } from "./_components/profile-form";

// ---------------------------------------------------------------------------
// Page
// ---------------------------------------------------------------------------

export default async function ProfilePage(): Promise<React.JSX.Element> {
  const serverClient = await createServerClient();
  const { data, error } = await getMe({ client: serverClient });

  if (error || !data) {
    notFound();
  }

  return (
    <div className="mx-auto max-w-[1200px] space-y-6">
      <header className="flex items-center gap-4">
        <UserAvatar userId={data.id} name={data.displayName} email={data.email} size="lg" />
        <div className="space-y-1">
          <h1 className="text-foreground text-[2rem] leading-[1.2] font-semibold tracking-[-0.01em]">
            Profile
          </h1>
          <p className="text-muted-foreground text-xs">{data.email}</p>
        </div>
      </header>
      <ProfileForm displayName={data.displayName ?? ""} email={data.email} />
    </div>
  );
}
