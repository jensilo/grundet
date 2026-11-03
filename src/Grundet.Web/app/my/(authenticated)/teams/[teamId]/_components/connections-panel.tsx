import { Plug } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";

export function ConnectionsPanel(): React.JSX.Element {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Connections</CardTitle>
        <CardDescription>
          External tools will connect execution context back to Grundet without turning product
          intent into a ticketing workflow.
        </CardDescription>
      </CardHeader>
      <CardContent>
        <div className="border-border bg-background flex items-start justify-between gap-4 rounded-md border p-4">
          <div className="flex min-w-0 items-start gap-3">
            <div className="bg-muted text-muted-foreground flex size-8 shrink-0 items-center justify-center rounded-sm">
              <Plug className="size-4" aria-hidden={true} />
            </div>
            <div className="min-w-0 space-y-1">
              <div className="flex flex-wrap items-center gap-2">
                <h3 className="text-foreground text-sm font-semibold">Jira</h3>
                <Badge variant="outline">Future</Badge>
              </div>
              <p className="text-muted-foreground text-sm">
                Jira integration is planned, but not available in this alpha view yet.
              </p>
            </div>
          </div>
          <span className="bg-muted text-muted-foreground inline-flex h-7 shrink-0 items-center rounded-sm px-2 text-xs font-medium">
            Inactive
          </span>
        </div>
      </CardContent>
    </Card>
  );
}
