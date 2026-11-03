import { cn } from "@/lib/utils";

interface WordmarkProps {
  className?: string;
  size?: "sm" | "md" | "lg";
}

const sizeClasses = {
  sm: "text-[14px]",
  md: "text-[18px]",
  lg: "text-[24px]",
};

export function Wordmark({ className, size = "md" }: WordmarkProps) {
  return (
    <span
      className={cn(
        "text-foreground font-sans font-medium tracking-[-0.02em]",
        sizeClasses[size],
        className
      )}
    >
      grundet
    </span>
  );
}
