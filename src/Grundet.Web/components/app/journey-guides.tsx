import { GuideHint } from "@/components/app/guide-hint";

// The four contextual guides that chain the Project → Feature → Intent → Work journey.
// Copy is single-sourced here so the same guide reads identically on the empty state that
// introduces a surface and the quiet "?" replay that stays on the populated surface.
// Each guide names the next concrete action so the user is always led onward.

export function ProjectGuide(): React.JSX.Element {
  return (
    <GuideHint guideId="project" title="About projects">
      <p>
        A project holds the product truth for one initiative. Create one, then add a feature inside
        it to group your first intent.
      </p>
    </GuideHint>
  );
}

export function FeatureGuide(): React.JSX.Element {
  return (
    <GuideHint guideId="feature" title="About features">
      <p>
        Features group related intent inside a project. Create your first feature, then write the
        intent that defines what should be true.
      </p>
    </GuideHint>
  );
}

export function IntentGuide(): React.JSX.Element {
  return (
    <GuideHint guideId="intent" title="About intent">
      <p>
        Intent lives inside a feature – create a feature first, then write what should be true, and
        why.
      </p>
      <p>
        Every change is versioned, so the history becomes your changelog. Next, add work to make the
        intent true.
      </p>
    </GuideHint>
  );
}

export function WorkGuide(): React.JSX.Element {
  return (
    <GuideHint guideId="work" title="About work">
      <p>
        Work is how intent becomes true. Create a work item, then link it to the intent it serves.
      </p>
    </GuideHint>
  );
}
