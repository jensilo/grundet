function SkeletonBlock({ className }: { className: string }): React.JSX.Element {
  return <div className={className} />;
}

export default function DashboardLoading(): React.JSX.Element {
  return (
    <div
      className="mx-auto max-w-[1080px] space-y-10"
      aria-busy="true"
      aria-label="Loading teams and projects"
    >
      <header className="space-y-2">
        <SkeletonBlock className="bg-muted h-9 w-72 rounded-md" />
        <SkeletonBlock className="bg-muted h-5 w-full max-w-[560px] rounded-md" />
      </header>

      <section className="space-y-4">
        <div className="flex items-center justify-between gap-3">
          <SkeletonBlock className="bg-muted h-5 w-24 rounded-md" />
          <SkeletonBlock className="bg-muted h-5 w-28 rounded-md" />
        </div>
        <div className="space-y-4">
          {["team-one", "team-two"].map((id) => (
            <div key={id} className="bg-card ring-foreground/10 rounded-xl py-4 ring-1">
              <div className="border-border flex items-center gap-2 border-b px-4 pb-4">
                <SkeletonBlock className="bg-muted h-5 w-40 rounded-md" />
                <SkeletonBlock className="bg-muted h-5 w-20 rounded-sm" />
              </div>
              <div className="grid grid-cols-1 gap-4 px-4 pt-4 lg:grid-cols-2">
                {[`${id}-project-one`, `${id}-project-two`].map((projectId) => (
                  <div key={projectId} className="border-border rounded-lg border p-4">
                    <SkeletonBlock className="bg-muted h-5 w-48 rounded-md" />
                    <SkeletonBlock className="bg-muted mt-2 h-4 w-full rounded-md" />
                    <SkeletonBlock className="bg-muted mt-4 h-4 w-32 rounded-md" />
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
      </section>
    </div>
  );
}
