#!/usr/bin/env bash
# Usage: bash scripts/package.sh
set -euo pipefail
cd "$(dirname "$0")/.."

ZIP=SE4040_Submission.zip
rm -f "$ZIP"

zip -r "$ZIP" . \
  -x ".git/*" ".github/*" ".vscode/*" ".idea/*" \
  -x "apps/web/node_modules/*" "apps/web/dist/*" "apps/web/.vite/*" \
  -x "apps/android/.gradle/*" "apps/android/build/*" "apps/android/app/build/*" \
  -x "apps/backend/bin/*" "apps/backend/obj/*" "apps/backend/publish/*" \
  -x "apps/web/.env.local" "apps/backend/appsettings.Development.json"
echo "Created $ZIP"
