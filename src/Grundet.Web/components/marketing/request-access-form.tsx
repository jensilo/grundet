"use client";

import { ArrowRight } from "lucide-react";
import { useState } from "react";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

export function RequestAccessForm() {
  const [email, setEmail] = useState("");

  function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const params = email.trim() ? `?email=${encodeURIComponent(email.trim())}` : "";
    window.location.href = `/join-alpha${params}`;
  }

  return (
    <form
      onSubmit={handleSubmit}
      className="flex w-full max-w-md flex-col gap-3 sm:flex-row sm:items-center"
    >
      <label htmlFor="email" className="sr-only">
        Work email
      </label>
      <Input
        id="email"
        type="email"
        required
        placeholder="you@company.com"
        value={email}
        onChange={(event) => setEmail(event.target.value)}
        className="h-10 flex-1 rounded-md border-slate-300 bg-white text-sm"
      />
      <Button size="lg" type="submit" className="h-10 px-4">
        Join alpha
        <ArrowRight className="size-3.5" />
      </Button>
    </form>
  );
}
