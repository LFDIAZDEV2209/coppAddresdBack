#!/usr/bin/env bash
# dev-down.sh - Stops all services (Auth, Community, Gateway, Telemedicine, API) by PORT.
# Catches skipped, manually-started and stale-PID services. Postgres (docker compose)
# stays up; stop it with: docker compose down
set -uo pipefail

LOGS="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/logs"

# Ports the dev environment owns (mirrors dev-up.sh).
PORTS=(
  "auth|5123"
  "community|5200"
  "gateway|5080"
  "telemedicine|5130"
  "api|5122"
)

color() { printf "\033[%sm%s\033[0m" "$1" "$2"; }

draw_banner() {
  local title="$1" color_code="${2:-36}"
  local width=50
  local pad=$(( (width - ${#title}) / 2 ))
  echo ""
  color "$color_code" "$(printf '=%.0s' $(seq 1 $width))"
  color "$color_code" "$(printf '%*s%s' $pad '' "$title")"
  color "$color_code" "$(printf '=%.0s' $(seq 1 $width))"
}

# Find the listener PID for a TCP port (prefer lsof, fall back to fuser).
listener_pid() {
  local port="$1" pid=""
  if command -v lsof >/dev/null 2>&1; then
    pid="$(lsof -ti tcp:"$port" -sTCP:LISTEN 2>/dev/null | head -n1)"
  fi
  if [[ -z "$pid" && "$(command -v fuser)" ]]; then
    pid="$(fuser "${port}/tcp" 2>/dev/null | tr -d ' ' | head -n1)"
  fi
  echo "$pid"
}

draw_banner "STOPPING SERVICES" 36

for entry in "${PORTS[@]}"; do
  IFS='|' read -r name port <<< "$entry"
  pid="$(listener_pid "$port")"
  if [[ -n "$pid" ]]; then
    kill -9 "$pid" 2>/dev/null || true
    # Also kill child processes if any.
    pkill -P "$pid" 2>/dev/null || true
    color 32 "  Stopped $name (port :$port)"
  else
    color 33 "  Already stopped $name (port :$port)"
  fi
done

# Clean up any leftover pid files (stale or from skipped services).
if [[ -d "$LOGS" ]]; then
  shopt -s nullglob
  for f in "$LOGS"/*.pid; do rm -f "$f"; done
fi

echo ""
color 90 "Postgres stays up (stop with: docker compose down)."
