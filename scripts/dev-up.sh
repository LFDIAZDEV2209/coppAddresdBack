#!/usr/bin/env bash
# dev-up.sh - Levanta Postgres + Auth + Community + API (entorno de desarrollo).
# Uso: ./scripts/dev-up.sh [--watch]
#   --watch: usa `dotnet watch run` para hot reload.
# Detener: ./scripts/dev-down.sh
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LOGS="$ROOT/scripts/logs"
mkdir -p "$LOGS"

WATCH=0
[[ "${1:-}" == "--watch" ]] && WATCH=1

SERVICES=(
  "auth|src/Services/CoppAddresd.Auth|http://localhost:5123|5123"
  "community|src/Services/CoppAddresd.Community|http://localhost:5200|5200"
  "api|src/CoppAddresd.Api|http://localhost:5122|5122"
)

port_open() { (echo > "/dev/tcp/127.0.0.1/$1") >/dev/null 2>&1; }

wait_port() {
  local port="$1" timeout="$2" deadline=$(( $(date +%s) + timeout ))
  while (( $(date +%s) < deadline )); do
    port_open "$port" && return 0
    sleep 2
  done
  port_open "$port"
}

echo ""
echo "== Postgres (docker compose) =="
if [[ ! -f "$ROOT/docker-compose.yaml" ]]; then
  echo "Falta docker-compose.yaml en la raiz del repo." >&2
  exit 1
fi
(cd "$ROOT" && docker compose up -d postgres) || {
  echo "docker compose up fallo. Revisa que Docker este corriendo." >&2
  exit 1
}
deadline=$(( $(date +%s) + 60 ))
while (( $(date +%s) < deadline )); do
  [[ "$(docker inspect --format '{{.State.Health.Status}}' coppAddresd 2>/dev/null)" == "healthy" ]] && break
  sleep 2
done
if [[ "$(docker inspect --format '{{.State.Health.Status}}' coppAddresd 2>/dev/null)" != "healthy" ]]; then
  echo "Postgres no quedo healthy en 60s. Revisa Docker." >&2
  exit 1
fi
echo "Postgres healthy."

echo ""
echo "== Servicios =="
started=()
for entry in "${SERVICES[@]}"; do
  IFS='|' read -r name project url port <<< "$entry"
  if port_open "$port"; then
    echo "skip $name (ya escucha en :$port)"
    continue
  fi
  args=(run --project "$project")
  [[ $WATCH -eq 1 ]] && args=(watch run --project "$project")
  (cd "$ROOT" && nohup dotnet "${args[@]}" > "$LOGS/$name.log" 2> "$LOGS/$name.err" & echo $! > "$LOGS/$name.pid")
  started+=("$name")
  echo "iniciando $name (PID $(cat "$LOGS/$name.pid"))..."
done

echo ""
echo "== Esperando puertos =="
failed=0
for entry in "${SERVICES[@]}"; do
  IFS='|' read -r name project url port <<< "$entry"
  in_started=0
  for s in "${started[@]:-}"; do [[ "$s" == "$name" ]] && in_started=1; done
  [[ $in_started -eq 0 ]] && continue
  if wait_port "$port" 90; then
    echo "$name listo en $url"
  else
    echo "$name NO arranco en $url" >&2
    failed=1
    echo "--- ultimas lineas del log ---"
    tail -n 15 "$LOGS/$name.log" 2>/dev/null || true
  fi
done

echo ""
if [[ $failed -eq 1 ]]; then
  echo "Algunos servicios fallaron (revisa los logs en scripts/logs/)." >&2
  exit 1
fi
echo "Todo arriba. Detener con ./scripts/dev-down.sh"