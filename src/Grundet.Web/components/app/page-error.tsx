export function PageError({ message }: { message: string }): React.JSX.Element {
  return (
    <p
      role="alert"
      className="border-border bg-muted text-muted-foreground rounded-md border px-4 py-3 text-sm"
    >
      {message}
    </p>
  );
}
