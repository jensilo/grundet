import { cn } from "@/lib/utils";

interface StrataMarkProps {
  size?: number;
  className?: string;
}

export function StrataMark({ size = 24, className }: StrataMarkProps) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 64 64"
      fill="none"
      xmlns="http://www.w3.org/2000/svg"
      className={cn("text-foreground", className)}
      aria-hidden="true"
    >
      <rect x="8" y="14" width="40" height="8" fill="currentColor" />
      <rect x="8" y="28" width="56" height="8" fill="currentColor" />
      <rect x="8" y="42" width="32" height="8" fill="currentColor" />
    </svg>
  );
}
