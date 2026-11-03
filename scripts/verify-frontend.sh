#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR/src/Grundet.Web"

TEMP_FILES=()
cleanup() {
  if [[ ${#TEMP_FILES[@]} -gt 0 ]]; then
    rm -f "${TEMP_FILES[@]}"
  fi
}
trap cleanup EXIT

run_step() {
  local label="$1"
  shift

  echo "[verify-frontend] $label"

  if [[ "${GRUNDET_VERIFY_VERBOSE:-0}" == "1" ]]; then
    "$@"
    echo "[verify-frontend] ✓ $label"
    return
  fi

  local output_file
  output_file="$(mktemp)"
  TEMP_FILES+=("$output_file")

  if "$@" >"$output_file" 2>&1; then
    echo "[verify-frontend] ✓ $label"
  else
    local status=$?
    echo "[verify-frontend] ✗ $label"
    cat "$output_file"
    return "$status"
  fi
}

run_step "Installing dependencies" pnpm install --frozen-lockfile
run_step "Type checking" pnpm exec tsc --noEmit
run_step "Checking formatting" pnpm exec prettier --check .
run_step "Linting" pnpm exec eslint .
run_step "Running tests" pnpm exec vitest run
run_step "Building" pnpm exec next build

echo "[verify-frontend] All checks passed"
