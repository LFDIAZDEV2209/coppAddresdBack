"""Backfill del histórico de mediciones para pacientes ya creados (idempotente).

Replica SeedMeasurementHistoryCommand (tendencia 12 meses x 14 métricas).
Uso: uv run --with psycopg[binary] python ../coppAddresdBack/scripts/seed_measurements_history.py
"""

from __future__ import annotations

import random
from datetime import datetime, timezone

import psycopg

DB_DSN = "host=localhost port=5432 dbname=coppaddresd user=app_user password=CoppAddresdDev!2026"
SOURCE = "seed-hist"
DOCS = ["77777777", "55551234", "66666666", "88888888", "99999999"]
# Tendencias por métrica: (code, valor_inicio, valor_fin). Unidades = catálogo
# (height en cm: la FASE 3 corrigió el 1.70 histórico que era metros sobre unidad cm).
TRAJ = [
    ("weight", 95, 82),
    ("bmi", 32.9, 28.4),
    ("body_fat", 38, 29),
    ("waist", 112, 97),
    ("hip", 112, 103),
    ("wrist", 17.5, 17.0),
    ("height", 170, 170),
    ("hba1c", 8.2, 6.3),
    ("glucose_fasting", 138, 96),
    ("systolic_bp", 138, 119),
    ("diastolic_bp", 88, 76),
    ("heart_rate", 78, 67),
    ("o2_saturation", 93, 97),
    ("temperature_c", 36.6, 36.6),
]
MONTHS = 12


def month_shift(base: datetime, back: int) -> datetime:
    """Devuelve el día 15 del mes `back` meses ANTES de `base` (sin cruces de año
    erróneos: el cálculo aritmético previo con floors de Python producía meses
    futuros 2027 para desplazamientos negativos — bug corregido en FASE 3)."""
    total = base.year * 12 + (base.month - 1) - back
    return datetime(total // 12, (total % 12) + 1, 15, 10, 0, tzinfo=base.tzinfo)


def main() -> None:
    now = datetime.now(timezone.utc)
    with psycopg.connect(DB_DSN) as conn:
        with conn.cursor() as cur:
            # Reparación FASE 3: los históricos previos guardaron la talla en
            # metros (1.70) con unidad cm y tres meses quedaron en 2027 por el
            # bug de fechas. Se eliminan esas filas del seed para que el re-run
            # las recree correctas (idempotente: solo filas source='seed-hist').
            cur.execute(
                "DELETE FROM app.clinical_measurements cm "
                "USING app.measurement_metrics mm "
                "WHERE cm.metric_id = mm.id AND cm.source = 'seed-hist' "
                "AND (mm.code = 'height' AND cm.value < 3 "
                "     OR cm.observed_at > now())"
            )
            repaired = cur.rowcount
            cur.execute(
                "SELECT code, id, default_unit_id FROM app.measurement_metrics WHERE is_active"
            )
            pairs = cur.fetchall()
            docs = ",".join(f"'{d}'" for d in DOCS)
            cur.execute(
                f"SELECT id, document_number FROM app.patient_profiles WHERE document_number IN ({docs})"
            )
            patients = cur.fetchall()
            total = 0
            for pid, _doc in patients:
                rng = random.Random(pid.int & 0xFFFFFFFF)
                cur.execute(
                    "SELECT mm.code, to_char(cm.observed_at,'YYYY-MM') "
                    "FROM app.clinical_measurements cm "
                    "JOIN app.measurement_metrics mm ON mm.id = cm.metric_id "
                    "WHERE cm.patient_id = %s AND cm.source = 'seed-hist'",
                    (pid,),
                )
                existing = {(code, month) for code, month in cur.fetchall()}
                for code, metric_id, unit_id in pairs:
                    start, end = next((s, e) for c, s, e in TRAJ if c == code)
                    for i in range(MONTHS):
                        offset = MONTHS - 1 - i
                        observed = month_shift(now, offset)
                        if (code, observed.strftime("%Y-%m")) in existing:
                            continue
                        progress = i / (MONTHS - 1)
                        noise = 1 + rng.uniform(-0.01, 0.01)
                        value = round((start - (start - end) * progress) * noise, 1)
                        sk = f"seed-hist:{pid}:{code}:{observed.strftime('%Y-%m')}"
                        cur.execute(
                            "INSERT INTO app.clinical_measurements "
                            "(id, patient_id, metric_id, value, unit_id, observed_at, recorded_at, source, source_key) "
                            "VALUES (gen_random_uuid(), %s, %s, %s, %s, %s, now(), %s, %s)",
                            (pid, metric_id, value, unit_id, observed, SOURCE, sk),
                        )
                        total += 1
            conn.commit()
            print(
                f"reparadas {repaired} filas; insertadas {total} filas para {len(patients)} pacientes"
            )


if __name__ == "__main__":
    main()
