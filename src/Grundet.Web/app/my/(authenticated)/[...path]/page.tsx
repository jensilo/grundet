import { notFound } from "next/navigation";

// Catch-all for unmatched paths on the app domain (my.grundet.*).
// The proxy rewrites e.g. /docs → /my/docs; if no page exists, Next.js
// would fall through to the global not-found. This forces it to the
// (authenticated) not-found instead, which renders within the app shell.
export default function CatchAll(): never {
  notFound();
}
