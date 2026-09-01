"""Seed de engagement para pacientes de prueba en la BD dev coppaddresd.

Rellena para TODOS los pacientes con cuenta (auth.users) y perfiles activos:
- Planes de nutrición (plantillas + personalizados) con días y asignaciones
- Asignaciones de rutinas de ejercicio
- Inscripciones al programa (83 semanas) + checkins + tareas + XP + rachas
- Encounters canónicos (app.encounters) + mediciones clínicas + baselines
- Registros emocionales, hábitos, notificaciones, documentos, device tokens
- Health scores + transformation scores semanales
- Signos vitales batch y media items reales

Uso (desde coppAddresdBack/scripts):
  python seed_engagement_demo_data.py
Idempotente: verifica existencia por paciente/plan antes de insertar.
"""

import json
import random
import subprocess
import uuid
from datetime import date, datetime, timedelta, timezone

DB = "coppaddresd"
RANDOM = random.Random(42)
NOW = datetime.now(timezone.utc)

# Pacientes objetivo: todos los que tienen user_id (cuenta real) + activos.
TARGET = "pacientes con user_id"

MEAL_TYPES = ["Desayuno", "Almuerzo", "Cena", "Snack"]

NUTRITION_TEMPLATES = [
    {
        "name": "Plan Hipertensión — DASH",
        "description": "Plan base estilo DASH: bajo en sodio, rico en potasio y fibra. Acompaña el control de presión arterial.",
        "target_condition": "Hipertensión",
        "duration_days": 28,
        "daily_calorie_target": 1800,
        "allergens": None,
        "days": [
            (
                "Desayuno",
                "Avena con frutas y nueces",
                "Avena 40g, manzana, 5 nueces, canela",
                320,
                8,
                45,
                6,
                3,
                0,
            ),
            (
                "Almuerzo",
                "Pechuga a la plancha con quinoa y verduras",
                "Pechuga 150g, quinoa 80g, brócoli, zanahoria",
                520,
                38,
                55,
                10,
                8,
                0,
            ),
            (
                "Cena",
                "Pescado al horno con ensalada",
                "Tilapia 150g, espinaca, tomate, limón",
                380,
                30,
                25,
                6,
                5,
                0,
            ),
            (
                "Snack",
                "Yogur griego con semillas",
                "Yogur griego 120g, chía 10g",
                150,
                12,
                12,
                2,
                3,
                0,
            ),
        ],
    },
    {
        "name": "Plan Diabetes Tipo 2",
        "description": "Plan bajo en carbohidratos simples, índice glucémico controlado y porciones balanceadas.",
        "target_condition": "Diabetes tipo 2",
        "duration_days": 30,
        "daily_calorie_target": 1600,
        "allergens": None,
        "days": [
            (
                "Desayuno",
                "Huevos revueltos con aguacate",
                "2 huevos, aguacate 50g, tomate",
                300,
                14,
                12,
                4,
                6,
                0,
            ),
            (
                "Almuerzo",
                "Pollo al horno con verduras",
                "Pollo 140g, calabacín, pimentón, aceite de oliva 10ml",
                450,
                32,
                22,
                6,
                7,
                0,
            ),
            (
                "Cena",
                "Sopa de lentejas con ensalada",
                "Lentejas 60g, espinaca, cebolla",
                350,
                18,
                45,
                9,
                6,
                0,
            ),
            (
                "Snack",
                "Macedonia de frutas",
                "Fresas, kiwi, melón",
                100,
                1,
                22,
                3,
                2,
                0,
            ),
        ],
    },
    {
        "name": "Plan Obesidad — Déficit calórico",
        "description": "Plan hipocalórico balanceado para pérdida de peso progresiva con supervisión clínica.",
        "target_condition": "Obesidad",
        "duration_days": 42,
        "daily_calorie_target": 1400,
        "allergens": None,
        "days": [
            (
                "Desayuno",
                "Batido de proteína con avena",
                "Proteína 30g, avena 30g, banano 1/2",
                280,
                25,
                30,
                6,
                4,
                0,
            ),
            (
                "Almuerzo",
                "Carne magra con arroz integral",
                "Carne magra 130g, arroz integral 60g, ensalada",
                430,
                30,
                40,
                8,
                6,
                0,
            ),
            (
                "Cena",
                "Wrap de lechuga con pollo",
                "Pollo 120g, lechuga, pepino, zanahoria",
                300,
                28,
                15,
                5,
                5,
                0,
            ),
            (
                "Snack",
                "Queso cottage con tomate",
                "Queso cottage 100g, tomate cherry",
                110,
                12,
                8,
                2,
                3,
                0,
            ),
        ],
    },
]

COMMENT_BODIES = [
    "Evolución favorable; mantener el plan y el seguimiento quincenal.",
    "Se ajusta el plan tras revisar la adherencia de la semana.",
    "Continúe con la rutina; los signos vitales se mantienen estables.",
    "Requiere refuerzo en hidratación; se sugiere recordatorio diario.",
]

DOC_TITLES = [
    ("Consentimiento informado", "Consentimiento informado del programa COPP-ADRESD"),
    ("Resultados de laboratorio", "Resultados de laboratorio del período actual"),
    ("Plan de cuidados", "Plan de cuidados firmado por el profesional"),
    ("Autorización de teleconsulta", "Autorización para telemedicina"),
]

