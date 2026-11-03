import { type NextRequest, NextResponse } from "next/server";

export function proxy(request: NextRequest): NextResponse {
  const { pathname } = request.nextUrl;

  // Skip Next.js internals and the dev-only design sandbox.
  if (pathname.startsWith("/_next") || pathname === "/design" || pathname.startsWith("/design/")) {
    return NextResponse.next();
  }

  const host = request.headers.get("host") ?? "";
  // grundet.* (bare domain or www.) → marketing
  // my.*, localhost, 127.0.0.1, etc. → product app
  const isMarketing = /^(www\.)?grundet\./.test(host);
  const url = request.nextUrl.clone();

  if (isMarketing) {
    url.pathname = pathname === "/" ? "/marketing" : `/marketing${pathname}`;
    return NextResponse.rewrite(url);
  }

  // Product app: root → redirect to dashboard; everything else → /my/*
  if (pathname === "/") {
    url.pathname = "/dashboard";
    return NextResponse.redirect(url);
  }

  url.pathname = `/my${pathname}`;
  return NextResponse.rewrite(url);
}

export const config = {
  matcher: ["/((?!_next/static|_next/image|favicon\\.ico|sitemap\\.xml|robots\\.txt).*)"],
};
