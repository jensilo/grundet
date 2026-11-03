"use client";

import { usePathname, useRouter, useSearchParams } from "next/navigation";

export type FilterValues = Record<string, string>;

type FiltersApi<T extends FilterValues> = {
  get: (key: keyof T & string) => string | null;
  set: (key: keyof T & string, value: string) => void;
  setMany: (updates: Partial<Record<keyof T & string, string>>) => void;
  clear: (key: keyof T & string) => void;
  clearAll: () => void;
};

export function useFilters<T extends FilterValues>(): FiltersApi<T> {
  const params = useSearchParams();
  const router = useRouter();
  const pathname = usePathname();

  function get(key: keyof T & string): string | null {
    return params.get(key);
  }

  function set(key: keyof T & string, value: string): void {
    const next = new URLSearchParams(params.toString());
    next.set(key, value);
    router.replace(`${pathname}?${next.toString()}`);
  }

  function setMany(updates: Partial<Record<keyof T & string, string>>): void {
    const next = new URLSearchParams(params.toString());
    for (const [key, value] of Object.entries(updates)) {
      if (value !== undefined) next.set(key, value);
    }
    router.replace(`${pathname}?${next.toString()}`);
  }

  function clear(key: keyof T & string): void {
    const next = new URLSearchParams(params.toString());
    next.delete(key);
    const qs = next.toString();
    router.replace(qs ? `${pathname}?${qs}` : pathname);
  }

  function clearAll(): void {
    router.replace(pathname);
  }

  return { get, set, setMany, clear, clearAll };
}
