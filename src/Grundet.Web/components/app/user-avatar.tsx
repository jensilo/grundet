"use client";

import BoringAvatar from "boring-avatars";

import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { AVATAR_PALETTE } from "@/lib/avatar-palette";
import { getInitials } from "@/lib/user";

type UserAvatarProps = {
  /** Stable, non-PII seed (the user's GUID). When present, drives the deterministic identicon. */
  userId?: string | null;
  name?: string | null;
  email?: string | null;
  size?: "sm" | "default" | "lg";
  className?: string;
};

/**
 * Deterministic, seeded user avatar. With a `userId` it renders a boring-avatars `beam` identicon
 * (inline SVG, no network, SSR-safe) seeded from the GUID. The `beam` mask uses React `useId`, which
 * is hydration-stable, so first paint is correct under SSR. The initials block is only a fallback
 * for the rare case where no seed is available.
 *
 * The identicon is a direct child of `Avatar` (Root) rather than `AvatarImage`: `AvatarImage` tracks
 * an `<img>` load event that an inline SVG never fires, which would fall through to the fallback.
 */
export function UserAvatar({
  userId,
  name,
  email,
  size = "default",
  className,
}: UserAvatarProps): React.JSX.Element {
  return (
    <Avatar size={size} className={className}>
      {userId ? (
        <BoringAvatar
          name={userId}
          colors={AVATAR_PALETTE}
          variant="beam"
          size="100%"
          className="size-full rounded-full"
          aria-hidden="true"
        />
      ) : (
        <AvatarFallback>{getInitials(name, email)}</AvatarFallback>
      )}
    </Avatar>
  );
}
