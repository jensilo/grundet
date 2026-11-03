"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

export function SearchForm({
  projectId,
  defaultValue,
}: {
  projectId: string;
  defaultValue: string;
}): React.JSX.Element {
  const router = useRouter();
  const [query, setQuery] = useState(defaultValue);

  function handleSubmit(e: React.FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const trimmed = query.trim();
    if (!trimmed) return;
    router.push(`/projects/${projectId}/search?q=${encodeURIComponent(trimmed)}`);
  }

  return (
    <form onSubmit={handleSubmit} className="flex items-center gap-2">
      <Input
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        placeholder="Search intent, work, features…"
        className="max-w-[480px]"
        autoFocus={!defaultValue}
      />
      <Button type="submit" size="sm" disabled={!query.trim()}>
        Search
      </Button>
    </form>
  );
}
