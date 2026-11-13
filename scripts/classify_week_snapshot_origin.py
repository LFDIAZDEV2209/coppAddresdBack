#!/usr/bin/env python3
"""Clasifica snapshots futuros. Por defecto simula; usa PG* sin imprimir credenciales.

Requiere psycopg2. --mode apply/reverse son acciones explícitas; reverse también
puede simularse con --mode simulate --reverse. No copia asignaciones antiguas:
su relleno y ejecución se coordinan en Fase 2.10.
"""
import argparse
from collections import Counter
from copy import deepcopy
from datetime import date
import json
import sys

import psycopg2
from psycopg2.extras import RealDictCursor, execute_values

VERSION = 1
MARKER = "_origin_backfill_v1"
FIELDS = ("weekday", "task_code", "points", "sort_order", "routine_id", "nutrition_plan_id", "media_id")


def classify(item, week, templates):
    """La ausencia de prueba histórica nunca autoriza convertir en programa."""
    code = item.get("task_code")
    if (code == "ejercicio" and week["legacy_routine"]) or (code == "nut" and week["legacy_nutrition"]):
        return "individual", "legacy_humano_o_ambiguo"
    template = templates.get(str(week["template_id"]))
    if not template or not week["template_version_at_start"] or template["version"] != week["template_version_at_start"]:
        return "individual", "sin_version_historica"
    # El catálogo no conserva revisiones antiguas. Si se editó después de crear
    # la semana, ni siquiera la igualdad de versión prueba el contenido original.
    if template["updated_at"] and template["updated_at"] > week["created_at"]:
        return "individual", "plantilla_editada_sin_revision_historica"
    matches = [row for row in template["days"] if row["weekday"] == item.get("weekday")
               and row["task_code"] == code and row["week_number"] in (None, week["week_number"])]
    matches.sort(key=lambda row: row["week_number"] is not None, reverse=True)
    if not matches or any(key not in item for key in FIELDS[:4]) or item.get("rest") is True:
        return "individual", "no_comparable"
    return ("program", "igual_plantilla") if all(item.get(key) == matches[0].get(key) for key in FIELDS) else ("individual", "distinto_plantilla")


def transform(week, templates, reverse, counts):
    original = week["tasks_snapshot"]
    if not isinstance(original, list):
        counts["conflicto_snapshot_no_array"] += 1
        return original
    if any(not isinstance(item, dict) or not isinstance(item.get("weekday"), int) or not isinstance(item.get("task_code"), str) for item in original):
        counts["conflicto_items_invalidos_o_duplicados"] += 1
        return original
    if not original:
        counts["semanas_vacias"] += 1
    keys = [(item.get("weekday"), item.get("task_code")) for item in original if isinstance(item, dict)]
    if len(keys) != len(original) or len(set(keys)) != len(keys):
        counts["conflicto_items_invalidos_o_duplicados"] += 1
        return original
    result = deepcopy(original)
    for index, item in enumerate(result):
        if reverse:
            backup = item.get(MARKER)
            if not isinstance(backup, dict) or item.get("backfill_version") != VERSION:
                counts["items_sin_reversa"] += 1
                continue
            before = backup.get("before")
            if not isinstance(before, dict):
                counts["conflicto_reversa_invalida"] += 1
                continue
            expected = dict(before, origin=backup.get("origin"), backfill_version=VERSION)
            if {key: value for key, value in item.items() if key != MARKER} != expected:
                counts["conflicto_editado_despues"] += 1
                continue
            result[index] = before
            counts["items_revertidos"] += 1
        else:
            if item.get("origin") or item.get("backfill_version") is not None or MARKER in item:
                counts["items_ya_clasificados"] += 1
                continue
            origin, reason = classify(item, week, templates)
            before = deepcopy(item)
            item.update(origin=origin, backfill_version=VERSION)
            item[MARKER] = {"before": before, "origin": origin}
            counts["items_" + origin] += 1
            counts["motivo_" + reason] += 1
            if origin == "individual" and reason not in ("distinto_plantilla",):
                counts["conflictos_conservados_individual"] += 1
    return result


