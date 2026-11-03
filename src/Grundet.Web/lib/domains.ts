/**
 * Builds an absolute URL on the app subdomain (my.*) from the current host.
 *
 * Works across all environments without configuration:
 *   grundet.test:3000  →  http://my.grundet.test:3000{path}
 *   grundet.app        →  https://my.grundet.app{path}
 *   www.grundet.app    →  https://my.grundet.app{path}
 *
 * The host argument should come from the `host` request header (includes port).
 * The path must start with "/".
 */
export function buildAppUrl(host: string, path: string): string {
  const protocol = host.includes(".test") ? "http" : "https";
  const appHost = host.replace(/^(www\.)?grundet\./, "my.grundet.");
  return `${protocol}://${appHost}${path}`;
}

/**
 * Builds an absolute URL on the marketing domain from the current host.
 *
 * Works across all environments without configuration:
 *   my.grundet.test:3000 → http://grundet.test:3000{path}
 *   my.grundet.app       → https://grundet.app{path}
 */
export function buildMarketingUrl(host: string, path: string): string {
  const protocol = host.includes(".test") || host.startsWith("localhost") ? "http" : "https";
  const marketingHost = host.replace(/^my\.grundet\./, "grundet.");
  return `${protocol}://${marketingHost}${path}`;
}
