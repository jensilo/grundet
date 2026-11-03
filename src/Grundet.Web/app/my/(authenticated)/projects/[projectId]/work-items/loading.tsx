export default function LoadingWorkItemsPage(): React.JSX.Element {
  return (
    <div className="mx-auto max-w-[1080px] space-y-6" aria-label="Loading work">
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div className="space-y-2">
          <div className="bg-muted h-4 w-16 animate-pulse rounded-sm" />
          <div className="bg-muted h-8 w-48 animate-pulse rounded-sm" />
        </div>
        <div className="bg-muted h-9 w-36 animate-pulse rounded-md" />
      </div>
      <div className="bg-muted h-12 animate-pulse rounded-lg" />
      <div className="grid grid-cols-1 gap-4 md:grid-cols-2 lg:grid-cols-3">
        <div className="bg-muted h-32 animate-pulse rounded-xl" />
        <div className="bg-muted h-32 animate-pulse rounded-xl" />
        <div className="bg-muted h-32 animate-pulse rounded-xl" />
        <div className="bg-muted h-32 animate-pulse rounded-xl" />
        <div className="bg-muted h-32 animate-pulse rounded-xl" />
        <div className="bg-muted h-32 animate-pulse rounded-xl" />
      </div>
    </div>
  );
}