def run(args):
    counts = Counter({key: 0 for key in ("semanas_con_cambios", "semanas_escritas", "items_program", "items_individual", "conflictos_conservados_individual", "conflicto_concurrencia")})
    with psycopg2.connect("") as connection:
        simulation = args.mode == "simulate"
        connection.set_session(readonly=simulation)
        with connection.cursor(cursor_factory=RealDictCursor) as cursor:
            cursor.execute("SET statement_timeout = '60s'; SET lock_timeout = '5s'")
            cursor.execute("SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='app' AND table_name='weekly_day_templates' AND column_name='week_number') AS present")
            week_column = "week_number" if cursor.fetchone()["present"] else "NULL::integer"
            cursor.execute("SELECT id::text, version, updated_at FROM app.program_templates")
            templates = {row["id"]: dict(row, days=[]) for row in cursor.fetchall()}
            cursor.execute(f"SELECT template_id::text, {week_column} AS week_number, weekday, task_code, points, sort_order, routine_id::text, nutrition_plan_id::text, media_id::text FROM app.weekly_day_templates")
            for row in cursor.fetchall():
                if row["template_id"] in templates:
                    templates[row["template_id"]]["days"].append(row)
            cursor.execute("SELECT count(*) AS total FROM app.program_weeks")
            counts["semanas_totales"] = cursor.fetchone()["total"]
        connection.commit()
        after = "00000000-0000-0000-0000-000000000000"
        while True:
            with connection.cursor(cursor_factory=RealDictCursor) as cursor:
                cursor.execute("""
                    SELECT w.*, e.template_id,
                      EXISTS (SELECT 1 FROM app.routine_assignments a WHERE a.patient_id=e.patient_id
                        AND a.start_date <= w.week_end_date_local AND (a.end_date IS NULL OR a.end_date >= w.week_start_date_local)) AS legacy_routine,
                      EXISTS (SELECT 1 FROM app.nutrition_plan_assignments a WHERE a.patient_id=e.patient_id
                        AND a.start_date <= w.week_end_date_local AND (a.end_date IS NULL OR a.end_date >= w.week_start_date_local)) AS legacy_nutrition
                    FROM app.program_weeks w JOIN app.program_enrollments e ON e.id=w.enrollment_id
                    WHERE w.id > %s::uuid AND w.status='Locked' AND w.activated_at IS NULL AND w.completed_at IS NULL
                      AND w.week_start_date_local > COALESCE(%s::date, (CURRENT_TIMESTAMP AT TIME ZONE e.timezone)::date)
                    ORDER BY w.id LIMIT %s
                    """, (after, args.today, args.batch_size))
                weeks = cursor.fetchall()
                if not weeks:
                    break
                updates = []
                for week in weeks:
                    counts["semanas_futuras_evaluadas"] += 1
                    changed = transform(week, templates, args.reverse or args.mode == "reverse", counts)
                    if changed == week["tasks_snapshot"]:
                        continue
                    counts["semanas_con_cambios"] += 1
                    if not simulation:
                        # Comparación optimista y estado/fecha revalidados en la escritura:
                        # una activación o edición concurrente nunca se sobrescribe.
                        updates.append((json.dumps(changed), str(week["id"]), json.dumps(week["tasks_snapshot"])))
                if updates:
                    execute_values(cursor, """
                        UPDATE app.program_weeks w SET tasks_snapshot=v.snapshot::jsonb
                        FROM app.program_enrollments e, (VALUES %s) AS v(snapshot,id,original)
                        WHERE e.id=w.enrollment_id AND w.id=v.id::uuid
                          AND w.tasks_snapshot=v.original::jsonb AND w.status='Locked'
                          AND w.activated_at IS NULL AND w.completed_at IS NULL
                          AND w.week_start_date_local > (CURRENT_TIMESTAMP AT TIME ZONE e.timezone)::date
                        """, updates, page_size=len(updates))
                    counts["semanas_escritas"] += cursor.rowcount
                    counts["conflicto_concurrencia"] += len(updates) - cursor.rowcount
                after = str(weeks[-1]["id"])
            connection.commit()
    counts["semanas_excluidas_por_estado_o_fecha"] = counts["semanas_totales"] - counts["semanas_futuras_evaluadas"]
    return dict(mode=args.mode, reverse=args.reverse or args.mode == "reverse", version=VERSION,
                escrituras=counts["semanas_escritas"], counts=dict(sorted(counts.items())))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--mode", choices=("simulate", "apply", "reverse"), default="simulate")
    parser.add_argument("--reverse", action="store_true", help="Simular reversa sin escribir")
    parser.add_argument("--today", type=date.fromisoformat, help="Fecha reproducible para pruebas; omitir en operación")
    parser.add_argument("--batch-size", type=int, default=500)
    args = parser.parse_args()
    if not 1 <= args.batch_size <= 5000 or (args.reverse and args.mode != "simulate"):
        parser.error("Lote 1..5000; --reverse solo con simulate")
    try:
        print(json.dumps(run(args), ensure_ascii=False))
    except Exception as error:
        # No volcar excepciones del driver: pueden incluir DSN o datos clínicos.
        print(type(error).__name__ + ":" + str(getattr(error, "pgcode", "")), file=sys.stderr)
        print("Clasificación interrumpida; lote actual revertido. Revisar conexión/esquema sin publicar datos privados.", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
