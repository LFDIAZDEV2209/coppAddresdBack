#!/usr/bin/env bash
# dev-up.sh - Starts Postgres + all services (Auth, Community, Gateway, Telemedicine, API) for development.
# Usage: ./scripts/dev-up.sh [--watch]
#   --watch: uses `dotnet watch run` for hot reload (skips pre-build, longer wait).
# Stop: ./scripts/dev-down.sh
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOGS="$ROOT/scripts/logs"
mkdir -p "$LOGS"

WATCH=0
[[ "${1:-}" == "--watch" ]] && WATCH=1

# name|project|url|port|color
SERVICES=(
  "auth|src/Services/CoppAddresd.Auth|http://localhost:5123|5123|36"
  "community|src/Services/CoppAddresd.Community|http://localhost:5200|5200|32"
  "gateway|src/Services/CoppAddresd.Gateway|http://localhost:5080|5080|35"
  "telemedicine|src/Services/CoppAddresd.Telemedicine|http://localhost:5130|5130|33"
  "api|src/CoppAddresd.Api|http://localhost:5122|5122|34"
)

# Wait timeout: 90s normal (pre-built), 240s watch (builds internally).
WAIT_TIMEOUT=$(( WATCH == 1 ? 240 : 90 ))

port_open() { (echo > "/dev/tcp/127.0.0.1/$1") >/dev/null 2>&1; }

wait_port() {
  local port="$1" timeout="$2"
  local deadline=$(( $(date +%s) + timeout ))
  while (( $(date +%s) < deadline )); do
    port_open "$port" && return 0
    sleep 2
  done
  port_open "$port"
}

color() { printf "\033[%sm%s\033[0m\n" "$1" "$2"; }
colorn() { printf "\033[%sm%s\033[0m" "$1" "$2"; }
bold() { printf "\033[1m%s\033[0m\n" "$1"; }

