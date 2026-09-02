"""Asigna la batería inicial ANTARES a TODOS los pacientes que no la tengan.

Replica la lógica de AutoAssignInitialBatteryCommand/AssignBatteryCommand del
backend (battery_assignment + un assignment por instrumento con la versión
activa vigente), para que la batería sea el estándar de todos los pacientes
existentes — no solo de los creados después de activar el auto-assign.

Uso (desde ai-service, donde hay venv/uv):
    uv run --with psycopg[binary] python ../coppAddresdBack/scripts/assign_initial_battery.py

Idempotente: solo crea filas para pacientes sin battery_assignment de la
batería 'bateria-inicial'. No borra ni toca asignaciones/evaluaciones previas.
"""

from __future__ import annotations

import uuid
from datetime import datetime, timezone

import psycopg

DB_DSN = "host=localhost port=5432 dbname=coppaddresd user=app_user password=CoppAddresdDev!2026"

BATTERY_CODE = "bateria-inicial"


def main() -> None:
    now = datetime.now(timezone.utc)
    with psycopg.connect(DB_DSN) as conn:
        with conn.cursor() as cur:
            battery = cur.execute(
                "SELECT id, name FROM app.health_test_batteries WHERE code = %s",
                (BATTERY_CODE,),
            ).fetchone()
            if battery is None:
                raise SystemExit(f"Batería '{BATTERY_CODE}' no encontrada.")
            battery_id, battery_name = battery

            # Instrumentos de la batería con su versión activa vigente.
            items = cur.execute(
                """
                SELECT bi.instrument_id, v.id
                FROM app.health_test_battery_items bi
                JOIN app.health_test_versions v
                  ON v.instrument_id = bi.instrument_id
                 AND v.is_current = true
                 AND v.status = 'active'
                WHERE bi.battery_id = %s
                ORDER BY bi.sort_order
                """,
                (battery_id,),
            ).fetchall()
            if not items:
                raise SystemExit("La batería no tiene instrumentos con versión activa.")

            patients = cur.execute(
                """
                SELECT pp.id
                FROM app.patient_profiles pp
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM app.health_test_battery_assignments ba
                    WHERE ba.patient_id = pp.id AND ba.battery_id = %s
                )
                """,
                (battery_id,),
            ).fetchall()

            created_batteries = 0
            created_assignments = 0
            for (patient_id,) in patients:
                battery_assignment_id = uuid.uuid4()
                cur.execute(
                    """
                    INSERT INTO app.health_test_battery_assignments
                        (id, patient_id, battery_id, status, assigned_by, assigned_at)
                    VALUES (%s, %s, %s, 'pending', NULL, %s)
                    """,
                    (battery_assignment_id, patient_id, battery_id, now),
                )
                created_batteries += 1
                for instrument_id, version_id in items:
                    cur.execute(
                        """
                        INSERT INTO app.health_test_assignments
                            (id, patient_id, battery_assignment_id, version_id, status, assigned_by, assigned_at)
                        VALUES (%s, %s, %s, %s, 'pending', NULL, %s)
                        """,
                        (
                            uuid.uuid4(),
                            patient_id,
                            battery_assignment_id,
                            version_id,
                            now,
                        ),
                    )
                    created_assignments += 1

            print(
                f"Batería '{battery_name}' ({len(items)} tests): "
                f"{created_batteries} pacientes asignados, {created_assignments} asignaciones creadas."
            )


if __name__ == "__main__":
    main()
