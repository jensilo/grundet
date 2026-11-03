"use client";

import { listProjectRequirements } from "@/lib/api/generated/sdk.gen";

import { EntityMultiPicker, type PickerItem } from "./entity-multi-picker";

export type SelectedIntent = {
  requirementId: string;
  ref: string;
  text: string;
};

const RESULT_LIMIT = "8";

function toItem(intent: SelectedIntent): PickerItem {
  return { id: intent.requirementId, primary: intent.ref, secondary: intent.text };
}

function toIntent(item: PickerItem): SelectedIntent {
  return { requirementId: item.id, ref: item.primary, text: item.secondary };
}

/**
 * Multi-select picker for linking intent (requirements) to a work item, backed by the project
 * requirements browse endpoint (server-side `q`; deleted intent already excluded by the
 * CurrentRequirements view). Used in the work composer and on the work detail page.
 */
export function IntentLinkPicker({
  id,
  projectId,
  values,
  onChange,
  disabled,
  excludeRequirementIds = [],
  placeholder = "Search intent by text or IN-id",
}: {
  id: string;
  projectId: string;
  values: SelectedIntent[];
  onChange: (values: SelectedIntent[]) => void;
  disabled?: boolean;
  excludeRequirementIds?: string[];
  placeholder?: string;
}): React.JSX.Element {
  async function search(term: string): Promise<PickerItem[]> {
    const { data } = await listProjectRequirements({
      path: { projectId },
      query: { q: term.trim() || undefined, pageSize: RESULT_LIMIT },
    });
    return (data?.items ?? []).map((item) => ({
      id: item.requirementId,
      primary: item.ref,
      secondary: item.text,
    }));
  }

  return (
    <EntityMultiPicker
      id={id}
      values={values.map(toItem)}
      onChange={(items) => onChange(items.map(toIntent))}
      search={search}
      disabled={disabled}
      excludeIds={excludeRequirementIds}
      placeholder={placeholder}
      emptyLabel="No matching intent."
    />
  );
}
