export default function LoadingChangelogPage(): React.JSX.Element {
  return (
    <div className="mx-auto max-w-[1080px] space-y-6" aria-label="Loading changelog">
      <div className="space-y-2">
        <div className="bg-muted h-4 w-28 animate-pulse rounded-sm" />
        <div className="bg-muted h-8 w-48 animate-pulse rounded-sm" />
        <div className="bg-muted h-4 w-full max-w-[480px] animate-pulse rounded-sm" />
      </div>
      <div className="bg-muted h-32 animate-pulse rounded-xl" />
      <div className="space-y-3">
        <div className="bg-muted h-14 animate-pulse rounded-lg" />
        <div className="bg-muted h-14 animate-pulse rounded-lg" />
        <div className="bg-muted h-14 animate-pulse rounded-lg" />
      </div>
    </div>
  );
}
