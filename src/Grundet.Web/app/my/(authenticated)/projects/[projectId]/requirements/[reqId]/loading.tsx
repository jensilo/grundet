export default function LoadingRequirementDetailPage(): React.JSX.Element {
  return (
    <div className="mx-auto max-w-[1200px] space-y-6" aria-label="Loading intent">
      <div className="flex items-center justify-between gap-4">
        <div className="bg-muted h-4 w-16 animate-pulse rounded-sm" />
        <div className="bg-muted h-9 w-24 animate-pulse rounded-md" />
      </div>

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-3">
        <div className="space-y-6 lg:col-span-2">
          <div className="space-y-4">
            <div className="flex items-center gap-2">
              <div className="bg-muted h-4 w-16 animate-pulse rounded-sm" />
              <div className="bg-muted h-5 w-16 animate-pulse rounded-sm" />
              <div className="bg-muted h-5 w-16 animate-pulse rounded-sm" />
            </div>
            <div className="bg-muted h-7 w-full animate-pulse rounded-sm" />
            <div className="bg-muted h-7 w-3/4 animate-pulse rounded-sm" />
          </div>
          <div className="bg-muted h-40 animate-pulse rounded-xl" />
          <div className="bg-muted h-24 animate-pulse rounded-xl" />
        </div>

        <div className="space-y-6 lg:col-span-1">
          <div className="bg-muted h-40 animate-pulse rounded-xl" />
          <div className="bg-muted h-40 animate-pulse rounded-xl" />
        </div>
      </div>
    </div>
  );
}
