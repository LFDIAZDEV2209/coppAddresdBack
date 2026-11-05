#!/usr/bin/env bash
# Expande el esquema con la imagen exacta nueva ANTES de enviarle tráfico.
set -euo pipefail
: "${AWS_REGION:?AWS_REGION requerida}"
: "${ECS_CLUSTER:?ECS_CLUSTER requerido}"
: "${MIGRATION_SERVICE:?MIGRATION_SERVICE requerido}"
: "${MIGRATION_TASK_DEFINITION:?MIGRATION_TASK_DEFINITION requerida}"
case "$MIGRATION_SERVICE" in cooppadresd-api|cooppadresd-telemedicine) ;; *) echo 'Servicio de migración no permitido' >&2; exit 1;; esac
CONTAINER=$(aws ecs describe-task-definition --task-definition "$MIGRATION_TASK_DEFINITION" --region "$AWS_REGION" --query 'taskDefinition.containerDefinitions[0].name' --output text)
NET_CFG=$(aws ecs describe-services --cluster "$ECS_CLUSTER" --services "$MIGRATION_SERVICE" --region "$AWS_REGION" --query 'services[0].networkConfiguration' --output json)
OVERRIDES=$(jq -cn --arg name "$CONTAINER" '{containerOverrides:[{name:$name,command:["--migrate"]}]}')
RESULT=$(aws ecs run-task --cluster "$ECS_CLUSTER" --task-definition "$MIGRATION_TASK_DEFINITION" --region "$AWS_REGION" --launch-type FARGATE --network-configuration "$NET_CFG" --overrides "$OVERRIDES" --output json)
TASK=$(jq -r '.tasks[0].taskArn // empty' <<< "$RESULT")
if [[ -z "$TASK" ]] || [[ $(jq '.failures | length' <<< "$RESULT") != 0 ]]; then
  echo '::error::ECS no pudo iniciar la tarea de migración; no se activará la imagen.' >&2
  exit 1
fi
echo "Tarea de migración: $TASK"
DEADLINE=$((SECONDS + 1200))
while (( SECONDS < DEADLINE )); do
  RESULT=$(aws ecs describe-tasks --cluster "$ECS_CLUSTER" --tasks "$TASK" --region "$AWS_REGION" --output json)
  STATE=$(jq -r '.tasks[0].lastStatus // empty' <<< "$RESULT")
  if [[ "$STATE" == STOPPED ]]; then
    EXIT_CODE=$(jq -r --arg name "$CONTAINER" '.tasks[0].containers[] | select(.name == $name) | .exitCode // "missing"' <<< "$RESULT")
    if [[ "$EXIT_CODE" == 0 ]]; then
      echo 'Migración completada; la imagen puede activarse.'
      exit 0
    fi
    echo "::error::Migración fallida (exit=$EXIT_CODE); no se activará la imagen." >&2
    exit 1
  fi
  if [[ -z "$STATE" ]]; then echo '::error::ECS no devolvió el estado de la tarea de migración.' >&2; exit 1; fi
  sleep 10
done
echo '::error::Timeout esperando migración; no se activará la imagen.' >&2
exit 1
