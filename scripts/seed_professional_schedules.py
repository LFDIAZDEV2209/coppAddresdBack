"""Seed de horarios semanales para profesionales demo (idempotente, FASE 3).

Erp.professional_schedules estaba vacía: el catálogo de agenda y la asignación
de profesional en la APP quedaban sin horarios de atención. Este seed asigna
una franja por día de atención (L-V) a cada profesional con estado Active,
elegida de forma determinista por su id para que el entorno sea reproducible.
Los profesionales Invited no reciben horario (la agenda solo usa activos).

Uso: uv run --with psycopg[binary] python ../coppAddresdBack/scripts/seed_professional_schedules.py
"""

from __future__ import annotations

import psycopg

DB_DSN = "host=localhost port=5432 dbname=coppaddresd user=app_user password=CoppAddresdDev!2026"

# Franjas deterministicas por hash del id del profesional (reproducible).
FRANJAS = [
    ("08:00", "17:00"),
    ("09:00", "18:00"),
    ("07:00", "16:00"),
]

# Turno de sábado solo para roles con fin de semana de guardia (médico/enfermería).
ROLES_CON_SABADO = {"Physician (MD/DO)", "Registered Nurse (RN)"}


def main() -> None:
    with psycopg.connect(DB_DSN) as conn:
        with conn.cursor() as cur:
            cur.execute(
                """
                SELECT p.id, p.professional_type_id, pt.name, p.id::text
                FROM erp.professionals p
                JOIN erp.employees e ON e.id = p.employee_id
                LEFT JOIN erp.professional_types pt ON pt.id = p.professional_type_id
                WHERE e.status = 'Active'
                """
            )
            rows = cur.fetchall()

            # Catálogo existente: no reasignar horarios ya presentes (idempotencia).
            cur.execute(
                "SELECT professional_id, weekday FROM erp.professional_schedules"
            )
            existentes = {(str(pid), wd) for pid, wd in cur.fetchall()}

            insertados = 0
            for professional_id, _type_id, tipo, pid_text in rows:
                franja = FRANJAS[int(pid_text.replace("-", "")[:2], 16) % len(FRANJAS)]
                dias = [1, 2, 3, 4, 5]
                if tipo in ROLES_CON_SABADO:
                    dias.append(6)
                for weekday in dias:
                    if (pid_text, weekday) in existentes:
                        continue
                    cur.execute(
                        "INSERT INTO erp.professional_schedules "
                        "(professional_id, weekday, start_time, end_time) "
                        "VALUES (%s, %s, %s::time, %s::time) "
                        "ON CONFLICT (professional_id, weekday) DO NOTHING",
                        (professional_id, weekday, franja[0], franja[1]),
                    )
                    insertados += cur.rowcount
            conn.commit()
            print(
                f"profesionales activos: {len(rows)}; horarios insertados: {insertados}"
            )


if __name__ == "__main__":
    main()