MEDIA_ITEMS = [
    (
        "Podcast: Introducción al programa",
        "Bienvenida al programa COPP-ADRESD: qué esperar y cómo aprovecharlo.",
        "Podcast",
        "Podcast",
        "Nutricion",
        900,
        1,
    ),
    (
        "Video: Caminata saludable 101",
        "Técnica correcta de caminata para pacientes con hipertensión.",
        "Video",
        "Video",
        "SaludFisica",
        480,
        2,
    ),
    (
        "Podcast: Manejo del estrés",
        "Técnicas de respiración y manejo de la ansiedad.",
        "Podcast",
        "Podcast",
        "BienestarEmocional",
        720,
        3,
    ),
    (
        "Video: Cocina baja en sodio",
        "Recetas prácticas para reducir el sodio sin perder sabor.",
        "Video",
        "Video",
        "Nutricion",
        600,
        4,
    ),
    (
        "Podcast: Nutriobiótico y microbiota",
        "Qué es el nutribiótico y cómo apoya la salud cardiovascular.",
        "Podcast",
        "Podcast",
        "Biologia",
        660,
        5,
    ),
    (
        "Video: Ejercicio en casa para principiantes",
        "Rutina de 20 minutos de bajo impacto.",
        "Video",
        "Video",
        "SaludFisica",
        1200,
        6,
    ),
]

ENCOUNTER_TYPES = ["consulta_periodica", "telemedicina", "seguimiento"]
ENCOUNTER_STATUS = ["planned", "in_progress", "completed", "cancelled"]


def psql(sql: str) -> str:
    r = subprocess.run(
        [
            "docker",
            "exec",
            "-i",
            "coppAddresd",
            "psql",
            "-U",
            "app_user",
            "-d",
            DB,
            "-t",
            "-A",
        ],
        input=sql,
        text=True,
        encoding="utf-8",
        errors="replace",
        check=True,
        capture_output=True,
    )
    if "ERROR" in r.stderr:
        raise RuntimeError(f"SQL falló: {r.stderr.strip()[:500]}\nSQL: {sql[:300]}")
    return r.stdout


def fetch_rows(sql: str) -> list[list[str]]:
    return [line.split("|") for line in psql(sql).strip().splitlines() if line]


