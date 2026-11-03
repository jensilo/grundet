import { cn } from "@/lib/utils";

import { StrataMark } from "./strata-mark";
import { Wordmark } from "./wordmark";

interface LogoProps {
  className?: string;
  size?: "sm" | "md" | "lg";
  markOnly?: boolean;
}

const markSize = { sm: 16, md: 20, lg: 28 };

export function Logo({ className, size = "md", markOnly = false }: LogoProps) {
  if (markOnly) {
    return <StrataMark size={markSize[size]} className={cn("text-foreground", className)} />;
  }

  return (
    <div className={cn("flex items-center gap-2", className)}>
      <StrataMark size={markSize[size]} className="text-foreground" />
      <Wordmark size={size} />
    </div>
  );
}
