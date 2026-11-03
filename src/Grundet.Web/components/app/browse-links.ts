export type BrowseSearchParams = Record<string, string | string[] | undefined>;

export function getSearchValues(
  searchParams: BrowseSearchParams,
  key: string,
  allowed?: readonly string[]
): string[] {
  const raw = searchParams[key];
  const values = (Array.isArray(raw) ? raw : raw ? [raw] : [])
    .flatMap((value) => value.split(","))
    .map((value) => value.trim())
    .filter(Boolean);

  return allowed ? values.filter((value) => allowed.includes(value)) : values;
}

export function getSearchValue(searchParams: BrowseSearchParams, key: string): string {
  const raw = searchParams[key];
  return typeof raw === "string" ? raw.trim() : "";
}

// Builds a fresh, single-label browse filter href from a detail page. Unlike `buildBrowseHref`,
// it carries no existing params — clicking a label always lands on a clean, single-label filter.
// `basePath` must be root-relative (leading slash) so Next resolves it against the app root, not
// the current detail route. `URLSearchParams` handles encoding of spaces, `#`, `&`, `/`.
export function buildLabelFilterHref(basePath: string, label: string): string {
  const query = new URLSearchParams({ labels: label }).toString();
  return `${basePath}?${query}`;
}

export function buildBrowseHref(
  pathname: string,
  searchParams: BrowseSearchParams,
  updates: Record<string, string | string[] | undefined>
): string {
  const params = new URLSearchParams();

  for (const [key, raw] of Object.entries(searchParams)) {
    if (key === "page") {
      continue;
    }

    const values = Array.isArray(raw) ? raw : raw ? [raw] : [];
    for (const value of values) {
      if (value) {
        params.append(key, value);
      }
    }
  }

  for (const [key, value] of Object.entries(updates)) {
    params.delete(key);
    if (Array.isArray(value)) {
      for (const item of value) {
        params.append(key, item);
      }
    } else if (value) {
      params.set(key, value);
    }
  }

  const queryString = params.toString();
  return queryString ? `${pathname}?${queryString}` : pathname;
}
