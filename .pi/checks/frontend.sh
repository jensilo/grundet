#!/usr/bin/env bash
set -euo pipefail

repo_root="$(git rev-parse --show-toplevel)"
cd "$repo_root/src/Grundet.Web"

if [[ ! -d node_modules ]]; then
  echo '$ npm ci'
  npm ci
  echo '[/sum:]npm ci: dependencies installed'
fi

echo '$ npm run format:check'
npm run format:check
echo '[/sum:]frontend format: passed'

echo '$ npm run lint'
npm run lint
echo '[/sum:]frontend lint: passed'
