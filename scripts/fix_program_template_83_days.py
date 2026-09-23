"""Corrección del programa: 83 DÍAS (12 semanas), no 83 semanas (idempotente).

Los 160 enrollments demo apuntaban a la plantilla `default-83w` (83 semanas)
por un fallback erróneo en el seeder/controller. El programa inicial y principal
de todos los pacientes es `program-coppaddresd-83-days` (12 semanas).

Qué hace:
1. Re-apunta `app.program_enrollments.template_id` a la plantilla de 83 días.
2. Elimina las filas `program_weeks` con week_number > 12 de esas inscripciones
   (en demo no tienen task_completions/daily_checkins asociados — verificado).
3. Recorta `current_week_number` a 12 por seguridad (en demo nadie pasa de 9).

Las semanas 1..12 conservan sus fechas y el snapshot de la semana 1 (ambas
plantillas comparten el mismo contenido diario: 42 weekly_day_templates con
los mismos TaskSeeds). El progreso gamificado (xp_ledger, streak_states,
task_completions, daily_checkins) queda intacto porque vive por enrollment_id.

Uso: uv run --with psycopg[binary] python ../coppAddresdBack/scripts/fix_program_template_83_days.py
"""

from __future__ import annotations

import psycopg

DB_DSN = "host=localhost port=5432 dbname=coppaddresd user=app_user password=CoppAddresdDev!2026"
NEW_CODE = "program-coppaddresd-83-days"
OLD_CODE = "default-83w"


def main() -> None:
    with psycopg.connect(DB_DSN) as conn:
        with conn.cursor() as cur:
            cur.execute(
                "SELECT id, code FROM app.program_templates WHERE code IN (%s, %s)",
                (NEW_CODE, OLD_CODE),
            )
            ids = {code: str(tid) for tid, code in cur.fetchall()}
            if NEW_CODE not in ids:
                raise SystemExit(
                    f"No existe la plantilla {NEW_CODE}: ejecutar la API una vez para sembrarla."
                )

            new_id = ids.get(NEW_CODE)
            old_id = ids.get(OLD_CODE)

            cur.execute(
                "SELECT count(*) FROM app.program_enrollments WHERE template_id = %s",
                (new_id,),
            )
            ya_migrados = cur.fetchone()[0]

            movidos = 0
            if old_id:
                cur.execute(
                    "UPDATE app.program_enrollments SET template_id = %s WHERE template_id = %s",
                    (new_id, old_id),
                )
                movidos = cur.rowcount

            # Semanas sobrantes (> 12) de las inscripciones ya migradas.
            cur.execute(
                "DELETE FROM app.program_weeks pw USING app.program_enrollments pe "
                "WHERE pw.enrollment_id = pe.id AND pe.template_id = %s AND pw.week_number > 12",
                (new_id,),
            )
            semanas_borradas = cur.rowcount

            cur.execute(
                "UPDATE app.program_enrollments SET current_week_number = 12 "
                "WHERE template_id = %s AND current_week_number > 12",
                (new_id,),
            )
            semanas_recortadas = cur.rowcount

            conn.commit()
            print(
                f"migrados {movidos} enrollments a {NEW_CODE} "
                f"(ya migrados: {ya_migrados}); semanas >12 borradas: {semanas_borradas}; "
                f"current_week recortada: {semanas_recortadas}"
            )


if __name__ == "__main__":
    main()
