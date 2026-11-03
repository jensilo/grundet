import { cookies } from "next/headers";

import { createClient, createConfig } from "@/lib/api/generated/client";
import type { Client } from "@/lib/api/generated/client/types.gen";

export async function createServerClient(): Promise<Client> {
  const cookieStore = await cookies();
  const cookieHeader = cookieStore
    .getAll()
    .map((c) => `${c.name}=${c.value}`)
    .join("; ");

  return createClient(
    createConfig({
      baseUrl: process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:8080",
      headers: {
        Cookie: cookieHeader,
      },
    })
  );
}