def main() -> None:
    now_iso = NOW.isoformat()
    admin_id = fetch_rows(
        'SELECT "Id" FROM auth."Users" WHERE "UserName" = \'admin@coppaddresd.com\' LIMIT 1;'
    )[0][0]

    # ── Catálogos de referencia ──────────────────────────────────────────
    patients = fetch_rows(
        "SELECT p.id, p.user_id, p.first_name, p.last_name, p.document_number "
        "FROM app.patient_profiles p "
        "WHERE p.user_id IS NOT NULL AND p.status ILIKE 'activ%' "
        "ORDER BY p.first_name;"
    )
    print(f"Pacientes objetivo: {len(patients)}")

    professionals = fetch_rows(
        "SELECT p.id, e.user_id FROM erp.professionals p "
        "JOIN erp.employees e ON e.id = p.employee_id WHERE e.status = 'Active' ORDER BY e.first_name;"
    )
    routines = fetch_rows(
        "SELECT id, name, category, difficulty, status FROM app.exercise_routines ORDER BY name;"
    )
    habit_templates = fetch_rows(
        "SELECT id, code FROM app.habit_templates ORDER BY sort_order;"
    )
    metrics = fetch_rows(
        "SELECT id, name, default_unit_id FROM app.measurement_metrics ORDER BY name;"
    )
    metric_by_name = {m[1]: (m[0], m[2]) for m in metrics}
    doc_types = fetch_rows(
        "SELECT id, name FROM app.clinical_document_types ORDER BY name LIMIT 4;"
    )
    template_id = fetch_rows(
        "SELECT id FROM app.program_templates WHERE code = 'default-83w' LIMIT 1;"
    )[0][0]

    # Semana 1 del enrollment de referencia: copiar su snapshot de tareas.
    week1_snapshot = fetch_rows(
        "SELECT tasks_snapshot::text FROM app.program_weeks "
        "WHERE enrollment_id = '732c2f7d-08e2-4bd3-825c-5dff0594e822' AND week_number = 1 LIMIT 1;"
    )
    week1_tasks = json.loads(week1_snapshot[0][0]) if week1_snapshot else []
    print(f"Tareas semana 1 (plantilla): {len(week1_tasks)}")

    # ── 1. Media items reales (reemplaza Prueba/Prueba2 si no hay otros) ──
    existing_media = fetch_rows(
        "SELECT id, title FROM app.media_items ORDER BY created_at;"
    )
    for idx, (title, desc, media_type, category, author, duration, sort) in enumerate(
        MEDIA_ITEMS
    ):
        exists = [m for m in existing_media if m[1] == title]
        if exists:
            continue
        psql(
            """
            INSERT INTO app.media_items (id, title, description, media_type, category,
                storage_key, content_type, file_size_bytes, duration_secs, status, sort_order,
                published_at, created_at, updated_at, created_by, author, day, month)
            VALUES ('{0}', '{1}', '{2}', '{3}', '{4}', 'media/seed-{10}.mp3', 'audio/mpeg', 0, {5}, 'Published',
                {6}, '{7}', '{7}', '{7}', '{8}', '{9}', {10}, {11});
            """.format(
                str(uuid.uuid4()),
                title.replace("'", "''"),
                desc.replace("'", "''"),
                media_type,
                category,
                duration,
                sort,
                now_iso,
                admin_id,
                author,
                (idx * 5) % 27 + 1,
                (idx * 3) % 12 + 1,
            )
        )
    print(f"Media items: {len(existing_media) + len(MEDIA_ITEMS)}")

    # ── 2. Planes de nutrición plantilla + días ───────────────────────────
    existing_plans = fetch_rows(
        "SELECT id, name FROM app.nutrition_plans WHERE is_template = true;"
    )
    for plan in NUTRITION_TEMPLATES:
        exists = [p for p in existing_plans if p[1] == plan["name"]]
        if exists:
            plan_id = exists[0][0]
        else:
            plan_id = str(uuid.uuid4())
            psql(
                """
                INSERT INTO app.nutrition_plans (id, name, description, target_condition,
                    duration_days, daily_calorie_target, is_template, status, created_by,
                    created_at, updated_at)
                VALUES ('{0}', '{1}', '{2}', '{3}', {4}, {5}, true, 2, '{6}', '{7}', '{7}');
                """.format(
                    plan_id,
                    plan["name"].replace("'", "''"),
                    plan["description"].replace("'", "''"),
                    plan["target_condition"],
                    plan["duration_days"],
                    plan["daily_calorie_target"],
                    admin_id,
                    now_iso,
                )
            )
            # 7 días × 4 comidas
            for day_num in range(1, 8):
                for meal_idx, (
                    meal,
                    desc,
                    foods,
                    cal,
                    prot,
                    carbs,
                    fat,
                    fiber,
                    water,
                ) in enumerate(plan["days"]):
                    psql(
                        """
                        INSERT INTO app.nutrition_plan_days (id, plan_id, day_number, meal_type,
                            description, foods, calories, carbs_g, fat_g, fiber_g, protein_g,
                            water_ml, daily_water_ml, sort_order, created_at, updated_at)
                        VALUES ('{0}', '{1}', {2}, {3}, '{4}', '{5}', {6}, {7}, {8}, {9}, {10},
                            {11}, 2000, {3}, '{12}', '{12}');
                        """.format(
                            str(uuid.uuid4()),
                            plan_id,
                            day_num,
                            meal_idx + 1,
                            desc.replace("'", "''"),
                            foods.replace("'", "''"),
                            cal,
                            carbs,
                            fat,
                            fiber,
                            prot,
                            water,
                            now_iso,
                        )
                    )
            print(f"  Plan creado: {plan['name']}")
    print(f"Planes plantilla: {len(NUTRITION_TEMPLATES)}")

    # ── 3. Plan personalizado + asignación por paciente ──────────────────
    plan_count = 0
    assign_count = 0
    for i, (pid, user_id, first, last, doc) in enumerate(patients):
        # Plan personalizado (no plantilla)
        existing_custom = fetch_rows(
            "SELECT id FROM app.nutrition_plans WHERE is_template = false AND patient_id = '{0}' LIMIT 1;".format(
                pid
            )
        )
        if existing_custom:
            custom_id = existing_custom[0][0]
        else:
            tpl = NUTRITION_TEMPLATES[i % len(NUTRITION_TEMPLATES)]
            custom_id = str(uuid.uuid4())
            psql(
                """
                INSERT INTO app.nutrition_plans (id, name, description, target_condition,
                    duration_days, daily_calorie_target, is_template, patient_id, status,
                    created_by, created_at, updated_at)
                VALUES ('{0}', 'Plan {1} {2}', '{3}', '{4}', {5}, {6}, false, '{7}', 2,
                    '{8}', '{9}', '{9}');
                """.format(
                    custom_id,
                    first,
                    last,
                    ("Plan personalizado de " + tpl["description"])[:500],
                    tpl["target_condition"],
                    tpl["duration_days"],
                    tpl["daily_calorie_target"],
                    pid,
                    admin_id,
                    now_iso,
                )
            )
            # Copiar los días del template
            tpl_days = fetch_rows(
                "SELECT day_number, meal_type, description, foods, calories, carbs_g, fat_g, "
                "fiber_g, protein_g, water_ml, daily_water_ml FROM app.nutrition_plan_days "
                "WHERE plan_id = (SELECT id FROM app.nutrition_plans WHERE name = '{0}' LIMIT 1) "
                "ORDER BY day_number, meal_type;".format(tpl["name"].replace("'", "''"))
            )
            for d in tpl_days:
                psql(
                    """
                    INSERT INTO app.nutrition_plan_days (id, plan_id, day_number, meal_type,
                        description, foods, calories, carbs_g, fat_g, fiber_g, protein_g,
                        water_ml, daily_water_ml, sort_order, created_at, updated_at)
                    VALUES ('{0}', '{1}', {2}, {3}, '{4}', '{5}', {6}, {7}, {8}, {9}, {10},
                        {11}, {12}, {3}, '{13}', '{13}');
                    """.format(
                        str(uuid.uuid4()),
                        custom_id,
                        d[0],
                        d[1],
                        (d[2] or "").replace("'", "''"),
                        (d[3] or "").replace("'", "''"),
                        d[4] or 0,
                        d[5] or 0,
                        d[6] or 0,
                        d[7] or 0,
                        d[8] or 0,
                        d[9] or 0,
                        d[10] or 2000,
                        now_iso,
                    )
                )
            plan_count += 1

        # Asignación del plan
        existing_assign = fetch_rows(
            "SELECT id FROM app.nutrition_plan_assignments WHERE patient_id = '{0}' AND plan_id = '{1}' LIMIT 1;".format(
                pid, custom_id
            )
        )
        if not existing_assign:
            start = date.today() - timedelta(days=(i % 14))
            psql(
                """
                INSERT INTO app.nutrition_plan_assignments (id, patient_id, plan_id,
                    start_date, end_date, status, notes, created_by, created_at, updated_at)
                VALUES ('{0}', '{1}', '{2}', '{3}', '{4}', 1, '{5}', '{6}', '{7}', '{7}');
                """.format(
                    str(uuid.uuid4()),
                    pid,
                    custom_id,
                    start.isoformat(),
                    (start + timedelta(days=30)).isoformat(),
                    "Asignado por seeder de pruebas",
                    admin_id,
                    now_iso,
                )
            )
            assign_count += 1
    print(f"Planes personalizados: {plan_count}, asignaciones: {assign_count}")

    # ── 4. Asignaciones de rutinas ───────────────────────────────────────
    routine_assign_count = 0
    for i, (pid, user_id, first, last, doc) in enumerate(patients):
        existing = fetch_rows(
            "SELECT id FROM app.routine_assignments WHERE patient_id = '{0}' LIMIT 1;".format(
                pid
            )
        )
        if existing:
            continue
        r1 = routines[i % len(routines)]
        r2 = routines[(i * 3 + 2) % len(routines)]
        start = date.today() - timedelta(days=(i % 14))
        psql(
            """
            INSERT INTO app.routine_assignments (id, patient_id, routine_id,
                start_date, end_date, frequency, status, notes, created_by, created_at, updated_at)
            VALUES ('{0}', '{1}', '{2}', '{3}', '{4}', 1, 1, '{5}', '{6}', '{7}', '{7}');
            """.format(
                str(uuid.uuid4()),
                pid,
                r1[0],
                start.isoformat(),
                (start + timedelta(days=60)).isoformat(),
                "Rutina principal asignada por seeder",
                admin_id,
                now_iso,
            )
        )
        if i % 2 == 0 and r2[0] != r1[0]:
            psql(
                """
                INSERT INTO app.routine_assignments (id, patient_id, routine_id,
                    start_date, end_date, frequency, status, notes, created_by, created_at, updated_at)
                VALUES ('{0}', '{1}', '{2}', '{3}', '{4}', 2, 1, '{5}', '{6}', '{7}', '{7}');
                """.format(
                    str(uuid.uuid4()),
                    pid,
                    r2[0],
                    start.isoformat(),
                    (start + timedelta(days=45)).isoformat(),
                    "Rutina complementaria",
                    admin_id,
                    now_iso,
                )
            )
        routine_assign_count += 1
    print(f"Pacientes con rutinas asignadas: {routine_assign_count}")

    # ── 5. Inscripciones al programa + semanas + checkins + XP + rachas ───
    enrolled = 0
    weeks_created = 0
    for i, (pid, user_id, first, last, doc) in enumerate(patients):
        existing = fetch_rows(
            "SELECT id FROM app.program_enrollments WHERE patient_id = '{0}' LIMIT 1;".format(
                pid
            )
        )
        if existing:
            enrollment_id = existing[0][0]
        else:
            enrollment_id = str(uuid.uuid4())
            start = date.today() - timedelta(days=(i % 7))
            psql(
                """
                INSERT INTO app.program_enrollments (id, patient_id, template_id, timezone,
                    status, started_at, start_local_date, current_week_number, created_by,
                    updated_by, created_at, updated_at)
                VALUES ('{0}', '{1}', '{2}', 'America/Bogota', 'Active', '{3}',
                    '{4}', 1, '{5}', '{5}', '{3}', '{3}');
                """.format(
                    enrollment_id,
                    pid,
                    template_id,
                    now_iso,
                    start.isoformat(),
                    admin_id,
                )
            )

        # Semanas: reparar enrollments huérfanos (corridas interrumpidas).
        week_count = fetch_rows(
            "SELECT count(*) FROM app.program_weeks WHERE enrollment_id = '{0}';".format(
                enrollment_id
            )
        )[0][0]
        if int(week_count) == 0:
            start = fetch_rows(
                "SELECT start_local_date FROM app.program_enrollments WHERE id = '{0}';".format(
                    enrollment_id
                )
            )[0][0]
            start = date.fromisoformat(start)
            # 83 semanas: semana 1 activa con snapshot, resto Locked. Batch único.
            week_values = []
            for w in range(1, 84):
                week_start = start + timedelta(days=(w - 1) * 7)
                week_end = week_start + timedelta(days=6)
                snapshot = (
                    json.dumps(week1_tasks, ensure_ascii=False) if w == 1 else "[]"
                )
                status = "Active" if w == 1 else "Locked"
                activated = "'{0}'::timestamptz".format(now_iso) if w == 1 else "NULL"
                week_values.append(
                    "('{0}', '{1}', {2}, '{3}', '{4}', '{5}', '{6}', 1, {7}, '{8}', '{8}')".format(
                        str(uuid.uuid4()),
                        enrollment_id,
                        w,
                        status,
                        week_start.isoformat(),
                        week_end.isoformat(),
                        snapshot.replace("'", "''"),
                        activated,
                        now_iso,
                    )
                )
                weeks_created += 1
            psql(
                "INSERT INTO app.program_weeks (id, enrollment_id, week_number, status, "
                "week_start_date_local, week_end_date_local, tasks_snapshot, "
                "template_version_at_start, activated_at, created_at, updated_at) "
                "VALUES " + ",".join(week_values) + ";"
            )
            enrolled += 1

        # Checkins últimos 7 días de la semana actual
        week_row = fetch_rows(
            "SELECT id, week_start_date_local FROM app.program_weeks "
            "WHERE enrollment_id = '{0}' AND status = 'Active' LIMIT 1;".format(
                enrollment_id
            )
        )
        week_id = week_row[0][0]
        week_start = date.fromisoformat(week_row[0][1])
        checkin_count = 0

        existing_checkins = set(
            r[0]
            for r in fetch_rows(
                "SELECT local_date FROM app.daily_checkins WHERE enrollment_id = '{0}';".format(
                    enrollment_id
                )
            )
        )
        existing_tasks = set(
            r[0] + "|" + r[1]
            for r in fetch_rows(
                "SELECT local_date, task_code FROM app.task_completions WHERE enrollment_id = '{0}';".format(
                    enrollment_id
                )
            )
        )

        checkin_values = []
        task_values = []
        xp_values = []
        for day_offset in range(0, 7):
            local_date = week_start + timedelta(days=day_offset)
            if local_date > date.today():
                continue
            weekday = local_date.isoweekday()
            day_tasks = [t for t in week1_tasks if t.get("weekday") == weekday]
            done_tasks = [t for t in day_tasks if RANDOM.random() < 0.8]
            total = sum(t.get("points", 0) for t in done_tasks)
            perfect = len(day_tasks) > 0 and len(done_tasks) == len(day_tasks)

            if local_date.isoformat() in existing_checkins:
                continue
            checkin_id = str(uuid.uuid4())
            checkin_values.append(
                "('{0}', '{1}', '{2}', '{3}', {4}, {5}, '{6}', {7}, {8}, {9}, '{10}', '{10}')".format(
                    checkin_id,
                    enrollment_id,
                    week_id,
                    local_date.isoformat(),
                    weekday,
                    2 + RANDOM.randint(0, 3),
                    "Ninguna" if perfect else "Poca motivación",
                    total,
                    50 if perfect else 0,
                    perfect,
                    now_iso,
                )
            )
            checkin_count += 1

            # Task completions para las tareas hechas (idempotente por task_code+fecha)
            for t in done_tasks:
                key = local_date.isoformat() + "|" + t["task_code"]
                if key in existing_tasks:
                    continue
                task_id = str(uuid.uuid4())
                task_values.append(
                    "('{0}', '{1}', '{2}', '{3}', '{4}', {5}, '{6}', {7}, '{8}', '{9}', 'manual')".format(
                        task_id,
                        enrollment_id,
                        week_id,
                        checkin_id,
                        local_date.isoformat(),
                        weekday,
                        t["task_code"],
                        t.get("points", 0),
                        str(uuid.uuid4()),
                        now_iso,
                    )
                )
                rule_code = "TASK_" + t["task_code"].upper()
                xp_values.append(
                    "('{0}', '{1}', {2}, 'TaskCompletion', 'manual', '{3}', 0, '{4}', '{5}', 1.0)".format(
                        str(uuid.uuid4()),
                        enrollment_id,
                        t.get("points", 0),
                        task_id,
                        now_iso,
                        rule_code,
                    )
                )

        if checkin_values:
            psql(
                "INSERT INTO app.daily_checkins (id, enrollment_id, program_week_id, "
                "local_date, weekday, mood_score, barriers, total_points, "
                "bonus_awarded, is_perfect_day, created_at, updated_at) "
                "VALUES " + ",".join(checkin_values) + ";"
            )
        if task_values:
            psql(
                "INSERT INTO app.task_completions (id, enrollment_id, program_week_id, "
                "daily_checkin_id, local_date, weekday, task_code, points_awarded, "
                "client_request_id, completed_at, source_ref_type) "
                "VALUES " + ",".join(task_values) + ";"
            )
        if xp_values:
            psql(
                "INSERT INTO app.xp_ledger (id, enrollment_id, amount, reason, "
                "source_ref_type, source_ref_id, balance_after, awarded_at, rule_code, multiplier_used) "
                "VALUES " + ",".join(xp_values) + ";"
            )

        # Streak state
        streak_exists = fetch_rows(
            "SELECT 1 FROM app.streak_states WHERE enrollment_id = '{0}' LIMIT 1;".format(
                enrollment_id
            )
        )
        if not streak_exists:
            checkins_done = fetch_rows(
                "SELECT count(*) FROM app.daily_checkins WHERE enrollment_id = '{0}';".format(
                    enrollment_id
                )
            )[0][0]
            streak = int(checkins_done) if checkins_done else 0
            psql(
                """
                INSERT INTO app.streak_states (enrollment_id, current_streak, longest_streak,
                    last_active_date, freezes_remaining, freezes_used_total, last_break_date,
                    updated_at, multiplier_active, nb_current_streak, nb_last_completed_date, nb_longest_streak)
                VALUES ('{0}', {1}, {2}, '{3}', 1, 0, NULL, '{4}', 1.0, {1}, '{3}', {2});
                """.format(
                    enrollment_id,
                    streak,
                    streak,
                    (date.today() - timedelta(days=1)).isoformat(),
                    now_iso,
                )
            )
        # Streak freeze para algunos
        if i % 4 == 0:
            freeze_exists = fetch_rows(
                "SELECT 1 FROM app.streak_freezes WHERE enrollment_id = '{0}' LIMIT 1;".format(
                    enrollment_id
                )
            )
            if not freeze_exists:
                psql(
                    """
                    INSERT INTO app.streak_freezes (id, enrollment_id, kind, used_on_local_date,
                        granted_at, granted_reason, created_at)
                    VALUES ('{0}', '{1}', 'perfect_day', '{2}', '{3}', '7 días perfectos', '{3}');
                    """.format(
                        str(uuid.uuid4()),
                        enrollment_id,
                        (date.today() - timedelta(days=3)).isoformat(),
                        now_iso,
                    )
                )
    print(f"Inscripciones: {enrolled}, semanas: {weeks_created}")

    # ── 6. Encounters canónicos + mediciones + baselines ─────────────────
    enc_count = 0
    meas_count = 0
    for i, (pid, user_id, first, last, doc) in enumerate(patients):
        existing_enc = fetch_rows(
            "SELECT id FROM app.encounters WHERE patient_id = '{0}' LIMIT 1;".format(
                pid
            )
        )
        if existing_enc:
            enc_id = existing_enc[0][0]
        else:
            enc_id = str(uuid.uuid4())
            prof = professionals[i % len(professionals)][0]
            enc_date = NOW - timedelta(days=(i % 10), hours=3)
            psql(
                """
                INSERT INTO app.encounters (id, patient_id, professional_id, type, status,
                    started_at, ended_at, reason, notes, created_by, created_at, updated_at)
                VALUES ('{0}', '{1}', '{2}', '{3}', 'completed', '{4}', '{5}',
                    '{6}', '{7}', '{8}', '{4}', '{4}');
                """.format(
                    enc_id,
                    pid,
                    prof,
                    ENCOUNTER_TYPES[i % len(ENCOUNTER_TYPES)],
                    (enc_date - timedelta(minutes=20)).isoformat(),
                    enc_date.isoformat(),
                    "Control periódico del programa",
                    COMMENT_BODIES[i % len(COMMENT_BODIES)],
                    admin_id,
                )
            )
            enc_count += 1

        # Mediciones clínicas por paciente (2-4 métricas)
        for mi, mname in enumerate(
            [
                "Peso",
                "Presión arterial sistólica",
                "Glucosa en ayunas",
                "Frecuencia cardíaca",
            ]
        ):
            if mname not in metric_by_name:
                continue
            mid, unit_id = metric_by_name[mname]
            exists = fetch_rows(
                "SELECT 1 FROM app.clinical_measurements WHERE patient_id = '{0}' AND metric_id = '{1}' LIMIT 1;".format(
                    pid, mid
                )
            )
            if exists:
                continue
            value = {
                "Peso": round(58 + (i % 25) + RANDOM.uniform(0, 3), 1),
                "Presión arterial sistólica": 118 + (i % 3) * 6 + RANDOM.randint(0, 5),
                "Glucosa en ayunas": 85 + (i % 4) * 10 + RANDOM.randint(0, 8),
                "Frecuencia cardíaca": 62 + (i % 3) * 5 + RANDOM.randint(0, 6),
            }[mname]
            obs = NOW - timedelta(days=(i % 10))
            psql(
                """
                INSERT INTO app.clinical_measurements (id, patient_id, metric_id, encounter_id,
                    value, unit_id, observed_at, recorded_at, source, notes, created_by, created_at)
                VALUES ('{0}', '{1}', '{2}', '{3}', {4}, '{5}', '{6}', '{6}', 'seed',
                    '{7}', '{8}', '{6}');
                """.format(
                    str(uuid.uuid4()),
                    pid,
                    mid,
                    enc_id,
                    value,
                    unit_id,
                    obs.isoformat(),
                    "Medición registrada por seeder",
                    admin_id,
                )
            )
            meas_count += 1

        # Baselines
        for mname in ["Peso", "Presión arterial sistólica"]:
            if mname not in metric_by_name:
                continue
            mid, unit_id = metric_by_name[mname]
            exists = fetch_rows(
                "SELECT 1 FROM app.clinical_baselines WHERE patient_id = '{0}' AND metric_id = '{1}' LIMIT 1;".format(
                    pid, mid
                )
            )
            if exists:
                continue
            value = 78.0 + (i % 20) if mname == "Peso" else 130 + (i % 3) * 8
            psql(
                """
                INSERT INTO app.clinical_baselines (id, patient_id, metric_id, value, unit_id,
                    favorable_direction, target_value, measured_at, set_by, created_at, updated_at)
                VALUES ('{0}', '{1}', '{2}', {3}, '{4}', -1, {5}, '{6}', '{7}', '{8}', '{8}');
                """.format(
                    str(uuid.uuid4()),
                    pid,
                    mid,
                    value,
                    unit_id,
                    round(value * 0.95, 1),
                    (date.today() - timedelta(days=30)).isoformat(),
                    admin_id,
                    now_iso,
                )
            )
    print(f"Encounters: {enc_count}, mediciones: {meas_count}")

    # ── 7. Registros emocionales + hábitos ───────────────────────────────
    emo_count = 0
    habit_count = 0
    for i, (pid, user_id, first, last, doc) in enumerate(patients):
        enrollment = fetch_rows(
            "SELECT id FROM app.program_enrollments WHERE patient_id = '{0}' LIMIT 1;".format(
                pid
            )
        )
        enr_id = enrollment[0][0] if enrollment else None
        for d in range(1, 8):
            rec_date = date.today() - timedelta(days=d)
            exists = fetch_rows(
                "SELECT 1 FROM app.emotional_records WHERE patient_id = '{0}' AND recorded_local_date = '{1}' LIMIT 1;".format(
                    pid, rec_date.isoformat()
                )
            )
            if exists:
                continue
            psql(
                """
                INSERT INTO app.emotional_records (id, patient_id, program_enrollment_id,
                    recorded_local_date, mood_score, barriers, notes, created_at)
                VALUES ('{0}', '{1}', {2}, '{3}', {4}, '{5}', '{6}', '{7}');
                """.format(
                    str(uuid.uuid4()),
                    pid,
                    "'{0}'".format(enr_id) if enr_id else "NULL",
                    rec_date.isoformat(),
                    2 + RANDOM.randint(0, 3),
                    "Ninguna" if RANDOM.random() < 0.6 else "Ansiedad",
                    "Registro diario de ánimo",
                    now_iso,
                )
            )
            emo_count += 1

        for ht_id, ht_code in habit_templates:
            for d in range(1, 8):
                rec_date = date.today() - timedelta(days=d)
                exists = fetch_rows(
                    "SELECT 1 FROM app.habit_checks WHERE patient_id = '{0}' AND habit_template_id = '{1}' AND local_date = '{2}' LIMIT 1;".format(
                        pid, ht_id, rec_date.isoformat()
                    )
                )
                if exists:
                    continue
                psql(
                    """
                    INSERT INTO app.habit_checks (id, patient_id, habit_template_id,
                        local_date, is_done, created_at)
                    VALUES ('{0}', '{1}', '{2}', '{3}', {4}, '{5}');
                    """.format(
                        str(uuid.uuid4()),
                        pid,
                        ht_id,
                        rec_date.isoformat(),
                        RANDOM.random() < 0.75,
                        now_iso,
                    )
                )
                habit_count += 1
    print(f"Registros emocionales: {emo_count}, hábitos: {habit_count}")

    # ── 8. Notificaciones ────────────────────────────────────────────────
    notif_count = 0
    notif_titles = [
        (
            "program",
            "Nueva semana disponible",
            "Tu semana {n} del programa ya está activa. ¡A cumplir tus metas!",
        ),
        (
            "appointment",
            "Próxima cita",
            "Tienes una cita médica programada para esta semana.",
        ),
        (
            "wellness",
            "Recordatorio de medición",
            "No olvides registrar tus signos vitales de hoy.",
        ),
        (
            "alert",
            "Resultado disponible",
            "Tu última evaluación de salud está disponible en la app.",
        ),
        (
            "program",
            "Racha en riesgo",
            "¡Completa una tarea hoy para no perder tu racha!",
        ),
    ]
    for i, (pid, user_id, first, last, doc) in enumerate(patients):
        existing = fetch_rows(
            "SELECT 1 FROM app.notifications WHERE patient_id = '{0}' LIMIT 1;".format(
                pid
            )
        )
        if existing:
            continue
        for ni in range(3):
            ntype, ntitle, nmsg = notif_titles[(i + ni) % len(notif_titles)]
            sent = NOW - timedelta(days=ni * 2, hours=ni * 3)
            psql(
                """
                INSERT INTO app.notifications (id, patient_id, type, title, message,
                    priority, channel, sent_at, read_at)
                VALUES ('{0}', '{1}', '{2}', '{3}', '{4}', '{5}', 'push', '{6}',
                    CASE WHEN {7} THEN '{6}'::timestamptz ELSE NULL END);
                """.format(
                    str(uuid.uuid4()),
                    pid,
                    ntype,
                    ntitle.replace("{n}", str(1 + ni)),
                    nmsg.replace("{n}", str(1 + ni)),
                    "high" if ni == 0 else "normal",
                    sent.isoformat(),
                    "true" if ni == 0 else "false",
                )
            )
            notif_count += 1
    print(f"Notificaciones: {notif_count}")

    # ── 9. Documentos por paciente ───────────────────────────────────────
    doc_count = 0
    for i, (pid, user_id, first, last, doc) in enumerate(patients):
        existing = fetch_rows(
            "SELECT 1 FROM app.documents WHERE patient_id = '{0}' LIMIT 1;".format(pid)
        )
        if existing:
            continue
        prof = professionals[i % len(professionals)][0]
        dtitle, ddesc = DOC_TITLES[i % len(DOC_TITLES)]
        psql(
            """
            INSERT INTO app.documents (id, patient_id, professional_id, clinic_id,
                document_type_id, title, description, storage_key, content_type,
                file_size_bytes, version, status, uploaded_by, created_by, created_at, updated_at)
            VALUES ('{0}', '{1}', '{2}', NULL, '{3}', '{4}', '{5}', 'seed/{6}.pdf',
                'application/pdf', 0, 1, 'Active', '{7}', '{7}', '{8}', '{8}');
            """.format(
                str(uuid.uuid4()),
                pid,
                prof,
                doc_types[i % len(doc_types)][0],
                dtitle.replace("'", "''"),
                ddesc.replace("'", "''"),
                doc,
                admin_id,
                now_iso,
            )
        )
        doc_count += 1
    print(f"Documentos: {doc_count}")

    # ── 10. Device tokens ────────────────────────────────────────────────
    token_count = 0
    for i, (pid, user_id, first, last, doc) in enumerate(patients):
        existing = fetch_rows(
            "SELECT 1 FROM app.device_tokens WHERE user_id = '{0}' LIMIT 1;".format(
                user_id
            )
        )
        if existing:
            continue
        psql(
            """
            INSERT INTO app.device_tokens (id, user_id, token, platform, created_at, updated_at)
            VALUES ('{0}', '{1}', '{2}', '{3}', '{4}', '{4}');
            """.format(
                str(uuid.uuid4()),
                user_id,
                "fcm-seed-" + str(uuid.uuid4()).replace("-", ""),
                "android" if i % 2 == 0 else "ios",
                now_iso,
            )
        )
        token_count += 1
    print(f"Device tokens: {token_count}")

    # ── 11. Health scores + transformation scores ────────────────────────
    hs_count = 0
    ts_count = 0
    for i, (pid, user_id, first, last, doc) in enumerate(patients):
        enrollment = fetch_rows(
            "SELECT id FROM app.program_enrollments WHERE patient_id = '{0}' LIMIT 1;".format(
                pid
            )
        )
        if not enrollment:
            continue
        exists_hs = fetch_rows(
            "SELECT 1 FROM app.health_scores WHERE patient_id = '{0}' LIMIT 1;".format(
                pid
            )
        )
        if not exists_hs:
            period_end = date.today() - timedelta(days=(i % 3))
            psql(
                """
                INSERT INTO app.health_scores (id, patient_id, score, score_previous,
                    score_adherence, score_clinical, score_nutrition, score_psychology,
                    score_exercise, trend, period_start, period_end, calculated_at, created_at, updated_at)
                VALUES ('{0}', '{1}', {2}, {3}, {4}, {5}, {6}, {7}, {8}, '{9}',
                    '{10}', '{10}', '{11}', '{11}', '{11}');
                """.format(
                    str(uuid.uuid4()),
                    pid,
                    45 + (i % 40),
                    40 + (i % 40),
                    30 + (i % 50),
                    55 + (i % 30),
                    25 + (i % 60),
                    50 + (i % 35),
                    35 + (i % 45),
                    "up" if i % 2 == 0 else "stable",
                    (period_end - timedelta(days=6)).isoformat(),
                    period_end.isoformat(),
                    now_iso,
                )
            )
            hs_count += 1
        exists_ts = fetch_rows(
            "SELECT 1 FROM app.transformation_scores WHERE patient_id = '{0}' LIMIT 1;".format(
                pid
            )
        )
        if not exists_ts:
            week_num = 1 + (i % 4)
            psql(
                """
                INSERT INTO app.transformation_scores (id, patient_id, score, score_previous,
                    week_number, detail, overall_trend, calculated_at, created_at, updated_at)
                VALUES ('{0}', '{1}', {2}, {3}, {4}, '{{}}', '{5}', '{6}', '{6}', '{6}');
                """.format(
                    str(uuid.uuid4()),
                    pid,
                    50 + (i % 30),
                    45 + (i % 30),
                    week_num,
                    "up" if i % 2 == 0 else "stable",
                    now_iso,
                )
            )
            ts_count += 1
    print(f"Health scores: {hs_count}, transformation scores: {ts_count}")

    # ── 12. Signos vitales batch (últimos 7 días) ────────────────────────
    vs_count = 0
    for i, (pid, user_id, first, last, doc) in enumerate(patients):
        recent = fetch_rows(
            "SELECT count(*) FROM app.vital_signs WHERE patient_id = '{0}' AND measured_at > now() - interval '14 days';".format(
                pid
            )
        )[0][0]
        if int(recent) >= 3:
            continue
        for d in range(1, 4):
            measured = NOW - timedelta(days=d, hours=(i % 6))
            systolic = 115 + (i % 4) * 8 + RANDOM.randint(-3, 3)
            psql(
                """
                INSERT INTO app.vital_signs (id, patient_id, measured_at, systolic, diastolic,
                    heart_rate, temperature_c, o2_saturation, height_cm, weight_kg, created_at)
                VALUES ('{0}', '{1}', '{2}', {3}, {4}, {5}, {6}, {7}, {8}, {9}, '{2}');
                """.format(
                    str(uuid.uuid4()),
                    pid,
                    measured.isoformat(),
                    systolic,
                    systolic - 40 + RANDOM.randint(-2, 2),
                    60 + (i % 4) * 5 + RANDOM.randint(0, 5),
                    round(36.5 + RANDOM.uniform(0, 0.6), 1),
                    95 + RANDOM.randint(0, 3),
                    158 + (i % 6),
                    round(58 + (i % 25) + RANDOM.uniform(0, 3), 1),
                )
            )
            vs_count += 1
    print(f"Signos vitales nuevos: {vs_count}")

    # ── Resumen ──────────────────────────────────────────────────────────
    print("\n=== RESUMEN FINAL ===")
    print(
        psql("""
        SELECT 'nutrition_plans=' || count(*) FROM app.nutrition_plans
        UNION ALL SELECT 'nutrition_plan_days=' || count(*) FROM app.nutrition_plan_days
        UNION ALL SELECT 'nutrition_plan_assignments=' || count(*) FROM app.nutrition_plan_assignments
        UNION ALL SELECT 'routine_assignments=' || count(*) FROM app.routine_assignments
        UNION ALL SELECT 'program_enrollments=' || count(*) FROM app.program_enrollments
        UNION ALL SELECT 'program_weeks=' || count(*) FROM app.program_weeks
        UNION ALL SELECT 'daily_checkins=' || count(*) FROM app.daily_checkins
        UNION ALL SELECT 'task_completions=' || count(*) FROM app.task_completions
        UNION ALL SELECT 'xp_ledger=' || count(*) FROM app.xp_ledger
        UNION ALL SELECT 'streak_states=' || count(*) FROM app.streak_states
        UNION ALL SELECT 'streak_freezes=' || count(*) FROM app.streak_freezes
        UNION ALL SELECT 'encounters=' || count(*) FROM app.encounters
        UNION ALL SELECT 'clinical_measurements=' || count(*) FROM app.clinical_measurements
        UNION ALL SELECT 'clinical_baselines=' || count(*) FROM app.clinical_baselines
        UNION ALL SELECT 'emotional_records=' || count(*) FROM app.emotional_records
        UNION ALL SELECT 'habit_checks=' || count(*) FROM app.habit_checks
        UNION ALL SELECT 'notifications=' || count(*) FROM app.notifications
        UNION ALL SELECT 'documents=' || count(*) FROM app.documents
        UNION ALL SELECT 'device_tokens=' || count(*) FROM app.device_tokens
        UNION ALL SELECT 'health_scores=' || count(*) FROM app.health_scores
        UNION ALL SELECT 'transformation_scores=' || count(*) FROM app.transformation_scores
        UNION ALL SELECT 'media_items=' || count(*) FROM app.media_items
        UNION ALL SELECT 'vital_signs=' || count(*) FROM app.vital_signs;
        """)
    )


if __name__ == "__main__":
    main()