draw_banner() {
  local title="$1" color_code="${2:-36}"
  local width=60
  local pad=$(( (width - ${#title}) / 2 ))
  echo ""
  color "$color_code" "$(printf '=%.0s' $(seq 1 $width))"
  color "$color_code" "$(printf '%*s%s' $pad '' "$title")"
  color "$color_code" "$(printf '=%.0s' $(seq 1 $width))"
}

draw_service_row() {
  local name="$1" url="$2" status="$3" status_color="$4" name_color="$5"
  printf "  %s  %s  %s\n" \
    "$(colorn "$name_color" "$(printf '%-16s' "$name")")" \
    "$(colorn "$name_color" "$(printf '%-32s' "$url")")" \
    "$(colorn "$status_color" "$(printf '%-10s' "$status")")"
}

echo ""
draw_banner "COPPADRESD BACKEND - DEV ENVIRONMENT" 32

echo ""
draw_banner "POSTGRES (DOCKER)" 36
if [[ -f "$ROOT/docker-compose.yml" || -f "$ROOT/docker-compose.yaml" ]]; then
  colorn 36 "  Starting Postgres..."
  (cd "$ROOT" && docker compose up -d postgres) || {
    color 31 " docker compose up failed. Is Docker running?"
    exit 1
  }
  color 32 " DONE"

  deadline=$(( $(date +%s) + 60 ))
  healthy=0
  colorn 36 "  Waiting for health check..."
  while (( $(date +%s) < deadline )); do
    [[ "$(docker inspect --format '{{.State.Health.Status}}' coppAddresd 2>/dev/null)" == "healthy" ]] && { healthy=1; break; }
    colorn 36 "."
    sleep 2
  done
  echo ""
  if [[ $healthy -eq 0 ]]; then
    color 31 "  Postgres not healthy after 60s. Check Docker."
    exit 1
  fi
  color 32 "  Postgres is healthy!"
else
  color 33 "  No docker-compose.yaml found. Skipping Postgres (assuming external DB)."
fi

# Pre-build phase (skipped for --watch: dotnet watch builds internally).
if [[ $WATCH -eq 0 ]]; then
  echo ""
  draw_banner "BUILDING SOLUTION" 36
  color 36 "  Building all 5 projects in parallel (fail fast)..."
  # Limpieza de nodos de build huérfanos de corridas abortadas: con nodeReuse
  # los workers MSBuild/VBCSCompiler quedan vivos con handles abiertos sobre
  # los ref assemblies compartidos (race MSB3883 en builds paralelos).
  # Nota: los patrones usan [.] para que pkill -f no matchee el propio shell.
  pkill -f 'MSBuild[.]dll /noautoresponse' 2>/dev/null || true
  pkill -f 'VBCSCompiler -pipenam[e]' 2>/dev/null || true
  build_pids=()
  for entry in "${SERVICES[@]}"; do
    IFS='|' read -r name project url port clr <<< "$entry"
    color "$clr" "  Building $name..."
    (cd "$ROOT" && dotnet build "$project" > "$LOGS/$name.build.log" 2>&1) &
    build_pids+=("$name|$!")
  done

  failed_builds=()
  for pe in "${build_pids[@]}"; do
    IFS='|' read -r name pid <<< "$pe"
    wait "$pid"
    if [[ $? -ne 0 ]]; then failed_builds+=("$name"); fi
  done

  # Los builds en paralelo comparten el proyecto Domain y pueden fallar por
  # locks transitorios (MSB3883): un único reintento en serie suele bastar.
  if [[ ${#failed_builds[@]} -gt 0 ]]; then
    color 33 "  Retrying failed builds sequentially..."
    still_failed=()
    for n in "${failed_builds[@]}"; do
      for entry in "${SERVICES[@]}"; do
        IFS='|' read -r sname sproject surl sport sclr <<< "$entry"
        if [[ "$sname" == "$n" ]]; then
          color "$sclr" "  Rebuilding $n..."
          if ! (cd "$ROOT" && dotnet build "$sproject" > "$LOGS/$n.build.log" 2>&1); then
            still_failed+=("$n")
          fi
        fi
      done
    done
    failed_builds=("${still_failed[@]}")
  fi

  if [[ ${#failed_builds[@]} -gt 0 ]]; then
    echo ""
    color 31 "  BUILD FAILED. Fix the following project(s) before starting:"
    for n in "${failed_builds[@]}"; do color 31 "    - $n (see $LOGS/$n.build.log)"; done
    echo ""
    color 31 "  Aborting dev-up. No services were started."
    exit 1
  fi
  color 32 "  All projects built successfully."
fi

# Migraciones pendientes del backend. La API NO migra al arrancar (a diferencia
# del Auth Service): sin este paso, un dev con la BD atrasada arranca con
# esquema viejo y endpoints como /health-tests/geo fallan con
# "relation ... does not exist" (500). Idempotente: EF omite las ya aplicadas.
echo ""
draw_banner "APPLYING DATABASE MIGRATIONS" 36
migrate_args=(run --project src/CoppAddresd.Api -- --migrate)
[[ $WATCH -eq 0 ]] && migrate_args=(run --no-build --project src/CoppAddresd.Api -- --migrate)
colorn 36 "  Applying pending migrations..."
if (cd "$ROOT" && dotnet "${migrate_args[@]}" > "$LOGS/migrate.log" 2>&1); then
  color 32 "  DONE"
else
  color 31 "  MIGRATION FAILED. See $LOGS/migrate.log"
  tail -n 15 "$LOGS/migrate.log" 2>/dev/null | while IFS= read -r line; do color 90 "  $line"; done
  exit 1
fi

echo ""
draw_banner "AI SERVICE (PYTHON/FastAPI)" 36
AI_ROOT="$ROOT/../ai-service"
AI_NAME="ai"
AI_PORT=8000
AI_STARTED=0
AI_RESULT=""
if [[ ! -d "$AI_ROOT" ]]; then
  color 33 "  ai-service repo not found (expected at ../ai-service). Skipped."
  AI_RESULT="$AI_NAME|http://localhost:8000|$AI_PORT|Missing|33"
elif [[ ! -f "$AI_ROOT/.env" ]]; then
  color 33 "  ai-service: .env missing (cp .env.example .env). Skipped."
  AI_RESULT="$AI_NAME|http://localhost:8000|$AI_PORT|Skipped|33"
elif [[ ! -d "$AI_ROOT/.venv" ]]; then
  color 33 "  ai-service: .venv missing (run 'uv sync' in ai-service). Skipped."
  AI_RESULT="$AI_NAME|http://localhost:8000|$AI_PORT|Skipped|33"
elif port_open "$AI_PORT"; then
  color 33 "  $AI_NAME already running on :$AI_PORT (skipped)"
  AI_RESULT="$AI_NAME|http://localhost:8000|$AI_PORT|Running|36"
else
  colorn 36 "  Starting $AI_NAME..."
  (cd "$AI_ROOT" && nohup uv run python run_dev.py > "$LOGS/$AI_NAME.log" 2> "$LOGS/$AI_NAME.err" & echo $! > "$LOGS/$AI_NAME.pid")
  AI_STARTED=1
  color 36 " PID $(cat "$LOGS/$AI_NAME.pid")"
fi

echo ""
draw_banner "STARTING SERVICES" 36

started=()
results=()
for entry in "${SERVICES[@]}"; do
  IFS='|' read -r name project url port clr <<< "$entry"
  if port_open "$port"; then
    color 33 "  $name already running on :$port (skipped)"
    # It IS running on the port, so report as Running.
    results+=("$name|$url|$port|Running|$clr")
    continue
  fi
  if [[ $WATCH -eq 1 ]]; then
    args=(watch run --project "$project")
  else
    args=(run --no-build --project "$project")
  fi
  colorn "$clr" "  Starting $name..."
  (cd "$ROOT" && nohup dotnet "${args[@]}" > "$LOGS/$name.log" 2> "$LOGS/$name.err" & echo $! > "$LOGS/$name.pid")
  started+=("$name")
  color "$clr" " PID $(cat "$LOGS/$name.pid")"
done

echo ""
draw_banner "WAITING FOR SERVICES" 36

failed=0
for entry in "${SERVICES[@]}"; do
  IFS='|' read -r name project url port clr <<< "$entry"
  in_started=0
  for s in "${started[@]:-}"; do [[ "$s" == "$name" ]] && in_started=1; done
  [[ $in_started -eq 0 ]] && continue
  colorn "$clr" "  Waiting for $name on port $port..."
  if wait_port "$port" "$WAIT_TIMEOUT"; then
    color 32 " READY"
    results+=("$name|$url|$port|Running|$clr")
  else
    color 31 " FAILED"
    results+=("$name|$url|$port|FAILED|31")
    failed=1
    if [[ -f "$LOGS/$name.err" ]] && grep -qi "Application Control policy has blocked" "$LOGS/$name.err"; then
      color 33 "  Windows Application Control (Smart App Control / WDAC) blocked the executable."
      color 33 "  Allow the repo path in Windows Security > App & browser control, or disable Smart App Control."
    fi
    [[ -f "$LOGS/$name.log" ]] && {
      color 90 "  --- Last 15 lines of $name.log ---"
      tail -n 15 "$LOGS/$name.log" 2>/dev/null | while IFS= read -r line; do color 90 "  $line"; done
    }
  fi
done

# AI service wait (non-fatal: backend has circuit breaker if it is down).
if [[ $AI_STARTED -eq 1 && -n "$AI_RESULT" ]]; then
  colorn 36 "  Waiting for $AI_NAME on port $AI_PORT..."
  if wait_port "$AI_PORT" "$WAIT_TIMEOUT"; then
    color 32 " READY"
    AI_RESULT="$AI_NAME|http://localhost:8000|$AI_PORT|Running|36"
  else
    color 31 " FAILED (non-fatal)"
    AI_RESULT="$AI_NAME|http://localhost:8000|$AI_PORT|FAILED|31"
    [[ -f "$LOGS/$AI_NAME.log" ]] && {
      color 90 "  --- Last 15 lines of $AI_NAME.log ---"
      tail -n 15 "$LOGS/$AI_NAME.log" 2>/dev/null | while IFS= read -r line; do color 90 "  $line"; done
    }
  fi
fi

[[ -n "$AI_RESULT" ]] && results+=("$AI_RESULT")

echo ""
draw_banner "SERVICE STATUS" 36
color 90 "  Name             URL                                 Status"
color 90 "  ----             ---                                 ------"
for r in "${results[@]:-}"; do
  IFS='|' read -r name url port status clr <<< "$r"
  draw_service_row "$name" "$url" "$status" "$clr" "$clr"
done

echo ""
if [[ $failed -eq 1 ]]; then
  draw_banner "SOME SERVICES FAILED" 31
  color 31 "  Check logs in: $LOGS"
  exit 1
fi

draw_banner "ALL SERVICES RUNNING" 32
color 90 "  Stop with: ./scripts/dev-down.sh"
color 90 "  Logs in: $LOGS"
color 90 "  View logs: ./scripts/dev-logs.sh <service> [-follow] [-err]"
color 90 "  Services: auth, community, gateway, telemedicine, api, ai, postgres"
