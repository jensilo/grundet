"use client";

import { useState } from "react";

import { type AnyVersionEntry, VersionEntry } from "@/components/app/version-entry";

export function VersionTimeline({
  versions,
  projectId,
}: {
  versions: AnyVersionEntry[];
  projectId: string;
}) {
  // Sort by explicit version number so ordering never depends on API list direction.
  const ordered = [...versions].sort((a, b) => Number(b.versionNumber) - Number(a.versionNumber));
  const latestId = ordered[0]?.versionId;
  const [expanded, setExpanded] = useState<Set<string>>(new Set(latestId ? [latestId] : []));

  const toggle = (id: string) => {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  return (
    <ol className="relative space-y-0">
      {ordered.map((v, i) => (
        <VersionEntry
          key={v.versionId}
          version={v}
          projectId={projectId}
          expanded={expanded.has(v.versionId)}
          onToggle={() => toggle(v.versionId)}
          isLatest={v.versionId === latestId}
          isLast={i === ordered.length - 1}
        />
      ))}
    </ol>
  );
}
