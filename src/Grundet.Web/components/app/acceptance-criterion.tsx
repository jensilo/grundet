import { ReferenceText } from "@/components/app/reference-text";

export function AcceptanceCriterion({ items, projectId }: { items: string[]; projectId?: string }) {
  return (
    <div className="bg-muted rounded-md p-4">
      <ul className="space-y-2">
        {items.map((item) => (
          <li key={item} className="flex items-start gap-2.5">
            <span className="text-muted-foreground mt-0.5 font-mono text-xs select-none">[ ]</span>
            <span className="text-foreground text-sm">
              {projectId ? <ReferenceText projectId={projectId} text={item} /> : item}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}
