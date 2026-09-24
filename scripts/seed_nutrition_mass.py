"""
Seed masivo de nutricion clinica y sincronizacion con el Programa de 83 Semanas.

Objetivos:
1. Actualizar metas de macronutrientes en planes (daily_protein_target, daily_carbs_target,
   daily_fat_target, daily_fiber_target, meal_timing, allergens) en templates y planes existentes.
2. Inscribir a cualquier paciente activo pendiente (ej. 88888888 Test E2E) en el programa
   default-83w con sus 83 semanas y streak inicial.
3. Asignar planes clinicos vigentes en app.nutrition_plan_assignments para TODOS los pacientes activos:
   - 55551234 (Luis Prueba Movil) -> Obesidad - Deficit calorico (1400 kcal)
   - 77777777 (Play Wright)       -> Hipertension - DASH (1800 kcal)
   - 88888888 (Test E2E)          -> Diabetes Tipo 2 (1600 kcal)
   - 1012345678 (Andrea Salazar)  -> Diabetes Tipo 2 (1600 kcal)
   - Demas pacientes activos      -> Distribucion ciclica equilibrada.
   - Ventana de fechas: start_date <= hoy, end_date >= hoy + 60 dias (status = 1 Active).
4. Generar historial de ingesta realista en app.habit_checks y app.nutrition_intake_logs:
   - Ultimos 7 dias (2026-09-17 a 2026-09-23): 3-4 comidas registradas + hidratacion (7-8 vasos),
     para poblar barras de progreso de macros, adherencia semanal y tendencias en App y ERP.
   - Hoy (2026-09-24): Desayuno y Almuerzo registrados + hidratacion parcial (1000-1250 ml / 4-5 vasos),
     dejando Merienda y Cena pendientes para pruebas interactivas de logging manual y foto IA en la app.
   - Source: 'manual' y 'ai_photo' para verificar tags en UI.

Idempotencia:
- Marca los registros generados con source = 'mass_seed' (o 'ai_photo_seed' / 'manual_seed').
- Limpia logs previos generados por este script antes de repoblar.
- No duplica enrollments ni assignments activos.

Ejecucion:
  uv run --with psycopg[binary] python coppAddresdBack/scripts/seed_nutrition_mass.py
"""

import sys
import uuid
import random
from datetime import date, datetime, timedelta, timezone
from decimal import Decimal

try:
    import psycopg
except ImportError as e:
    raise SystemExit(
        "psycopg no instalado: uv run --with psycopg[binary] python ..."
    ) from e

DSN = "postgresql://app_user:CoppAddresdDev!2026@localhost:5432/coppaddresd"
RANDOM = random.Random(2026)

TEMPLATE_CODE = "default-83w"
TIMEZONE = "America/Bogota"

PLAN_METAS = {
    # Plan Hipertension - DASH (5d75167e-5a81-4f22-81d6-91e95b33ab40)
    "hipertension": {
        "daily_calorie_target": 1800,
        "daily_protein_target": Decimal("90.00"),
        "daily_carbs_target": Decimal("220.00"),
        "daily_fat_target": Decimal("45.00"),
        "daily_fiber_target": Decimal("30.00"),
        "meal_timing": "07:30, 12:30, 16:30, 19:30",
        "allergens": "Bajo en sodio (<1500mg/dia)",
    },
    # Plan Diabetes Tipo 2 (e61b0d5f-4c09-4246-8d59-5dc1d2d7e249)
    "diabetes": {
        "daily_calorie_target": 1600,
        "daily_protein_target": Decimal("100.00"),
        "daily_carbs_target": Decimal("150.00"),
        "daily_fat_target": Decimal("40.00"),
        "daily_fiber_target": Decimal("28.00"),
        "meal_timing": "07:00, 12:00, 16:00, 19:30",
        "allergens": "Sin azucares anadidos, bajo indice glucemico",
    },
    # Plan Obesidad - Deficit calorico (83c645be-8572-46e2-b6ec-e907841f597c)
    "obesidad": {
        "daily_calorie_target": 1400,
        "daily_protein_target": Decimal("110.00"),
        "daily_carbs_target": Decimal("130.00"),
        "daily_fat_target": Decimal("35.00"),
        "daily_fiber_target": Decimal("25.00"),
        "meal_timing": "08:00, 13:00, 17:00, 20:00",
        "allergens": "Control estricto de porciones, alto en fibra",
    },
}


