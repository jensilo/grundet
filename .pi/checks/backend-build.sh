#!/usr/bin/env bash
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

run_step() {
  local label="$1"
  shift

  echo "$ $label"
  "$@"
  echo "[/sum:]$label: passed"
}

run_step "dotnet build src/Grundet.Api/Grundet.Api.csproj -warnaserror" \
  dotnet build src/Grundet.Api/Grundet.Api.csproj -warnaserror

run_step "dotnet build src/Grundet.AdminCli/Grundet.AdminCli.csproj -warnaserror" \
  dotnet build src/Grundet.AdminCli/Grundet.AdminCli.csproj -warnaserror

run_step "dotnet build src/Grundet.Api.Tests/Grundet.Api.Tests.csproj -warnaserror" \
  dotnet build src/Grundet.Api.Tests/Grundet.Api.Tests.csproj -warnaserror

run_step "dotnet build src/Grundet.AdminCli.Tests/Grundet.AdminCli.Tests.csproj -warnaserror" \
  dotnet build src/Grundet.AdminCli.Tests/Grundet.AdminCli.Tests.csproj -warnaserror
