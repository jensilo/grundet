#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

TEST_PROJECT="src/Grundet.Api.Tests/Grundet.Api.Tests.csproj"
ADMIN_CLI_PROJECT="src/Grundet.AdminCli/Grundet.AdminCli.csproj"
ADMIN_CLI_TEST_PROJECT="src/Grundet.AdminCli.Tests/Grundet.AdminCli.Tests.csproj"

VERIFY_TMPDIR="${GRUNDET_VERIFY_TMPDIR:-$ROOT_DIR/.tmp}"
mkdir -p "$VERIFY_TMPDIR"
export TMPDIR="$VERIFY_TMPDIR"
export TMP="$VERIFY_TMPDIR"
export TEMP="$VERIFY_TMPDIR"

TEMP_FILES=()
cleanup() {
  if ((${#TEMP_FILES[@]})); then
    rm -f "${TEMP_FILES[@]}"
  fi
}
trap cleanup EXIT

run_step() {
  local label="$1"
  shift

  echo "[verify-backend] $label"

  if [[ "${GRUNDET_VERIFY_VERBOSE:-0}" == "1" ]]; then
    "$@"
    echo "[verify-backend] ✓ $label"
    return
  fi

  local output_file
  output_file="$(mktemp "$VERIFY_TMPDIR/verify-backend.XXXXXX")"
  TEMP_FILES+=("$output_file")

  if "$@" >"$output_file" 2>&1; then
    echo "[verify-backend] ✓ $label"
  else
    local status=$?
    echo "[verify-backend] ✗ $label"
    cat "$output_file"
    return "$status"
  fi
}

has_docker() {
  if [[ "${GRUNDET_FORCE_NO_DOCKER:-0}" == "1" ]]; then
    return 1
  fi

  if [[ "${GRUNDET_FORCE_DOCKER:-0}" == "1" ]]; then
    return 0
  fi

  if ! command -v docker >/dev/null 2>&1; then
    return 1
  fi

  docker info >/dev/null 2>&1
}

run_step "Restoring API project" dotnet restore src/Grundet.Api/Grundet.Api.csproj
run_step "Restoring admin CLI project" dotnet restore "$ADMIN_CLI_PROJECT"
run_step "Restoring test project" dotnet restore "$TEST_PROJECT"
run_step "Restoring admin CLI test project" dotnet restore "$ADMIN_CLI_TEST_PROJECT"

run_step "Verifying API code formatting" dotnet format src/Grundet.Api --verify-no-changes --no-restore
run_step "Verifying admin CLI code formatting" dotnet format src/Grundet.AdminCli --verify-no-changes --no-restore
run_step "Verifying test code formatting" dotnet format src/Grundet.Api.Tests --verify-no-changes --no-restore
run_step "Verifying admin CLI test code formatting" dotnet format src/Grundet.AdminCli.Tests --verify-no-changes --no-restore

run_step "Building admin CLI project (Release)" dotnet build "$ADMIN_CLI_PROJECT" --no-restore -c Release -warnaserror
run_step "Building tests project (Release)" dotnet build "$TEST_PROJECT" --no-restore -c Release -warnaserror
run_step "Building admin CLI tests project (Release)" dotnet build "$ADMIN_CLI_TEST_PROJECT" --no-restore -c Release -warnaserror

if has_docker; then
  run_step "Running full backend test suite" dotnet test "$TEST_PROJECT" --no-build -c Release
  run_step "Running admin CLI test suite" dotnet test "$ADMIN_CLI_TEST_PROJECT" --no-build -c Release
else
  run_step "Running non-Docker backend test subset" dotnet test "$TEST_PROJECT" --no-build -c Release --filter "Category!=DockerRequired"
  run_step "Running non-Docker admin CLI test subset" dotnet test "$ADMIN_CLI_TEST_PROJECT" --no-build -c Release --filter "Category!=DockerRequired"
fi

echo "[verify-backend] All checks passed"