def monday_of(d: date) -> date:
    return d - timedelta(days=d.weekday())


def main():
    print("=" * 70)
    print("Iniciando Seed de Nutricion Masiva y Sincronizacion de Programa")
    print("=" * 70)

    today = date.today()
    print(f"Fecha de referencia (hoy): {today}")

    with psycopg.connect(DSN) as conn:
        conn.autocommit = False
        with conn.cursor() as cur:
            # -------------------------------------------------------------
            # PASO 1: Actualizar metas de macronutrientes en app.nutrition_plans
            # -------------------------------------------------------------
            print("\n[Paso 1] Actualizando metas de macronutrientes en planes...")

            # Templates principales
            cur.execute("""
                SELECT id, name, target_condition
                FROM app.nutrition_plans
                WHERE is_template = true
            """)
            templates = cur.fetchall()

            template_map = {}
            for tid, tname, tcond in templates:
                cond_norm = (tcond or "").lower()
                key = None
                if "hiper" in cond_norm or "dash" in tname.lower():
                    key = "hipertension"
                elif "diab" in cond_norm or "diabetes" in tname.lower():
                    key = "diabetes"
                elif "obes" in cond_norm or "déficit" in tname.lower() or "deficit" in tname.lower():
                    key = "obesidad"

                if key and key in PLAN_METAS:
                    meta = PLAN_METAS[key]
                    template_map[key] = tid
                    cur.execute("""
                        UPDATE app.nutrition_plans
                        SET daily_calorie_target = %s,
                            daily_protein_target = %s,
                            daily_carbs_target = %s,
                            daily_fat_target = %s,
                            daily_fiber_target = %s,
                            meal_timing = %s,
                            allergens = COALESCE(allergens, %s),
                            updated_at = now()
                        WHERE id = %s
                    """, (
                        meta["daily_calorie_target"],
                        meta["daily_protein_target"],
                        meta["daily_carbs_target"],
                        meta["daily_fat_target"],
                        meta["daily_fiber_target"],
                        meta["meal_timing"],
                        meta["allergens"],
                        tid
                    ))
                    print(f"  -> Template actualizado: '{tname}' ({key}) [Cal: {meta['daily_calorie_target']}, Prot: {meta['daily_protein_target']}g, Carbs: {meta['daily_carbs_target']}g, Fat: {meta['daily_fat_target']}g, Fib: {meta['daily_fiber_target']}g]")

            # Actualizar tambien planes clonados que tengan target_condition
            for key, meta in PLAN_METAS.items():
                cur.execute("""
                    UPDATE app.nutrition_plans
                    SET daily_calorie_target = COALESCE(daily_calorie_target, %s),
                        daily_protein_target = COALESCE(daily_protein_target, %s),
                        daily_carbs_target = COALESCE(daily_carbs_target, %s),
                        daily_fat_target = COALESCE(daily_fat_target, %s),
                        daily_fiber_target = COALESCE(daily_fiber_target, %s),
                        meal_timing = COALESCE(meal_timing, %s),
                        updated_at = now()
                    WHERE is_template = false
                      AND (
                        (lower(target_condition) LIKE %s)
                        OR (source_plan_id = %s)
                      )
                """, (
                    meta["daily_calorie_target"],
                    meta["daily_protein_target"],
                    meta["daily_carbs_target"],
                    meta["daily_fat_target"],
                    meta["daily_fiber_target"],
                    meta["meal_timing"],
                    f"%{key[:4]}%",
                    template_map.get(key)
                ))

            # -------------------------------------------------------------
            # PASO 2: Inscribir en el Programa de 83 Semanas a pacientes activos sin enrollment
            # -------------------------------------------------------------
            print("\n[Paso 2] Verificando inscripciones activas al programa de 83 semanas...")
            cur.execute("SELECT id, total_weeks FROM app.program_templates WHERE code=%s", (TEMPLATE_CODE,))
            row = cur.fetchone()
            if not row:
                raise SystemExit(f"Plantilla {TEMPLATE_CODE} no encontrada en app.program_templates")
            program_tmpl_id, total_weeks = row

            cur.execute("""
                SELECT p.id, p.document_number, p.first_name, p.last_name
                FROM app.patient_profiles p
                WHERE p.status = 'Activo'
                  AND p.id NOT IN (
                    SELECT patient_id FROM app.program_enrollments WHERE status = 'Active'
                  )
                ORDER BY p.document_number
            """)
            unregistered = cur.fetchall()
            print(f"  -> Pacientes activos sin programa activo: {len(unregistered)}")

            # Cargar weekly_day_templates para snapshot de semana 1
            cur.execute("""
                SELECT weekday, task_code, points, sort_order, media_id
                FROM app.weekly_day_templates
                WHERE template_id = %s
                ORDER BY weekday, sort_order
            """, (program_tmpl_id,))
            wdt_rows = cur.fetchall()
            import json
            week1_tasks = [
                {
                    "weekday": r[0],
                    "task_code": str(r[1]),
                    "points": r[2],
                    "sort_order": r[3],
                    "media_id": str(r[4]) if r[4] else None,
                    "nutrition_plan_id": None,
                    "routine_id": None,
                }
                for r in wdt_rows
            ]
            week1_snapshot_json = json.dumps(week1_tasks)
            print(f"  -> Template weekly_day_templates: {len(week1_tasks)} tareas configuradas.")

            for pid, doc, fn, ln in unregistered:
                enroll_id = uuid.uuid4()
                # Inicio lunes de hace 1 semana para pacientes clave, o hace 2-4 semanas
                weeks_ago = 1 if doc in ('55551234', '77777777', '88888888') else RANDOM.randint(1, 4)
                start_local = monday_of(today - timedelta(weeks=weeks_ago))

                cur.execute("""
                    INSERT INTO app.program_enrollments
                        (id, patient_id, template_id, timezone, status, started_at, start_local_date, current_week_number, created_at)
                    VALUES (%s, %s, %s, %s, 'Active', now(), %s, 1, now())
                """, (enroll_id, pid, program_tmpl_id, TIMEZONE, start_local))

                # Semanas 1..83
                weeks_vals = []
                for wn in range(1, total_weeks + 1):
                    ws = start_local + timedelta(days=(wn - 1) * 7)
                    we = ws + timedelta(days=6)
                    status = "Active" if wn == 1 else "Locked"
                    snap = week1_snapshot_json if wn == 1 else "[]"
                    wid = uuid.uuid4()
                    weeks_vals.append((wid, enroll_id, wn, status, ws, we, snap))

                cur.executemany("""
                    INSERT INTO app.program_weeks
                        (id, enrollment_id, week_number, status, week_start_date_local, week_end_date_local, tasks_snapshot, template_version_at_start, created_at)
                    VALUES (%s, %s, %s, %s, %s, %s, %s::jsonb, 1, now())
                """, weeks_vals)

                # Streak inicial
                cur.execute("""
                    INSERT INTO app.streak_states
                        (enrollment_id, current_streak, longest_streak, last_active_date, freezes_remaining, freezes_used_total, updated_at, multiplier_active, nb_current_streak, nb_longest_streak)
                    VALUES (%s, 0, 0, NULL, 3, 0, now(), 1.00, 0, 0)
                """, (enroll_id,))

                print(f"     Inscrito en programa: {fn} {ln} (Doc: {doc}) -> Enroll {enroll_id}")

            # Asegurar que todas las semanas 1 de enrollments activos tengan el snapshot poblado
            cur.execute("""
                UPDATE app.program_weeks w
                SET tasks_snapshot = %s::jsonb
                FROM app.program_enrollments e
                WHERE w.enrollment_id = e.id
                  AND w.week_number = 1
                  AND (w.tasks_snapshot = '[]'::jsonb OR w.tasks_snapshot IS NULL)
                  AND e.status = 'Active'
            """, (week1_snapshot_json,))
            print(f"  -> Semanas 1 actualizadas con snapshot de tareas: {cur.rowcount}")

            # -------------------------------------------------------------
            # PASO 3: Asignacion masiva de planes de nutricion vigentes
            # -------------------------------------------------------------
            print("\n[Paso 3] Asignando planes de nutricion clinicos...")

            dash_id = template_map["hipertension"]
            diabetes_id = template_map["diabetes"]
            obesidad_id = template_map["obesidad"]

            # Obtener todos los pacientes activos
            cur.execute("""
                SELECT p.id, p.document_number, p.first_name, p.last_name, e.start_local_date
                FROM app.patient_profiles p
                LEFT JOIN app.program_enrollments e ON e.patient_id = p.id AND e.status = 'Active'
                WHERE p.status = 'Activo'
                ORDER BY p.document_number
            """)
            active_patients = cur.fetchall()
            print(f"  -> Total de pacientes activos a verificar/asignar: {len(active_patients)}")

            plan_keys_cycle = ["obesidad", "hipertension", "diabetes"]
            assigned_count = 0
            extended_count = 0

            patient_plan_lookup = {}  # patient_id -> plan_id

            for idx, (pid, doc, fn, ln, start_local) in enumerate(active_patients):
                # Determinar plan segun regla o ciclo
                if doc == "55551234":
                    chosen_plan_id = obesidad_id
                    plan_desc = "Obesidad (1400 kcal)"
                elif doc == "77777777":
                    chosen_plan_id = dash_id
                    plan_desc = "Hipertensión - DASH (1800 kcal)"
                elif doc == "88888888":
                    chosen_plan_id = diabetes_id
                    plan_desc = "Diabetes Tipo 2 (1600 kcal)"
                elif doc == "1012345678":
                    chosen_plan_id = diabetes_id
                    plan_desc = "Diabetes Tipo 2 (1600 kcal)"
                else:
                    k = plan_keys_cycle[idx % len(plan_keys_cycle)]
                    chosen_plan_id = template_map[k]
                    plan_desc = k.capitalize()

                patient_plan_lookup[pid] = chosen_plan_id

                # Verificar si ya tiene asignacion activa
                cur.execute("""
                    SELECT id, plan_id, start_date, end_date, status
                    FROM app.nutrition_plan_assignments
                    WHERE patient_id = %s AND status = 1
                    ORDER BY start_date DESC
                    LIMIT 1
                """, (pid,))
                existing_assign = cur.fetchone()

                assign_start = (start_local or (today - timedelta(days=14)))
                assign_end = today + timedelta(days=90)

                if existing_assign:
                    aid, existing_plan_id, ex_start, ex_end, ex_status = existing_assign
                    # Si la fecha de vencimiento es menor a hoy + 30 dias, extenderla
                    if ex_end is None or ex_end < (today + timedelta(days=30)):
                        cur.execute("""
                            UPDATE app.nutrition_plan_assignments
                            SET end_date = %s,
                                updated_at = now()
                            WHERE id = %s
                        """, (assign_end, aid))
                        extended_count += 1
                    # Usar el plan asignado existente para este paciente
                    patient_plan_lookup[pid] = existing_plan_id
                else:
                    # Crear nueva asignacion activa
                    aid = uuid.uuid4()
                    cur.execute("""
                        INSERT INTO app.nutrition_plan_assignments
                            (id, patient_id, plan_id, start_date, end_date, status, notes, created_at)
                        VALUES (%s, %s, %s, %s, %s, 1, 'Asignación clínica programa 83 semanas', now())
                    """, (aid, pid, chosen_plan_id, assign_start, assign_end))
                    assigned_count += 1
                    if doc in ('55551234', '77777777', '88888888', '1012345678'):
                        print(f"     Asignado {plan_desc} a {fn} {ln} (Doc: {doc})")

            print(f"  -> Nuevas asignaciones: {assigned_count}, Asignaciones extendidas: {extended_count}")

            # -------------------------------------------------------------
            # PASO 4: Cargar comidas configuradas por cada plan y dia
            # -------------------------------------------------------------
            print("\n[Paso 4] Mapeando comidas configuradas por plan y dia...")
            cur.execute("""
                SELECT plan_id, day_number, meal_type, description, calories, protein_g, carbs_g, fat_g, fiber_g, daily_water_ml
                FROM app.nutrition_plan_days
                ORDER BY plan_id, day_number, sort_order
            """)
            plan_days_rows = cur.fetchall()

            # Estructura: plan_id -> day_number (1..7) -> meal_code ('des','alm','mer','cen') -> data
            # MealType enum: 1=Desayuno (des), 2=Almuerzo (alm), 3=Cena (cen), 4=Snack (mer)
            MEAL_TYPE_MAP = {
                1: "des",
                2: "alm",
                3: "cen",
                4: "mer",
            }
            plan_meals_catalog = {}
            for r_plan_id, r_day, r_mtype, r_desc, r_cal, r_prot, r_carbs, r_fat, r_fib, r_water in plan_days_rows:
                mcode = MEAL_TYPE_MAP.get(r_mtype)
                if not mcode:
                    continue
                plan_meals_catalog.setdefault(r_plan_id, {}).setdefault(r_day, {})[mcode] = {
                    "description": r_desc,
                    "calories": r_cal or 350,
                    "protein_g": r_prot or Decimal("25.00"),
                    "carbs_g": r_carbs or Decimal("40.00"),
                    "fat_g": r_fat or Decimal("10.00"),
                    "fiber_g": r_fib or Decimal("5.00"),
                    "daily_water_ml": r_water or 2000,
                }
            print(f"  -> Catalogo de comidas cargado para {len(plan_meals_catalog)} planes.")

            # Habit templates
            cur.execute("SELECT id, code FROM app.habit_templates WHERE code IN ('des', 'alm', 'mer', 'cen', 'agua')")
            habit_template_map = {code: hid for hid, code in cur.fetchall()}
            if len(habit_template_map) < 5:
                raise SystemExit(f"Faltan plantillas de habitos de nutricion en app.habit_templates: {habit_template_map}")

            # -------------------------------------------------------------
            # PASO 5: Generar historial de ingesta (app.habit_checks y app.nutrition_intake_logs)
            # -------------------------------------------------------------
            print("\n[Paso 5] Generando registros de ingesta de comidas y agua...")

            # Limpiar registros marcados con 'mass_seed' o 'ai_photo_seed'
            cur.execute("DELETE FROM app.nutrition_intake_logs WHERE source IN ('mass_seed', 'ai_photo_seed')")
            # Para habit_checks asociados, limpiar checks huerfanos creados previamente
            cur.execute("""
                DELETE FROM app.habit_checks h
                WHERE h.habit_template_id = ANY(%s)
                  AND NOT EXISTS (
                    SELECT 1 FROM app.nutrition_intake_logs l WHERE l.habit_check_id = h.id
                  )
            """, (list(habit_template_map.values()),))

            intake_logs_to_insert = []
            habit_checks_to_insert = []

            # Fechas: ultimos 7 dias anteriores + hoy
            past_days = [today - timedelta(days=d) for d in range(7, 0, -1)]

            total_patients_seeded = 0

            for pid, doc, fn, ln, start_local in active_patients:
                plan_id = patient_plan_lookup.get(pid)
                if not plan_id or plan_id not in plan_meals_catalog:
                    # Si es un plan clonado sin dias en catalogo, fallback al template
                    plan_id = obesidad_id
                    if plan_id not in plan_meals_catalog:
                        continue

                # 5.1 Historial de los ultimos 7 dias (comportamiento variado pero consistente)
                for log_date in past_days:
                    iso_wd = log_date.isoweekday()  # 1..7
                    day_meals = plan_meals_catalog.get(plan_id, {}).get(iso_wd, {})

                    # Registrar 3 a 4 comidas por dia (Desayuno, Almuerzo, Merienda, Cena)
                    # En pacientes demo, 85% de probabilidad de registrar cada comida
                    meals_to_log = ["des", "alm"]
                    if RANDOM.random() < 0.90:
                        meals_to_log.append("cen")
                    if RANDOM.random() < 0.75:
                        meals_to_log.append("mer")

                    for mcode in meals_to_log:
                        meal_info = day_meals.get(mcode, {
                            "calories": 400,
                            "protein_g": Decimal("25.00"),
                            "carbs_g": Decimal("40.00"),
                            "fat_g": Decimal("10.00"),
                            "fiber_g": Decimal("5.00"),
                        })

                        # Pequena variacion realista (+/- 5%)
                        factor = Decimal(str(round(RANDOM.uniform(0.95, 1.05), 2)))
                        cal_var = int(meal_info["calories"] * float(factor))
                        prot_var = round(meal_info["protein_g"] * factor, 2)
                        carbs_var = round(meal_info["carbs_g"] * factor, 2)
                        fat_var = round(meal_info["fat_g"] * factor, 2)
                        fib_var = round(meal_info["fiber_g"] * factor, 2)

                        source = "ai_photo_seed" if RANDOM.random() < 0.25 else "mass_seed"

                        chk_id = uuid.uuid4()
                        habit_checks_to_insert.append((
                            chk_id, pid, habit_template_map[mcode], log_date, True
                        ))

                        intake_logs_to_insert.append((
                            uuid.uuid4(), pid, chk_id, log_date, mcode,
                            cal_var, prot_var, carbs_var, fat_var, fib_var,
                            None, source, plan_id, iso_wd
                        ))

                    # Hidratacion diaria (6 a 8 vasos = 1500 a 2000 ml)
                    water_ml = RANDOM.choice([1500, 1750, 2000, 2000])
                    chk_water_id = uuid.uuid4()
                    habit_checks_to_insert.append((
                        chk_water_id, pid, habit_template_map["agua"], log_date, True
                    ))
                    intake_logs_to_insert.append((
                        uuid.uuid4(), pid, chk_water_id, log_date, "agua",
                        None, None, None, None, None,
                        water_ml, "mass_seed", plan_id, iso_wd
                    ))

                # 5.2 Dia de HOY (2026-09-24):
                # Desayuno y Almuerzo registrados + Hidratacion parcial (4-5 vasos: 1000-1250 ml)
                # Dejamos Merienda y Cena PENDIENTES para que el usuario pueda probar el registro
                today_iso_wd = today.isoweekday()
                today_meals = plan_meals_catalog.get(plan_id, {}).get(today_iso_wd, {})

                for mcode in ["des", "alm"]:
                    meal_info = today_meals.get(mcode, {
                        "calories": 420,
                        "protein_g": Decimal("28.00"),
                        "carbs_g": Decimal("45.00"),
                        "fat_g": Decimal("12.00"),
                        "fiber_g": Decimal("6.00"),
                    })
                    source = "ai_photo_seed" if (mcode == "alm" and doc in ("55551234", "77777777")) else "mass_seed"

                    chk_id = uuid.uuid4()
                    habit_checks_to_insert.append((
                        chk_id, pid, habit_template_map[mcode], today, True
                    ))
                    intake_logs_to_insert.append((
                        uuid.uuid4(), pid, chk_id, today, mcode,
                        meal_info["calories"], meal_info["protein_g"], meal_info["carbs_g"], meal_info["fat_g"], meal_info["fiber_g"],
                        None, source, plan_id, today_iso_wd
                    ))

                # Agua de hoy: 1250 ml (5 vasos de 250ml)
                chk_water_id = uuid.uuid4()
                habit_checks_to_insert.append((
                    chk_water_id, pid, habit_template_map["agua"], today, True
                ))
                intake_logs_to_insert.append((
                    uuid.uuid4(), pid, chk_water_id, today, "agua",
                    None, None, None, None, None,
                    1250, "mass_seed", plan_id, today_iso_wd
                ))

                total_patients_seeded += 1

            # Insercion masiva en lotes
            print(f"  -> Insertando {len(habit_checks_to_insert)} registros en app.habit_checks...")
            cur.executemany("""
                INSERT INTO app.habit_checks
                    (id, patient_id, habit_template_id, local_date, is_done, created_at)
                VALUES (%s, %s, %s, %s, %s, now())
                ON CONFLICT (patient_id, habit_template_id, local_date) DO UPDATE
                SET is_done = EXCLUDED.is_done
            """, habit_checks_to_insert)

            print(f"  -> Insertando {len(intake_logs_to_insert)} registros en app.nutrition_intake_logs...")
            cur.executemany("""
                INSERT INTO app.nutrition_intake_logs
                    (id, patient_id, habit_check_id, local_date, meal_code,
                     calories, protein_g, carbs_g, fat_g, fiber_g, water_ml,
                     source, nutrition_plan_id, nutrition_plan_day_number, created_at)
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, now())
                ON CONFLICT (patient_id, local_date, meal_code) DO UPDATE
                SET calories = EXCLUDED.calories,
                    protein_g = EXCLUDED.protein_g,
                    carbs_g = EXCLUDED.carbs_g,
                    fat_g = EXCLUDED.fat_g,
                    fiber_g = EXCLUDED.fiber_g,
                    water_ml = EXCLUDED.water_ml,
                    source = EXCLUDED.source,
                    nutrition_plan_id = EXCLUDED.nutrition_plan_id,
                    nutrition_plan_day_number = EXCLUDED.nutrition_plan_day_number
            """, intake_logs_to_insert)

            conn.commit()

    print("\n" + "=" * 70)
    print("Seed de Nutricion completado exitosamente con integridad referencial.")
    print("=" * 70)


if __name__ == "__main__":
    main()
