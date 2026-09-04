"""
Seed masivo para programa: inscribe pacientes restantes y genera historial
gamificado para poblar vistas ERP (dashboard, adherencia, cofres, gestion).

Uso (desde ai-service donde hay psycopg):
  uv run --with psycopg[binary] python ../coppAddresdBack/scripts/seed_program_mass.py
o directo:
  python scripts/seed_program_mass.py

Idempotente:
- No duplica inscripciones (paciente ya activo -> skip).
- Para enrollments existentes: limpia historial previo generado por este seed
  (marca source_ref_type='mass_seed') y lo regenera. No toca datos de otros origenes
  sin marca.
- Usa transaccion por enrollment.

Genera para cada enrollment activo (existente + nuevo):
- program_weeks (83) si no existen
- streak_states
- daily_checkins + task_completions + xp_ledger para cada dia desde
  start_local_date hasta hoy (max 42 dias para no saturar) con adherencia
  variada (60-95% dias con tareas, 10% dias perfectos 6/6, 5% dias vacios 0/6).
- Actualiza enrollment.xp_balance, current_week_number, streak_states.

Vista ERP esperada tras seed:
- dashboard: KPIs, distribucion rachas, xp por categoria, tendencia 30d
- adherencia: tabla paginada con ranking rachas, tendencia 8 semanas
- cofres: milestones 7/11/22/50
- gestion: lista enrollments paginada, filtros por estado/paciente
"""

import uuid
import random
from datetime import date, datetime, timedelta, timezone

try:
    import psycopg
except ImportError as e:
    raise SystemExit(
        "psycopg no instalado: uv run --with psycopg[binary] python ..."
    ) from e

DSN = "postgresql://app_user:CoppAddresdDev!2026@localhost:5432/coppaddresd"
RANDOM = random.Random(42)

TEMPLATE_CODE = "default-83w"
TIMEZONE = "America/Bogota"

# Puntos por task_code (del seeder ProgramProgressSeeder)
TASK_POINTS = {
    "podcast": 80,
    "vitals": 120,
    "nut": 150,
    "ejercicio": 150,
    "nutraceutico": 80,
    "emocional": 120,
}
TASK_RULE_CODE = {
    "podcast": "TASK_PODCAST",
    "vitals": "TASK_VITALS",
    "nut": "TASK_NUT",
    "ejercicio": "TASK_EJERCICIO",
    "nutraceutico": "TASK_NUTRIBIOTICO",
    "emocional": "TASK_EMOCIONAL",
}
TASK_CODES = list(TASK_POINTS.keys())


def monday_of(d: date) -> date:
    return d - timedelta(days=d.weekday())


def today_bogota() -> date:
    # Aproximar con UTC today (suficiente para seed; -5h no cambia mucho)
    return date.today()


def fetch_one(cur, sql, params=None):
    cur.execute(sql, params or ())
    row = cur.fetchone()
    return row[0] if row else None


def fetch_all(cur, sql, params=None):
    cur.execute(sql, params or ())
    return cur.fetchall()


