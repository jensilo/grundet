import { Fragment } from "react";

import { ReferenceLink } from "@/components/app/reference-link";
import { parseReferenceTokens } from "@/lib/references";

// Renders free text with IN-/WO-/# references turned into hoverable, clickable
// soft links. Safe to drop in anywhere a plain string is shown; when the text
// holds no references it renders exactly the original string.
export function ReferenceText({
  projectId,
  text,
  className,
  linkClassName,
}: {
  projectId: string;
  text: string;
  className?: string;
  linkClassName?: string;
}): React.JSX.Element {
  const tokens = parseReferenceTokens(text);

  if (tokens.length === 0 || tokens.every((token) => token.kind === "text")) {
    return className ? <span className={className}>{text}</span> : <>{text}</>;
  }

  const content = tokens.map((token) =>
    token.kind === "text" ? (
      <Fragment key={token.start}>{token.value}</Fragment>
    ) : (
      <ReferenceLink
        key={token.start}
        projectId={projectId}
        label={token.label}
        query={token.query}
        className={linkClassName}
      />
    )
  );

  return className ? <span className={className}>{content}</span> : <>{content}</>;
}
