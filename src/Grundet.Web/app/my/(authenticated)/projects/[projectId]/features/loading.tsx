export default function LoadingFeaturesPage(): React.JSX.Element {
  return (
    <div className="mx-auto max-w-[1080px] space-y-6" aria-label="Loading features">
      <div className="space-y-4">
        <div className="bg-muted h-4 w-64 animate-pulse rounded-sm" />
        <div className="space-y-2">
          <div className="bg-muted h-8 w-40 animate-pulse rounded-sm" />
          <div className="bg-muted h-4 w-full max-w-[560px] animate-pulse rounded-sm" />
        </div>
      </div>
      <div className="space-y-3">
        <div className="bg-muted h-24 animate-pulse rounded-lg" />
        <div className="bg-muted h-24 animate-pulse rounded-lg" />
      </div>
    </div>
  );
}