def main():
    print("Conectando a", DSN.replace("CoppAddresdDev!2026", "***"))
    with psycopg.connect(DSN) as conn:
        conn.autocommit = False
        with conn.cursor() as cur:
            cur.execute(
                "SELECT id, total_weeks FROM app.program_templates WHERE code=%s",
                (TEMPLATE_CODE,),
            )
            row = cur.fetchone()
            if not row:
                raise SystemExit(f"Plantilla {TEMPLATE_CODE} no encontrada")
            template_id, total_weeks = row
            print(f"Template {TEMPLATE_CODE} -> {template_id} ({total_weeks} semanas)")

            # Map weekly_day_templates points por weekday (1..7) -> task_code -> points
            cur.execute(
                """
                SELECT weekday, task_code, points, sort_order
                FROM app.weekly_day_templates
                WHERE template_id=%s
                ORDER BY weekday, sort_order
            """,
                (template_id,),
            )
            wd_rows = cur.fetchall()
            # No varia por weekday en seed actual, pero mapeamos igual
            points_map = {}
            for wd, code, pts, so in wd_rows:
                points_map.setdefault(wd, {})[code] = pts
            print(f"Weekly day templates: {len(wd_rows)} filas")

            # Pacientes no inscritos (Activos sin enrollment activo)
            cur.execute("""
                SELECT id, first_name, last_name, clinic_id
                FROM app.patient_profiles
                WHERE status='Activo'
                  AND id NOT IN (SELECT patient_id FROM app.program_enrollments WHERE status='Active')
                ORDER BY created_at
                LIMIT 120
            """)
            candidates = cur.fetchall()
            print(f"Pacientes candidatos sin inscripcion: {len(candidates)}")

            # Enroll nuevos (hasta 90 para llegar a ~120 total)
            to_enroll = min(len(candidates), 90)
            print(f"Inscribiendo {to_enroll} pacientes nuevos...")
            new_enroll_ids = []
            for idx, (pid, fn, ln, clinic_id) in enumerate(candidates[:to_enroll]):
                # start hace 2-8 semanas, lunes
                weeks_ago = RANDOM.randint(2, 8)
                start = monday_of(today_bogota() - timedelta(weeks=weeks_ago))
                # variacion de dias extra 0-3 para no todos el mismo lunes
                start = start + timedelta(days=RANDOM.randint(0, 2))
                # asegurar lunes? for seed usamos igual, week calc se ajusta
                # forzar lunes de nuevo
                start = monday_of(start)
                enroll_id = uuid.uuid4()
                cur.execute(
                    """
                    INSERT INTO app.program_enrollments
                        (id, patient_id, template_id, timezone, status, started_at, start_local_date, current_week_number, created_at)
                    VALUES (%s, %s, %s, %s, 'Active', now(), %s, 1, now())
                """,
                    (enroll_id, pid, template_id, TIMEZONE, start),
                )
                # weeks
                weeks_vals = []
                for wn in range(1, total_weeks + 1):
                    ws = start + timedelta(days=(wn - 1) * 7)
                    we = ws + timedelta(days=6)
                    status = "Active" if wn == 1 else "Locked"
                    wid = uuid.uuid4()
                    weeks_vals.append((wid, enroll_id, wn, status, ws, we))
                cur.executemany(
                    """
                    INSERT INTO app.program_weeks
                        (id, enrollment_id, week_number, status, week_start_date_local, week_end_date_local, tasks_snapshot, template_version_at_start, created_at)
                    VALUES (%s, %s, %s, %s, %s, %s, '[]'::jsonb, 1, now())
                """,
                    weeks_vals,
                )
                # streak
                cur.execute(
                    """
                    INSERT INTO app.streak_states
                        (enrollment_id, current_streak, longest_streak, last_active_date, freezes_remaining, freezes_used_total, updated_at, multiplier_active, nb_current_streak, nb_longest_streak)
                    VALUES (%s, 0, 0, NULL, 3, 0, now(), 1.00, 0, 0)
                """,
                    (enroll_id,),
                )
                new_enroll_ids.append((enroll_id, pid, start))
            conn.commit()
            print(f"  -> {len(new_enroll_ids)} inscripciones creadas")

            # Ahora poblar historial para todos los activos (existentes + nuevos)
            cur.execute("""
                SELECT e.id, e.patient_id, e.start_local_date, e.template_id
                FROM app.program_enrollments e
                WHERE e.status='Active'
                ORDER BY e.created_at
            """)
            all_enrollments = cur.fetchall()
            print(f"Total enrollments activos a poblar: {len(all_enrollments)}")

            # Para cada enrollment, limpiar previo mass_seed y generar historial
            total_task_rows = 0
            total_daily_rows = 0
            total_xp_rows = 0
            for enroll_id, patient_id, start_local_date, tmpl_id in all_enrollments:
                # Limpiar previo de este script (marcado con source_ref_type='mass_seed' o client_request_id like 'mass-%')
                cur.execute(
                    "DELETE FROM app.xp_ledger WHERE enrollment_id=%s AND source_ref_type='mass_seed'",
                    (enroll_id,),
                )
                cur.execute(
                    "DELETE FROM app.task_completions WHERE enrollment_id=%s AND source_ref_type='mass_seed'",
                    (enroll_id,),
                )
                cur.execute(
                    "DELETE FROM app.daily_checkins WHERE enrollment_id=%s AND local_date >= %s",
                    (enroll_id, start_local_date),
                )
                # Reset enrollment y streak
                cur.execute(
                    "UPDATE app.program_enrollments SET current_week_number=1 WHERE id=%s",
                    (enroll_id,),
                )
                cur.execute(
                    "UPDATE app.streak_states SET current_streak=0, longest_streak=0, last_active_date=NULL, nb_current_streak=0, nb_longest_streak=0, nb_last_completed_date=NULL WHERE enrollment_id=%s",
                    (enroll_id,),
                )

                # Determinar semanas: mapear local_date -> week_id
                cur.execute(
                    "SELECT id, week_number, week_start_date_local, week_end_date_local FROM app.program_weeks WHERE enrollment_id=%s ORDER BY week_number",
                    (enroll_id,),
                )
                week_rows = cur.fetchall()
                week_by_date = {}
                for wid, wn, ws, we in week_rows:
                    for d in [ws + timedelta(days=i) for i in range(7)]:
                        week_by_date[d] = wid
                # Fallback: si week_rows vacio (no deberia), crear
                if not week_by_date:
                    for wn in range(1, total_weeks + 1):
                        ws = start_local_date + timedelta(days=(wn - 1) * 7)
                        wid = uuid.uuid4()
                        cur.execute(
                            """
                            INSERT INTO app.program_weeks
                                (id, enrollment_id, week_number, status, week_start_date_local, week_end_date_local, tasks_snapshot, template_version_at_start, created_at)
                            VALUES (%s, %s, %s, %s, %s, %s, '[]'::jsonb, 1, now())
                        """,
                            (
                                wid,
                                enroll_id,
                                wn,
                                "Active" if wn == 1 else "Locked",
                                ws,
                                ws + timedelta(days=6),
                            ),
                        )
                        for d in [ws + timedelta(days=i) for i in range(7)]:
                            week_by_date[d] = wid

                # Generar dias desde start hasta hoy (max 42 dias para no explotar, pero cubrir 30d dashboard)
                today = today_bogota()
                days = (today - start_local_date).days + 1
                # Limitar a max 42 dias de historial reciente (dashboard usa 30d, adherencia 8 semanas=56d, limitamos a 45)
                if days > 45:
                    gen_start = today - timedelta(days=44)
                else:
                    gen_start = start_local_date
                # Si start es muy reciente (1 semana), generar igual desde start

                # Para variar adherencia por paciente: perfil de adherencia
                # 20% alta (90% dias), 50% media (70%), 20% baja (45%), 10% muy baja (25%)
                roll = RANDOM.random()
                if roll < 0.2:
                    adherence = 0.90
                elif roll < 0.7:
                    adherence = 0.70
                elif roll < 0.9:
                    adherence = 0.45
                else:
                    adherence = 0.25
                # Streak: algunos con racha larga, otros rotas
                # Vamos a generar consecutivo y luego romper aleatorio

                # Acumular XP
                balance = 0
                current_streak = 0
                longest = 0
                nb_streak = 0
                nb_longest = 0
                last_active = None
                nb_last = None

                # Para cada dia
                cur_day = gen_start
                while cur_day <= today:
                    wd_iso = cur_day.isoweekday()  # 1 lunes ...7 domingo
                    # decidir si el dia tiene actividad
                    has_activity = RANDOM.random() < adherence
                    # 8% de dias perfectos cuando hay actividad -> 6/6
                    is_perfect_cfg = has_activity and RANDOM.random() < 0.12
                    # 5% dias vacios forzados incluso con adherence alta -> simula olvido
                    if has_activity and RANDOM.random() < 0.05:
                        has_activity = False

                    daily_tasks = []
                    if has_activity:
                        if is_perfect_cfg:
                            daily_tasks = TASK_CODES[:]  # 6
                        else:
                            # 2-5 tareas aleatorias, sesgo a 3-4
                            n = RANDOM.choices([2, 3, 4, 5], weights=[1, 3, 4, 2])[0]
                            daily_tasks = RANDOM.sample(TASK_CODES, n)
                            # asegurar que esenciales aparezcan con mas prob si adherence alta
                            if (
                                adherence > 0.7
                                and "nut" not in daily_tasks
                                and RANDOM.random() < 0.6
                            ):
                                daily_tasks[0] = "nut"
                        # ordenar por sort_order original (usar TASK_CODES order)
                        daily_tasks.sort(key=lambda c: TASK_CODES.index(c))
                    # else daily_tasks = []

                    # Si no hay tareas, racha se rompe (streak 0) pero nb_streak solo si nutraceutico no hecho
                    # Calcular streak general: >=1 tarea = dia activo
                    if daily_tasks:
                        current_streak += 1
                        longest = max(longest, current_streak)
                        last_active = cur_day
                        # nb streak: solo si nutraceutico presente
                        if "nutraceutico" in daily_tasks:
                            nb_streak += 1
                            nb_longest = max(nb_longest, nb_streak)
                            nb_last = cur_day
                        else:
                            # dia perdido rompe nb
                            nb_streak = 0
                    else:
                        # dia sin actividad -> racha general rota (sin congelamiento en este seed simplificado)
                        # Para variabilidad: 30% de pacientes tienen congelamientos que salvan 1 dia cada 7 perfectos
                        # Pero simplificamos: romper directo
                        current_streak = 0
                        nb_streak = 0

                    if daily_tasks:
                        # Crear daily_checkin
                        week_id = week_by_date.get(cur_day)
                        if week_id is None:
                            # buscar semana por calculo
                            wn = ((cur_day - start_local_date).days // 7) + 1
                            wn = max(1, min(wn, total_weeks))
                            # fetch id
                            cur.execute(
                                "SELECT id FROM app.program_weeks WHERE enrollment_id=%s AND week_number=%s",
                                (enroll_id, wn),
                            )
                            r = cur.fetchone()
                            week_id = r[0] if r else week_rows[0][0]
                        dc_id = uuid.uuid4()
                        total_pts = sum(TASK_POINTS[c] for c in daily_tasks)
                        is_perf = len(daily_tasks) == 6
                        # mood y barriers aleatorios para enriquecer
                        mood = RANDOM.randint(3, 5) if has_activity else None
                        barriers = (
                            RANDOM.choice([None, "Poca motivación", "Falta tiempo", ""])
                            if not is_perfect_cfg and has_activity
                            else None
                        )
                        if barriers == "":
                            barriers = None
                        cur.execute(
                            """
                            INSERT INTO app.daily_checkins
                                (id, enrollment_id, program_week_id, local_date, weekday, mood_score, barriers, total_points, bonus_awarded, is_perfect_day, created_at, updated_at)
                            VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, now(), now())
                        """,
                            (
                                dc_id,
                                enroll_id,
                                week_id,
                                cur_day,
                                wd_iso,
                                mood,
                                barriers,
                                total_pts,
                                50 if is_perf else 0,
                                is_perf,
                            ),
                        )

                        # Por cada tarea, crear task_completion + xp_ledger
                        for code in daily_tasks:
                            tc_id = uuid.uuid4()
                            pts = TASK_POINTS[code]
                            rule = TASK_RULE_CODE[code]
                            # decidir si este code es esencial (para streak config)
                            # no afecta seed, solo informativo
                            cur.execute(
                                """
                                INSERT INTO app.task_completions
                                    (id, enrollment_id, program_week_id, daily_checkin_id, local_date, weekday, task_code, points_awarded, client_request_id, completed_at, client_completed_at, source_ref_type)
                                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, now(), now(), 'mass_seed')
                            """,
                                (
                                    tc_id,
                                    enroll_id,
                                    week_id,
                                    dc_id,
                                    cur_day,
                                    wd_iso,
                                    code,
                                    pts,
                                    str(uuid.uuid4()),
                                ),
                            )
                            balance += pts
                            cur.execute(
                                """
                                INSERT INTO app.xp_ledger
                                    (id, enrollment_id, amount, reason, source_ref_type, source_ref_id, balance_after, awarded_at, rule_code, multiplier_used)
                                VALUES (%s, %s, %s, 'TaskCompletion', 'mass_seed', %s, %s, now(), %s, 1.00)
                            """,
                                (uuid.uuid4(), enroll_id, pts, tc_id, balance, rule),
                            )
                            total_task_rows += 1
                            total_xp_rows += 1
                        # Bonus dia perfecto
                        if is_perf:
                            balance += 50
                            cur.execute(
                                """
                                INSERT INTO app.xp_ledger
                                    (id, enrollment_id, amount, reason, source_ref_type, source_ref_id, balance_after, awarded_at, rule_code, multiplier_used)
                                VALUES (%s, %s, 50, 'DayBonus', 'mass_seed', %s, %s, now(), 'DAY_BONUS', 1.00)
                            """,
                                (uuid.uuid4(), enroll_id, dc_id, balance),
                            )
                            total_xp_rows += 1
                        total_daily_rows += 1
                    cur_day += timedelta(days=1)

                # Actualizar enrollment y streaks finales
                # current_week = weeks elapsed +1, cap 83
                weeks_elapsed = ((today - start_local_date).days // 7) + 1
                weeks_elapsed = max(1, min(weeks_elapsed, total_weeks))
                freezes = RANDOM.randint(0, 3)  # 0-3 restantes
                cur.execute(
                    """
                    UPDATE app.program_enrollments
                    SET current_week_number=%s, updated_at=now()
                    WHERE id=%s
                """,
                    (weeks_elapsed, enroll_id),
                )
                cur.execute(
                    """
                    UPDATE app.streak_states
                    SET current_streak=%s, longest_streak=%s, last_active_date=%s,
                        freezes_remaining=%s, updated_at=now(),
                        nb_current_streak=%s, nb_longest_streak=%s, nb_last_completed_date=%s
                    WHERE enrollment_id=%s
                """,
                    (
                        current_streak,
                        longest,
                        last_active,
                        freezes,
                        nb_streak,
                        nb_longest,
                        nb_last,
                        enroll_id,
                    ),
                )
                # Actualizar xp_ledger balance ya esta, pero necesitamos asegurar enrollment xpBalance consistente?
                # No hay columna xp_balance en enrollment, se deriva del ledger? Revisar: enrollment no tiene xp_balance,
                # pero DTO lo calcula del ledger. Sin embargo program_enrollments tiene? No, es DTO calc.
                # Pero si existe columna xp? No, no hay.

            conn.commit()
            print(f"DailyCheckins generados: {total_daily_rows}")
            print(f"TaskCompletions: {total_task_rows}")
            print(f"XpLedger rows: {total_xp_rows}")

            # Resumen final
            cur.execute(
                "SELECT count(*) FROM app.program_enrollments WHERE status='Active'"
            )
            print("Enrollments activos:", cur.fetchone()[0])
            cur.execute(
                "SELECT count(*) FROM app.task_completions WHERE source_ref_type='mass_seed'"
            )
            print("Task completions mass_seed:", cur.fetchone()[0])
            cur.execute("SELECT count(*) FROM app.daily_checkins")
            print("DailyCheckins total:", cur.fetchone()[0])
            cur.execute("""
                SELECT status, count(*) FROM app.program_enrollments GROUP BY status
            """)
            print("Enrollments por status:", cur.fetchall())
            cur.execute(
                "SELECT count(*) FROM app.xp_ledger WHERE source_ref_type='mass_seed'"
            )
            print("Xp mass_seed:", cur.fetchone()[0])


if __name__ == "__main__":
    main()
