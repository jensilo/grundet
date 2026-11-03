/**
 * Two-letter initials for an avatar fallback: the first letters of the first two name words,
 * otherwise the first two characters of the email. Shared by the app shell and `UserAvatar`.
 */
export function getInitials(
  name: string | null | undefined,
  email: string | null | undefined
): string {
  const fromName = (name ?? "")
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? "")
    .join("");

  return fromName || (email ?? "").slice(0, 2).toUpperCase();
}
