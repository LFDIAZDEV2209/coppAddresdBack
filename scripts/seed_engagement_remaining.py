"""Completa health_scores, transformation_scores y vital_signs para los 32 pacientes.
Usa psycopg con batch (una conexión, sin docker exec por fila). Idempotente."""

import random
import uuid
from datetime import date, datetime, timedelta, timezone

import psycopg

DSN = "host=127.0.0.1 port=5432 dbname=coppaddresd user=app_user password=CoppAddresdDev!2026"
RANDOM = random.Random(7)
NOW = datetime.now(timezone.utc)


def main() -> None:
    with psycopg.connect(DSN) as conn:
        with conn.cursor() as cur:
            cur.execute(
                "SELECT id FROM app.patient_profiles "
                "WHERE user_id IS NOT NULL AND status ILIKE 'activ%' ORDER BY first_name;"
            )
            patients = [r[0] for r in cur.fetchall()]
            print(f"Pacientes: {len(patients)}")

            period_end = date.today() - timedelta(days=1)
            period_start = period_end - timedelta(days=6)

            # Health scores (solo faltantes)
            cur.execute("SELECT patient_id FROM app.health_scores;")
            with_hs = {r[0] for r in cur.fetchall()}
            hs_rows = []
            for i, pid in enumerate(patients):
                if pid in with_hs:
                    continue
                hs_rows.append(
                    (
                        uuid.uuid4(),
                        pid,
                        45 + (i % 40),
                        40 + (i % 40),
                        30 + (i % 50),
                        55 + (i % 30),
                        25 + (i % 60),
                        50 + (i % 35),
                        35 + (i % 45),
                        "up" if i % 2 == 0 else "stable",
                        period_start,
                        period_end,
                        NOW,
                    )
                )
            if hs_rows:
                cur.executemany(
                    "INSERT INTO app.health_scores (id, patient_id, score, score_previous, "
                    "score_adherence, score_clinical, score_nutrition, score_psychology, "
                    "score_exercise, trend, period_start, period_end, calculated_at, created_at, updated_at) "
                    "VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s);",
                    [
                        (
                            r[0],
                            r[1],
                            r[2],
                            r[3],
                            r[4],
                            r[5],
                            r[6],
                            r[7],
                            r[8],
                            r[9],
                            r[10],
                            r[11],
                            r[12],
                            r[12],
                            r[12],
                        )
                        for r in hs_rows
                    ],
                )
            print(f"Health scores nuevos: {len(hs_rows)}")

            # Transformation scores (solo faltantes)
            cur.execute("SELECT patient_id FROM app.transformation_scores;")
            with_ts = {r[0] for r in cur.fetchall()}
            ts_rows = []
            for i, pid in enumerate(patients):
                if pid in with_ts:
                    continue
                ts_rows.append(
                    (
                        uuid.uuid4(),
                        pid,
                        50 + (i % 30),
                        45 + (i % 30),
                        1 + (i % 4),
                        "up" if i % 2 == 0 else "stable",
                        NOW,
                    )
                )
            if ts_rows:
                cur.executemany(
                    "INSERT INTO app.transformation_scores (id, patient_id, score, score_previous, "
                    "week_number, detail, overall_trend, calculated_at, created_at, updated_at) "
                    "VALUES (%s, %s, %s, %s, %s, '{}'::jsonb, %s, %s, %s, %s);",
                    [
                        (r[0], r[1], r[2], r[3], r[4], r[5], r[6], r[6], r[6])
                        for r in ts_rows
                    ],
                )
            print(f"Transformation scores nuevos: {len(ts_rows)}")

            # Vital signs: asegurar >= 3 mediciones recientes por paciente
            cur.execute(
                "SELECT patient_id, count(*) FROM app.vital_signs "
                "WHERE measured_at > now() - interval '14 days' GROUP BY patient_id;"
            )
            recent = {r[0]: r[1] for r in cur.fetchall()}
            vs_rows = []
            for i, pid in enumerate(patients):
                needed = 3 - recent.get(pid, 0)
                for d in range(1, needed + 1):
                    measured = NOW - timedelta(days=d, hours=(i % 6))
                    systolic = 115 + (i % 4) * 8 + RANDOM.randint(-3, 3)
                    vs_rows.append(
                        (
                            uuid.uuid4(),
                            pid,
                            measured,
                            systolic,
                            systolic - 40 + RANDOM.randint(-2, 2),
                            60 + (i % 4) * 5 + RANDOM.randint(0, 5),
                            round(36.5 + RANDOM.uniform(0, 0.6), 1),
                            95 + RANDOM.randint(0, 3),
                            158 + (i % 6),
                            round(58 + (i % 25) + RANDOM.uniform(0, 3), 1),
                        )
                    )
            if vs_rows:
                cur.executemany(
                    "INSERT INTO app.vital_signs (id, patient_id, measured_at, systolic, diastolic, "
                    "heart_rate, temperature_c, o2_saturation, height_cm, weight_kg, created_at) "
                    "VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s);",
                    [(*r, r[2]) for r in vs_rows],
                )
            print(f"Vital signs nuevos: {len(vs_rows)}")

        conn.commit()

    with psycopg.connect(DSN) as conn:
        with conn.cursor() as cur:
            cur.execute(
                "SELECT 'health_scores='||count(*) FROM app.health_scores "
                "UNION ALL SELECT 'transformation='||count(*) FROM app.transformation_scores "
                "UNION ALL SELECT 'vitals='||count(*) FROM app.vital_signs "
                "UNION ALL SELECT 'notifications='||count(*) FROM app.notifications;"
            )
            for r in cur.fetchall():
                print(r[0])


if __name__ == "__main__":
    main()
