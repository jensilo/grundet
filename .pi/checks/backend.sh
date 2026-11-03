#!/usr/bin/env bash
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

changed_cs=()
while IFS= read -r file; do
  changed_cs+=("$file")
done < <(.pi/checks/changed-files.sh | awk '/\.cs$/')

if ((${#changed_cs[@]} == 0)); then
  echo '[/sum:]backend format: no changed C# files'
  exit 0
fi

run_format_for_root() {
  local root="$1"
  shift

  local files=()
  for file in "${changed_cs[@]}"; do
    if [[ "$file" == "$root"/*.cs ]]; then
      files+=("$file")
    fi
  done

  if ((${#files[@]} == 0)); then
    echo "[/sum:]dotnet format $root: no changed files"
    return
  fi

  echo "$ dotnet format $root --include <${#files[@]} changed file(s)>"
  dotnet format "$root" --include "${files[@]}" --verify-no-changes --no-restore
  echo "[/sum:]dotnet format $root: ${#files[@]} changed file(s) passed"
}

run_format_for_root "src/Grundet.Api"
run_format_for_root "src/Grundet.AdminCli"
run_format_for_root "src/Grundet.Api.Tests"
run_format_for_root "src/Grundet.AdminCli.Tests"
