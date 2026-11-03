"use client";

import { listWorkItems } from "@/lib/api/generated/sdk.gen";

import { EntityMultiPicker, type PickerItem } from "./entity-multi-picker";

export type SelectedWork = {
  workItemId: string;
  ref: string;
  title: string;
};

const RESULT_LIMIT = "8";

function toItem(work: SelectedWork): PickerItem {
  return { id: work.workItemId, primary: work.ref, secondary: work.title };
}

function toWork(item: PickerItem): SelectedWork {
  return { workItemId: item.id, ref: item.primary, title: item.secondary };
}

/**
 * Multi-select picker for linking work items to an intent, backed by the project work browse
 * endpoint (server-side `q`; deleted work already excluded by the CurrentWorkItems view). The
 * mirror of IntentLinkPicker for the intent detail page.
 */
export function WorkLinkPicker({
  id,
  projectId,
  values,
  onChange,
  disabled,
  excludeWorkItemIds = [],
  placeholder = "Search work by title or WO-id",
}: {
  id: string;
  projectId: string;
  values: SelectedWork[];
  onChange: (values: SelectedWork[]) => void;
  disabled?: boolean;
  excludeWorkItemIds?: string[];
  placeholder?: string;
}): React.JSX.Element {
  async function search(term: string): Promise<PickerItem[]> {
    const { data } = await listWorkItems({
      path: { projectId },
      query: { q: term.trim() || undefined, pageSize: RESULT_LIMIT },
    });
    return (data?.items ?? []).map((item) => ({
      id: item.workItemId,
      primary: item.ref,
      secondary: item.title,
    }));
  }

  return (
    <EntityMultiPicker
      id={id}
      values={values.map(toItem)}
      onChange={(items) => onChange(items.map(toWork))}
      search={search}
      disabled={disabled}
      excludeIds={excludeWorkItemIds}
      placeholder={placeholder}
      emptyLabel="No matching work."
    />
  );
}
