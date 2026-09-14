#!/usr/bin/env bash
# dev-logs.sh - View dev service logs.
# Usage:
#   ./scripts/dev-logs.sh                  # list services + which are running + log files present
#   ./scripts/dev-logs.sh <service>        # last 50 lines of <service>.log
#   ./scripts/dev-logs.sh <service> -follow   # live tail (Ctrl+C to exit)
#   ./scripts/dev-logs.sh <service> -err       # view <service>.err instead of .log
#   ./scripts/dev-logs.sh <service> -lines 200 # last 200 lines
# Services: auth, community, gateway, telemedicine, api, ai, postgres
# Logs live in scripts/logs (already gitignored). Postgres is a Docker container.
set -uo pipefail

LOGS="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/logs"

SERVICE="${1:-}"
FOLLOW=0
ERR=0
N=${LINES:-50}

# Parse extra flags.
shift 2>/dev/null || shift $# 2>/dev/null || true
while [[ $# -gt 0 ]]; do
  case "$1" in
    -follow|-f) FOLLOW=1 ;;
    -err|-e)     ERR=1 ;;
    -lines|-n)   N="${2:-50}"; shift ;;
    *) ;;
  esac
  shift
done

# name|port|docker(0/1)
SERVICES=(
  "auth|5123|0"
  "community|5200|0"
  "gateway|5080|0"
  "telemedicine|5130|0"
  "api|5122|0"
  "ai|8000|0"
  "postgres|5432|1"
)

port_open() { (echo > "/dev/tcp/127.0.0.1/$1") >/dev/null 2>&1; }
color() { printf "\033[%sm%s\033[0m\n" "$1" "$2"; }
draw_banner() {
  local title="$1" color_code="${2:-36}"
  local width=50
  local pad=$(( (width - ${#title}) / 2 ))
  echo ""
  color "$color_code" "$(printf '=%.0s' $(seq 1 $width))"
  color "$color_code" "$(printf '%*s%s' $pad '' "$title")"
  color "$color_code" "$(printf '=%.0s' $(seq 1 $width))"
}

# --- List mode ---
if [[ -z "$SERVICE" ]]; then
  draw_banner "DEV SERVICE LOGS" 36
  color 90 "  Usage: ./scripts/dev-logs.sh <service> [-follow] [-err] [-lines N]"
  color 36 "  Services:"
  for entry in "${SERVICES[@]}"; do
    IFS='|' read -r name port docker <<< "$entry"
    if port_open "$port"; then st="RUNNING"; else st="stopped"; fi
    logf="$LOGS/$name.log"; errf="$LOGS/$name.err"
    if [[ -f "$logf" || -f "$errf" ]]; then mark="log available"; else mark="no log"; fi
    color 36 "    $(printf '%-12s' "$name") :$(printf '%-5s' "$port") $(printf '%-8s' "$st") $mark"
  done
  echo ""
  color 36 "  Examples:"
  color 90 "    ./scripts/dev-logs.sh api            # last 50 lines of api.log"
  color 90 "    ./scripts/dev-logs.sh api -follow    # live tail"
  color 90 "    ./scripts/dev-logs.sh ai -err        # ai.err (Python writes to stderr)"
  color 90 "    ./scripts/dev-logs.sh postgres       # docker logs coppAddresd"
  exit 0
fi

# Validate service name.
known=0
for entry in "${SERVICES[@]}"; do
  IFS='|' read -r name port docker <<< "$entry"
  [[ "$name" == "$SERVICE" ]] && { known=1; SVC_PORT="$port"; SVC_DOCKER="$docker"; break; }
done
if [[ $known -eq 0 ]]; then
  names=""; for entry in "${SERVICES[@]}"; do IFS='|' read -r name _ _ <<< "$entry"; names="$names, $name"; done
  color 31 "  Unknown service '$SERVICE'. Valid:${names#, }"
  exit 1
fi

# Docker service (postgres).
if [[ "$SVC_DOCKER" == "1" ]]; then
  if [[ $FOLLOW -eq 1 ]]; then docker logs -f coppAddresd; else docker logs --tail "$N" coppAddresd; fi
  exit $?
fi

# .NET / Python services (log files in scripts/logs).
logf="$LOGS/$SERVICE.log"
errf="$LOGS/$SERVICE.err"
if [[ $ERR -eq 0 && ( ! -s "$logf" ) && -f "$errf" ]]; then
  logf="$errf"   # fallback to .err when .log is empty/missing
fi
if [[ $ERR -eq 1 ]]; then logf="$errf"; fi
if [[ ! -f "$logf" ]]; then
  color 33 "  No log for '$SERVICE' ($logf)."
  color 90 "  Start it with ./scripts/dev-up.sh."
  exit 1
fi

draw_banner "$SERVICE log" 36
if [[ $FOLLOW -eq 1 ]]; then
  tail -n 30 -f "$logf"
else
  tail -n "$N" "$logf"
fi
