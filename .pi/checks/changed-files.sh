#!/usr/bin/env bash
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root"

{
  git diff --name-only --diff-filter=ACMR HEAD --
  git ls-files --others --exclude-standard
} | sort -u
