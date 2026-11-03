"use client";

import { client } from "@/lib/api/generated/client.gen";

client.setConfig({
  baseUrl: process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:8080",
  credentials: "include",
});

export function ApiConfigProvider({ children }: { children: React.ReactNode }): React.JSX.Element {
  return <>{children}</>;
}
