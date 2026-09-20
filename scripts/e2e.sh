#!/usr/bin/env bash
# End-to-end run: a throwaway PostgreSQL container, the real .NET API, the Vite dev server and Playwright
# (driving your installed Google Chrome — no browser download). Nothing here touches a local database:
# the container is created and removed by this script.
#
#   scripts/e2e.sh                       # whole suite
#   scripts/e2e.sh e2e/login.e2e.ts      # arguments go to `playwright test`
#
# Needs: Docker, the .NET SDK (dotnet tool restore), Node deps (cd web && npm ci), Google Chrome
# (or E2E_BROWSER_CHANNEL=msedge / chromium after `npx playwright install chromium`).
# Ports (all overridable, all chosen not to collide with a normal dev setup): PostgreSQL 55432 (E2E_PG_PORT),
# API 5209 (E2E_API_PORT), Vite 5174 (E2E_APP_PORT). They must be free.
set -euo pipefail

cd "$(dirname "$0")/.."
NAME="fynovio-e2e-pg"
PG_PORT="${E2E_PG_PORT:-55432}"
ADMIN_CONN="Host=localhost;Port=${PG_PORT};Database=fynovio_platform;Username=postgres;Password=postgres"

cleanup() { docker rm -f "$NAME" >/dev/null 2>&1 || true; }
trap cleanup EXIT
cleanup

echo "==> starting PostgreSQL on :${PG_PORT}"
docker run -d --name "$NAME" -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=fynovio_platform \
  -p "127.0.0.1:${PG_PORT}:5432" postgres:17-alpine >/dev/null
until docker exec "$NAME" pg_isready -U postgres -d fynovio_platform >/dev/null 2>&1; do sleep 1; done
# The image restarts once during init; wait until it accepts a real query.
until docker exec "$NAME" psql -U postgres -d fynovio_platform -c "SELECT 1" >/dev/null 2>&1; do sleep 1; done

echo "==> migrations (as postgres)"
dotnet tool restore >/dev/null
for module in MasterData CRM Access; do   # CRM's migrations reference masterdata tables
  dotnet ef database update --connection "$ADMIN_CONN" \
    --project "src/Modules/$module/$module.csproj" --startup-project "src/Modules/$module/$module.csproj" 2>&1 | grep -v NU1900 || true
done

echo "==> runtime role"
sed "s/change-me/runtime/" scripts/create-runtime-role.sql | docker exec -i "$NAME" psql -U postgres -d fynovio_platform -q >/dev/null

echo "==> building the API"
dotnet build src/Host/Host.csproj -m:1 -nodeReuse:false --nologo -v q

echo "==> playwright"
cd web
E2E_PG_PORT="$PG_PORT" npx playwright test "$@"
