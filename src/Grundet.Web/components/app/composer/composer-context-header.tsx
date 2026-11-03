import { FolderTree } from "lucide-react";

/**
 * "Where am I creating this" indicator shown at the top of both composer hosts (dialog and page).
 * Renders the target Project (and Feature, for Intent) so the user always sees the location the new
 * or edited item lands in. Work items are project-scoped — omit `featureName` there.
 */
export function ComposerContextHeader({
  mode,
  projectName,
  featureName,
}: {
  mode: "create" | "edit";
  projectName: string;
  featureName?: string;
}): React.JSX.Element {
  return (
    <div className="text-muted-foreground flex items-center gap-1.5 text-xs">
      <FolderTree aria-hidden={true} className="size-3.5 shrink-0" />
      <span>{mode === "edit" ? "Editing in" : "Creating in"}</span>
      <span className="text-foreground min-w-0 truncate font-medium">{projectName}</span>
      {featureName && (
        <>
          <span className="text-muted-foreground/50">/</span>
          <span className="text-foreground min-w-0 truncate font-medium">{featureName}</span>
        </>
      )}
    </div>
  );
}
