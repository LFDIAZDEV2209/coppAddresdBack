#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "=================================================="
echo "  Updating AppDbContext Migrations (PostgreSQL)   "
echo "=================================================="

TARGET_MIGRATION="${1:-}"

if [ -n "$TARGET_MIGRATION" ]; then
    echo "Targeting migration: $TARGET_MIGRATION"
    dotnet ef database update "$TARGET_MIGRATION" --project ../CoppAddresd.Infrastructure --startup-project . --context AppDbContext
else
    echo "Applying latest migrations..."
    dotnet ef database update --project ../CoppAddresd.Infrastructure --startup-project . --context AppDbContext
fi

echo "Migrations applied successfully."
