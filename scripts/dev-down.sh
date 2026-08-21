#!/usr/bin/env bash
# dev-down.sh - Detiene Auth + Community + API iniciados con dev-up.sh.
# Postgres (docker compose) queda arriba; detenerlo con: docker compose down
set -uo pipefail

LOGS="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/logs"
shopt -s nullglob
pids=( "$LOGS"/*.pid )
if [[ ${#pids[@]} -eq 0 ]]; then
  echo "No hay servicios registrados (nada que detener)."
  exit 0
fi
for f in "${pids[@]}"; do
  name="$(basename "$f" .pid)"
  id="$(cat "$f")"
  if kill -0 "$id" 2>/dev/null; then
    kill "$id" 2>/dev/null || true
    pkill -P "$id" 2>/dev/null || true
    echo "detenido $name (PID $id)"
  else
    echo "$name ya no corria (PID $id)"
  fi
  rm -f "$f"
done
echo "Postgres queda arriba (detener con: docker compose down)."