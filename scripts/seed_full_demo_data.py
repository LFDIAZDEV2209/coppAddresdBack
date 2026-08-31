"""Seed completo de datos de prueba relacionados para la BD dev coppaddresd.

Limpia los pacientes sinteticos sin documento (junk) y siembra:
- 24 pacientes realistas (usuarios auth + perfiles con aseguradora/clinica/ciudad)
- asignaciones profesional-paciente (patient_professionals)
- bateria inicial + asignaciones de los 9 instrumentos (pendientes, en curso, completados)
- evaluaciones con respuestas REALES (scoring coherente: opciones -> score -> rango -> qualifier/severity)
- resultados (score + subescalas por seccion + indicadores IAC/apnea)
- alertas desde las reglas reales (orp_riesgo_alto, iapnea_sospecha, adherencia_baja)
- comentarios de profesionales sobre evaluaciones
- intentos multiples (2-3) para varios pacientes del mismo test

Uso (desde coppAddresdBack/scripts):
  python seed_full_demo_data.py
Idempotente: puede re-ejecutarse (los pacientes se crean solo si el documento no existe).
"""

import random
import subprocess
import uuid

DB = "coppaddresd"
RANDOM = random.Random(7)

PASSWORD_HASH_SOURCE = (
    "PRUEBA"  # se copia el PasswordHash del usuario del paciente PRUEBA
)

PATIENTS = [
    # (nombre, apellido, genero, edad, documento, telefono)
    ("Andrea", "Salazar", "Femenino", 29, "1012345678", "3102458891"),
    ("Julián", "Restrepo", "Masculino", 41, "1023456789", "3204561234"),
    ("Valentina", "Ospina", "Femenino", 35, "1034567890", "3005672345"),
    ("Felipe", "Cárdenas", "Masculino", 52, "1045678901", "3116783456"),
    ("Mariana", "Patiño", "Femenino", 24, "1056789012", "3157894567"),
    ("Andrés", "Londoño", "Masculino", 63, "1067890123", "3168905678"),
    ("Catalina", "Vélez", "Femenino", 47, "1078901234", "3129016789"),
    ("Santiago", "Quintero", "Masculino", 19, "1089012345", "3180127890"),
    ("Daniela", "Arias", "Femenino", 33, "1090123456", "3191238901"),
    ("Camilo", "Montoya", "Masculino", 58, "1101234567", "3012349012"),
    ("Paula", "Ramírez", "Femenino", 27, "1112345678", "3023450123"),
    ("Esteban", "Gil", "Masculino", 44, "1123456789", "3034561234"),
    ("Laura", "Castaño", "Femenino", 31, "1134567890", "3045672345"),
    ("Nicolás", "Duarte", "Masculino", 66, "1145678901", "3056783456"),
    ("Gabriela", "Mejía", "Femenino", 22, "1156789012", "3067894567"),
    ("Ricardo", "Bermúdez", "Masculino", 49, "1167890123", "3078905678"),
    ("Sofía", "Toro", "Femenino", 38, "1178901234", "3089016789"),
    ("Mateo", "Zapata", "Masculino", 26, "1189012345", "3090127890"),
    ("Isabella", "Rojas", "Femenino", 55, "1190123456", "3101238901"),
    ("Sebastián", "Molina", "Masculino", 30, "1201234567", "3112349012"),
    ("Camila", "Herrera", "Femenino", 42, "1212345678", "3123450123"),
    ("Tomás", "Vargas", "Masculino", 36, "1223456789", "3134561234"),
    ("Ximena", "Pérez", "Femenino", 60, "1234567890", "3145672345"),
    ("Diego", "Moreno", "Masculino", 45, "1245678901", "3156783456"),
]

COMMENT_BODIES = [
    "El paciente presenta una evolución favorable respecto a la evaluación anterior.",
    "Se recomienda seguimiento en consulta de control en 30 días.",
    "El score se mantiene estable; continuar con el plan nutricional actual.",
    "Requiere refuerzo en actividad física; se remite a fisioterapia.",
    "Buen cumplimiento del plan; reforzar higiene de sueño.",
    "Se ajusta la dosis del plan tras revisar este resultado.",
    "Paciente motivado; excelente adherencia al programa.",
    "Se detecta riesgo moderado; programar nueva evaluación en 15 días.",
    "Sin hallazgos nuevos; mantener seguimiento habitual.",
    "El paciente refiere mejoría subjetiva consistente con el resultado.",
]


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
    # ---------- 0. Limpieza de pacientes junk ----------
    print(
        "Limpiando pacientes sintéticos (sin documento, sin user_id, sin referencias)..."
    )
    psql("""
    DELETE FROM app.patient_profiles p
    WHERE (p.document_number IS NULL OR p.document_number = '')
      AND p.user_id IS NULL
      AND NOT EXISTS (SELECT 1 FROM app.health_test_assignments a WHERE a.patient_id = p.id)
      AND NOT EXISTS (SELECT 1 FROM app.health_test_evaluations e WHERE e.patient_id = p.id)
      AND NOT EXISTS (SELECT 1 FROM app.patient_professionals pp WHERE pp.patient_id = p.id)
      AND NOT EXISTS (SELECT 1 FROM tele.appointments t WHERE t.patient_id = p.id)
      AND NOT EXISTS (SELECT 1 FROM tele.telemedicine_requests tr WHERE tr.patient_id = p.id)
      AND NOT EXISTS (SELECT 1 FROM app.program_enrollments pe WHERE pe.patient_id = p.id)
      AND NOT EXISTS (SELECT 1 FROM app.nutrition_plan_assignments npa WHERE npa.patient_id = p.id)
      AND NOT EXISTS (SELECT 1 FROM app.routine_assignments era WHERE era.patient_id = p.id)
      AND NOT EXISTS (SELECT 1 FROM app.encounters en WHERE en.patient_id = p.id)
      AND NOT EXISTS (SELECT 1 FROM app.documents d WHERE d.patient_id = p.id);
    """)
    print(
        psql(
            "SELECT 'pacientes restantes=' || count(*) FROM app.patient_profiles;"
        ).strip()
    )

    # ---------- 1. Catálogos de referencia ----------
    instruments = fetch_rows(
        "SELECT i.id, i.code, i.name, v.id, v.scoring_strategy FROM app.health_test_instruments i "
        "JOIN app.health_test_versions v ON v.instrument_id = i.id AND v.status = 'active' AND v.is_current "
        "WHERE i.is_active ORDER BY i.sort_order;"
    )
    print(f"Instrumentos: {len(instruments)}")

    questions = fetch_rows(
        "SELECT q.id, q.version_id, q.code, q.section, q.scoring_direction, "
        "       max(o.score_value)::text "
        "FROM app.health_test_questions q "
        "JOIN app.health_test_answer_options o ON o.question_id = q.id AND o.is_active "
        "WHERE q.is_active GROUP BY q.id ORDER BY q.sort_order;"
    )
    options = fetch_rows(
        "SELECT o.id, o.question_id, o.score_value::text, o.text FROM app.health_test_answer_options o "
        "WHERE o.is_active ORDER BY o.sort_order;"
    )
    ranges = fetch_rows(
        "SELECT r.version_id, r.min_value::text, r.max_value::text, r.label, r.severity "
        "FROM app.health_test_score_ranges r WHERE r.is_active;"
    )

    insurers = [r[0] for r in fetch_rows("SELECT id FROM app.insurers ORDER BY name;")]
    clinics = [
        r[0] for r in fetch_rows("SELECT id FROM erp.clinics ORDER BY name LIMIT 8;")
    ]
    professionals = fetch_rows(
        "SELECT p.id, e.user_id, e.first_name FROM erp.professionals p "
        "JOIN erp.employees e ON e.id = p.employee_id WHERE e.status = 'Active' ORDER BY e.first_name;"
    )
    battery_id = fetch_rows(
        "SELECT id FROM app.health_test_batteries WHERE is_active ORDER BY created_at LIMIT 1;"
    )[0][0]
    doc_types = [
        r[0]
        for r in fetch_rows("SELECT id FROM app.document_types ORDER BY name LIMIT 3;")
    ]
    countries = [
        r[0] for r in fetch_rows("SELECT id FROM app.countries ORDER BY name LIMIT 1;")
    ]
    states = [
        r[0] for r in fetch_rows("SELECT id FROM app.states ORDER BY name LIMIT 2;")
    ]
    cities = [
        r[0] for r in fetch_rows("SELECT id FROM app.cities ORDER BY name LIMIT 3;")
    ]
    blood_types = [
        r[0] for r in fetch_rows("SELECT id FROM app.blood_types ORDER BY name;")
    ]
    ethnicities = [
        r[0]
        for r in fetch_rows("SELECT id FROM app.ethnicities ORDER BY name LIMIT 3;")
    ]
    patient_user_id = fetch_rows(
        'SELECT "Id" FROM auth."Users" WHERE "UserName" = \'paciente.prueba@mediquer.com\' LIMIT 1;'
    )[0][0]
    password_hash = fetch_rows(
        'SELECT "PasswordHash" FROM auth."Users" WHERE "Id" = \'{0}\';'.format(
            patient_user_id
        )
    )[0][0]

    # pregunta -> opciones con score
    options_by_question: dict[str, list[tuple[str, float, str]]] = {}
    for oid, qid, score, text in options:
        options_by_question.setdefault(qid, []).append((oid, float(score), text))

    questions_by_version: dict[str, list[tuple[str, str, float, str]]] = {}
    for qid, vid, code, section, direction, max_score in questions:
        questions_by_version.setdefault(vid, []).append(
            (qid, section or "", float(max_score or 0), direction)
        )

    ranges_by_version: dict[str, list[tuple[float, float, str, str]]] = {}
    for vid, min_v, max_v, label, severity in ranges:
        ranges_by_version.setdefault(vid, []).append(
            (float(min_v), float(max_v), label, severity)
        )

    def classify(score: float, vid: str) -> tuple[str, str]:
        matches = [r for r in ranges_by_version.get(vid, []) if r[0] <= score <= r[1]]
        if matches:
            return matches[0][2], matches[0][3]
        # sin rango exacto: usar el más cercano (evita scores huérfanos)
        nearest = min(
            ranges_by_version.get(vid, []),
            key=lambda r: min(abs(score - r[0]), abs(score - r[1])),
            default=None,
        )
        if nearest:
            return nearest[2], nearest[3]
        return "bajo", "low"

    def pick_responses(vid: str, target_fraction: float) -> list[tuple[str, str, str]]:
        """Elige una opción por pregunta aproximando la fracción objetivo del score máximo."""
        picks = []
        for qid, section, max_score, direction in questions_by_version.get(vid, []):
            opts = options_by_question.get(qid, [])
            if not opts:
                continue
            target = target_fraction * max_score
            best = min(opts, key=lambda o: abs(o[1] - target))
            picks.append((qid, best[0], best[2]))
        return picks

    def compute_score(
        vid: str, picks: list[tuple[str, str, str]]
    ) -> tuple[float, float]:
        """Score real desde opciones + score máximo posible."""
        total, max_total = 0.0, 0.0
        for qid, oid, _ in picks:
            for o in options_by_question.get(qid, []):
                if o[0] == oid:
                    total += o[1]
                    break
        for qid, section, max_score, direction in questions_by_version.get(vid, []):
            max_total += max_score
        return total, max_total

    def subscale_scores(
        vid: str, picks: list[tuple[str, str, str]]
    ) -> dict[str, float]:
        """Score por sección (subescalas)."""
        sums: dict[str, float] = {}
        for qid, oid, _ in picks:
            section = ""
            for q in questions_by_version.get(vid, []):
                if q[0] == qid:
                    section = q[1]
                    break
            for o in options_by_question.get(qid, []):
                if o[0] == oid:
                    sums[section] = sums.get(section, 0.0) + o[1]
                    break
        return sums

    # ---------- 1b. Pacientes existentes sin usuario ni asignaciones ----------
    seed_docs = [p[4] for p in PATIENTS]
    extra = fetch_rows(
        "SELECT first_name, last_name, coalesce(gender, 'Femenino'), "
        "       coalesce(date_of_birth::text, ''), document_number, coalesce(phone_number, '') "
        "FROM app.patient_profiles p "
        "WHERE document_number IS NOT NULL AND document_number <> '' "
        "  AND document_number NOT IN ("
        + ",".join("'{}'".format(d) for d in seed_docs)
        + ")"
        "  AND status ILIKE 'activ%'"
        "  AND NOT EXISTS (SELECT 1 FROM app.health_test_assignments a WHERE a.patient_id = p.id)"
        "  AND id <> 'b6928024-89b4-4838-b57d-0e9519bf8eb5' "
        "ORDER BY p.created_at LIMIT 8;"
    )
    if extra:
        PATIENTS.extend(
            (f, l, g, 30 + (i * 5) % 30, d, ph)
            for i, (f, l, g, dob, d, ph) in enumerate(extra)
        )
        print(f"Pacientes existentes a relacionar: {len(extra)}")

    # ---------- 2. Usuarios + pacientes ----------
    now = "2026-08-31"
    for idx, (first, last, gender, age, doc, phone) in enumerate(PATIENTS):
        existing = fetch_rows(
            "SELECT id, user_id FROM app.patient_profiles WHERE document_number = '{0}';".format(
                doc
            )
        )
        if existing and existing[0][1]:
            print(f"  paciente {first} {last} ya tiene usuario, omitido")
            continue
        if existing:
            print(f"  paciente {first} {last} existía sin usuario; creando cuenta")
            existing_id = existing[0][0]
            user_id = str(uuid.uuid4())
            email = f"paciente.{idx + 1}@mediquer.com"
            psql(
                """
            INSERT INTO auth."Users" ("Id", "UserName", "NormalizedUserName", "Email",
                "NormalizedEmail", "FirstName", "LastName", "EmailConfirmed",
                "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled",
                "AccessFailedCount", "IsActive", "CreatedAt", "UpdatedAt",
                "PasswordHash", "SecurityStamp", "ConcurrencyStamp")
            VALUES ('{0}', '{1}', '{2}', '{1}', '{2}', '{3}', '{4}', true,
                false, false, true, 0, true, '{5}', '{5}', '{6}', '{7}', '{7}');
            """.format(
                    user_id,
                    email,
                    email.upper(),
                    first,
                    last,
                    now,
                    password_hash.replace("'", "''"),
                    str(uuid.uuid4()),
                )
            )
            psql(
                "UPDATE app.patient_profiles SET user_id = '{0}', updated_at = '{1}' "
                "WHERE id = '{2}';".format(user_id, now, existing_id)
            )
            continue
        user_id = str(uuid.uuid4())
        email = f"paciente.{idx + 1}@mediquer.com"
        dob = f"{2026 - age}-{(idx % 12) + 1:02d}-{((idx * 7) % 27) + 1:02d}"
        existing_user = fetch_rows(
            'SELECT "Id" FROM auth."Users" WHERE "UserName" = \'{0}\' LIMIT 1;'.format(
                email
            )
        )
        if existing_user:
            user_id = existing_user[0][0]
        else:
            psql(
                """
        INSERT INTO auth."Users" ("Id", "UserName", "NormalizedUserName", "Email",
            "NormalizedEmail", "FirstName", "LastName", "EmailConfirmed",
            "PhoneNumberConfirmed", "TwoFactorEnabled", "LockoutEnabled",
            "AccessFailedCount", "IsActive", "CreatedAt", "UpdatedAt",
            "PasswordHash", "SecurityStamp", "ConcurrencyStamp")
        VALUES ('{0}', '{1}', '{2}', '{1}', '{2}', '{3}', '{4}', true,
            false, false, true, 0, true, '{5}', '{5}', '{6}', '{7}', '{7}');
        """.format(
                    user_id,
                    email,
                    email.upper(),
                    first,
                    last,
                    now,
                    password_hash.replace("'", "''"),
                    str(uuid.uuid4()),
                )
            )
        psql(
            """
        INSERT INTO app.patient_profiles (id, user_id, first_name, last_name,
            document_number, date_of_birth, gender, phone_country_code, phone_number,
            email, insurer_id, clinic_id, document_type_id, country_id, state_id,
            city_id, blood_type_id, ethnicity_id, status, created_at, updated_at)
        VALUES ('{0}', '{1}', '{2}', '{3}', '{4}', '{5}', '{6}', '+57', '{7}',
            '{8}', '{9}', '{10}', '{11}', '{12}', '{13}', '{14}', '{15}', '{16}', 'Activo', '{17}', '{17}');
        """.format(
                str(uuid.uuid4()),
                user_id,
                first,
                last,
                doc,
                dob,
                gender,
                phone,
                email,
                insurers[idx % len(insurers)],
                clinics[idx % len(clinics)],
                doc_types[idx % len(doc_types)],
                countries[0],
                states[idx % len(states)],
                cities[idx % len(cities)],
                blood_types[idx % len(blood_types)],
                ethnicities[idx % len(ethnicities)],
                now,
            )
        )
    print(f"Pacientes sembrados: {len(PATIENTS)}")

    # ---------- 3. Relación paciente-profesional ----------
    patient_rows = fetch_rows(
        "SELECT id, document_number FROM app.patient_profiles WHERE document_number IN ("
        + ",".join("'{}'".format(p[4]) for p in PATIENTS)
        + ");"
    )
    relation_types = fetch_rows(
        "SELECT DISTINCT relationship_type FROM app.patient_professionals;"
    )
    rel_type = relation_types[0][0] if relation_types else "Primario"
    pp_count = 0
    for i, (pid, doc) in enumerate(patient_rows):
        prof_id, prof_user_id, _ = professionals[i % len(professionals)]
        exists = fetch_rows(
            "SELECT 1 FROM app.patient_professionals WHERE patient_id = '{0}' AND professional_id = '{1}';".format(
                pid, prof_id
            )
        )
        if exists:
            continue
        psql(
            """
        INSERT INTO app.patient_professionals (patient_id, professional_id, clinic_id,
            relationship_type, status, created_at, updated_at)
        VALUES ('{0}', '{1}', '{2}', '{3}', 'Active', '{4}', '{4}');
        """.format(pid, prof_id, clinics[i % len(clinics)], rel_type, now)
        )
        pp_count += 1
    print(f"Relaciones paciente-profesional creadas: {pp_count}")

    # ---------- 4. Asignaciones + evaluaciones + respuestas + resultados ----------
    # Pacientes incompletos (menos de 9 asignaciones) se re-sembran completos.
    patient_rows = fetch_rows(
        "SELECT p.id, p.document_number, count(a.id)::int "
        "FROM app.patient_profiles p "
        "LEFT JOIN app.health_test_assignments a ON a.patient_id = p.id "
        "WHERE p.document_number IN ("
        + ",".join("'{}'".format(p[4]) for p in PATIENTS)
        + ") GROUP BY p.id HAVING count(a.id) < 9;"
    )
    print(f"Pacientes incompletos (a re-sembrar): {len(patient_rows)}")

    def delete_patient_tests(pid: str) -> None:
        psql(f"""
        DELETE FROM app.health_test_alerts WHERE patient_id = '{pid}';
        DELETE FROM app.health_test_comments WHERE patient_id = '{pid}';
        DELETE FROM app.health_test_results
          WHERE evaluation_id IN (SELECT id FROM app.health_test_evaluations WHERE patient_id = '{pid}');
        DELETE FROM app.health_test_responses
          WHERE evaluation_id IN (SELECT id FROM app.health_test_evaluations WHERE patient_id = '{pid}');
        DELETE FROM app.health_test_evaluations WHERE patient_id = '{pid}';
        DELETE FROM app.health_test_assignments WHERE patient_id = '{pid}';
        DELETE FROM app.health_test_battery_assignments WHERE patient_id = '{pid}';
        """)

    statuses_by_patient: list[list[str]] = []
    for i in range(len(patient_rows)):
        # 9 tests: completado x6, en curso x1, pendiente x2 (varía un poco)
        statuses = ["completed"] * 6 + ["started"] * 1 + ["pending"] * 2
        statuses = statuses[:9]
        if i % 3 == 0:
            statuses = ["completed"] * 7 + ["pending"] * 2
        elif i % 3 == 1:
            statuses = ["completed"] * 5 + ["started"] * 2 + ["pending"] * 2
        statuses_by_patient.append(statuses)

    eval_count = 0
    for i, (pid, doc, existing_count) in enumerate(patient_rows):
        delete_patient_tests(pid)
        battery_assignment_id = str(uuid.uuid4())
        battery_status = (
            "completed"
            if statuses_by_patient[i].count("pending") == 0
            else "in_progress"
        )
        psql(
            """
        INSERT INTO app.health_test_battery_assignments (id, patient_id, battery_id,
            status, assigned_at)
        VALUES ('{0}', '{1}', '{2}', '{3}', '{4}');
        """.format(battery_assignment_id, pid, battery_id, battery_status, now)
        )

        for j, (inst_id, inst_code, inst_name, vid, strategy) in enumerate(instruments):
            status = statuses_by_patient[i][j]
            assignment_id = str(uuid.uuid4())
            assigned_at = "2026-08-{0:02d} 09:00:00+00".format((i * 3 + j) % 28 + 1)
            if status == "pending":
                psql(
                    """
                INSERT INTO app.health_test_assignments (id, patient_id,
                    battery_assignment_id, version_id, status, assigned_by, assigned_at,
                    expires_at, due_date)
                VALUES ('{0}', '{1}', '{2}', '{3}', 'pending', NULL, '{4}',
                    '{4}'::timestamptz + interval '30 days', '{4}'::timestamptz + interval '14 days');
                """.format(assignment_id, pid, battery_assignment_id, vid, assigned_at)
                )
                continue

            # intentos adicionales para algunos pacientes/tests
            attempts = 1
            if i % 4 == 0 and j in (2, 4):
                attempts = 3
            elif i % 4 == 2 and j == 5:
                attempts = 2

            for attempt in range(attempts):
                started_at = "2026-08-{0:02d} 10:00:00+00".format(
                    (i * 3 + j + attempt) % 28 + 1
                )
                completed_at = "2026-08-{0:02d} 10:20:00+00".format(
                    (i * 3 + j + attempt) % 28 + 1
                )
                target_fraction = RANDOM.uniform(0.15, 0.95)
                picks = pick_responses(vid, target_fraction)
                total, max_total = compute_score(vid, picks)
                # tope: el score máximo posible puede exceder el rango superior de la versión
                max_bound = max(
                    (r[1] for r in ranges_by_version.get(vid, [])), default=max_total
                )
                score = round(min(total, max_bound), 2)
                pct = round(score / max_total * 100, 2) if max_total else 0.0
                qualifier, severity = classify(score, vid)

                if status == "started" and attempt == 0:
                    psql(
                        """
                    INSERT INTO app.health_test_assignments (id, patient_id,
                        battery_assignment_id, version_id, status, assigned_by, assigned_at,
                        started_at)
                    VALUES ('{0}', '{1}', '{2}', '{3}', 'in_progress', NULL, '{4}', '{5}');
                    """.format(
                            assignment_id,
                            pid,
                            battery_assignment_id,
                            vid,
                            assigned_at,
                            started_at,
                        )
                    )
                    eval_id = str(uuid.uuid4())
                    psql(
                        """
                    INSERT INTO app.health_test_evaluations (id, assignment_id, patient_id,
                        version_id, status, started_at)
                    VALUES ('{0}', '{1}', '{2}', '{3}', 'started', '{4}');
                    """.format(eval_id, assignment_id, pid, vid, started_at)
                    )
                    # respuestas parciales (60%)
                    partial = picks[: max(1, int(len(picks) * 0.6))]
                    partial_values = ",".join(
                        "('{0}', '{1}', '{2}', '{3}', '{4}')".format(
                            str(uuid.uuid4()), eval_id, qid, oid, started_at
                        )
                        for qid, oid, _ in partial
                    )
                    psql(
                        "INSERT INTO app.health_test_responses "
                        "(id, evaluation_id, question_id, answer_option_id, created_at) "
                        "VALUES " + partial_values + ";"
                    )
                    eval_count += 1
                    break

                if attempt == 0:
                    psql(
                        """
                    INSERT INTO app.health_test_assignments (id, patient_id,
                        battery_assignment_id, version_id, status, assigned_by, assigned_at,
                        completed_at)
                    VALUES ('{0}', '{1}', '{2}', '{3}', 'completed', NULL, '{4}', '{5}');
                    """.format(
                            assignment_id,
                            pid,
                            battery_assignment_id,
                            vid,
                            assigned_at,
                            completed_at,
                        )
                    )
                else:
                    assignment_id = str(uuid.uuid4())
                    psql(
                        """
                    INSERT INTO app.health_test_assignments (id, patient_id,
                        battery_assignment_id, version_id, status, assigned_by, assigned_at,
                        completed_at)
                    VALUES ('{0}', '{1}', '{2}', '{3}', 'completed', NULL,
                        '{4}'::timestamptz - interval '14 days', '{4}'::timestamptz - interval '13 days');
                    """.format(
                            assignment_id, pid, battery_assignment_id, vid, assigned_at
                        )
                    )

                eval_id = str(uuid.uuid4())
                psql(
                    """
                INSERT INTO app.health_test_evaluations (id, assignment_id, patient_id,
                    version_id, status, started_at, completed_at, score, score_percentage)
                VALUES ('{0}', '{1}', '{2}', '{3}', 'completed', '{4}', '{5}', {6}, {7});
                """.format(
                        eval_id,
                        assignment_id,
                        pid,
                        vid,
                        started_at,
                        completed_at,
                        score,
                        pct,
                    )
                )

                resp_values = ",".join(
                    "('{0}', '{1}', '{2}', '{3}', '{4}')".format(
                        str(uuid.uuid4()), eval_id, qid, oid, completed_at
                    )
                    for qid, oid, _ in picks
                )
                psql(
                    "INSERT INTO app.health_test_responses "
                    "(id, evaluation_id, question_id, answer_option_id, created_at) "
                    "VALUES " + resp_values + ";"
                )

                # resultado score (code = código del instrumento)
                result_values = [
                    "('{0}', '{1}', 'score', '{2}', 'Score total', {3}, '{4}', '{5}', '{6}')".format(
                        str(uuid.uuid4()),
                        eval_id,
                        inst_code,
                        score,
                        qualifier,
                        severity,
                        completed_at,
                    )
                ]

                # subescalas por sección
                for section, sval in subscale_scores(vid, picks).items():
                    if not section:
                        continue
                    squal, ssev = classify(sval, vid)
                    result_values.append(
                        "('{0}', '{1}', 'subscale', '{2}', '{3}', {4}, '{5}', '{6}', '{7}')".format(
                            str(uuid.uuid4()),
                            eval_id,
                            inst_code + "." + section,
                            section,
                            round(sval, 2),
                            squal,
                            ssev,
                            completed_at,
                        )
                    )

                # indicadores para iac-adresd
                if inst_code == "iac-adresd":
                    iapnea_val = round(RANDOM.uniform(0, 100), 2)
                    iapnea_sev = "high" if iapnea_val >= 70 else "low"
                    iapnea_qual = "alto" if iapnea_sev == "high" else "bajo"
                    result_values.append(
                        "('{0}', '{1}', 'indicator', 'iapnea', 'Sospecha de apnea', {2}, '{3}', '{4}', '{5}')".format(
                            str(uuid.uuid4()),
                            eval_id,
                            iapnea_val,
                            iapnea_qual,
                            iapnea_sev,
                            completed_at,
                        )
                    )
                    adh_val = round(RANDOM.uniform(0, 100), 2)
                    adh_sev = "high" if adh_val < 40 else "low"
                    adh_qual = "bajo" if adh_sev == "high" else "alto"
                    result_values.append(
                        "('{0}', '{1}', 'indicator', 'iadherencia', 'Índice de adherencia', {2}, '{3}', '{4}', '{5}')".format(
                            str(uuid.uuid4()),
                            eval_id,
                            adh_val,
                            adh_qual,
                            adh_sev,
                            completed_at,
                        )
                    )

                psql(
                    "INSERT INTO app.health_test_results "
                    "(id, evaluation_id, result_type, code, label, value, qualifier, severity, created_at) "
                    "VALUES " + ",".join(result_values) + ";"
                )

                eval_count += 1

    print(f"Evaluaciones creadas: {eval_count}")

    # ---------- 5. Alertas desde reglas reales ----------
    rules = fetch_rows(
        "SELECT id, code, name, severity, condition, message_template FROM app.health_test_alert_rules "
        "WHERE is_active;"
    )
    # map result code -> rule (el rule.code es 'orp_riesgo_alto', el resultado usa 'orp')
    result_to_rule_code = {
        "orp": "orp_riesgo_alto",
        "iapnea": "iapnea_sospecha",
        "iadherencia": "adherencia_baja",
    }
    rule_by_code = {r[1]: r for r in rules}
    # buscar resultados que disparan reglas
    triggered = fetch_rows("""
    SELECT r.id, r.evaluation_id, r.code, r.qualifier, r.severity, r.value::text, e.patient_id
    FROM app.health_test_results r
    JOIN app.health_test_evaluations e ON e.id = r.evaluation_id
    WHERE (r.code = 'orp' AND r.result_type = 'score' AND r.severity IN ('high', 'critical'))
       OR (r.code = 'iapnea' AND r.result_type = 'indicator' AND r.severity = 'high')
       OR (r.code = 'iadherencia' AND r.result_type = 'indicator' AND r.severity = 'high');
    """)
    alert_count = 0
    for result_id, eval_id, code, qualifier, severity, value, patient_id in triggered:
        rule_code = result_to_rule_code.get(code)
        rule = rule_by_code.get(rule_code) if rule_code else None
        if rule is None:
            continue
        exists = fetch_rows(
            "SELECT 1 FROM app.health_test_alerts WHERE result_id = '{0}' AND rule_id = '{1}';".format(
                result_id, rule[0]
            )
        )
        if exists:
            continue
        title = rule[2]
        body = (rule[5] or "Riesgo {label} ({value})").format(
            label=qualifier or "", value=value
        )
        psql(
            """
        INSERT INTO app.health_test_alerts (id, patient_id, result_id, rule_id, severity,
            title, body, status, created_at)
        VALUES ('{0}', '{1}', '{2}', '{3}', '{4}', '{5}', '{6}', 'active', now());
        """.format(
                str(uuid.uuid4()),
                patient_id,
                result_id,
                rule[0],
                rule[3],
                title.replace("'", "''"),
                body.replace("'", "''"),
            )
        )
        alert_count += 1
    print(f"Alertas creadas: {alert_count}")

    # ---------- 6. Comentarios de profesionales ----------
    comment_count = 0
    completed_evals = fetch_rows("""
    SELECT e.id, e.patient_id, pp.professional_id, e2.user_id
    FROM app.health_test_evaluations e
    JOIN app.patient_professionals pp ON pp.patient_id = e.patient_id
    JOIN erp.professionals p ON p.id = pp.professional_id
    JOIN erp.employees e2 ON e2.id = p.employee_id
    WHERE e.status = 'completed'
    ORDER BY random()
    LIMIT 40;
    """)
    for eval_id, patient_id, prof_id, prof_user_id in completed_evals:
        exists = fetch_rows(
            "SELECT 1 FROM app.health_test_comments WHERE evaluation_id = '{0}';".format(
                eval_id
            )
        )
        if exists:
            continue
        body = RANDOM.choice(COMMENT_BODIES)
        psql(
            """
        INSERT INTO app.health_test_comments (id, patient_id, evaluation_id, author_id,
            body, created_at)
        VALUES ('{0}', '{1}', '{2}', '{3}', '{4}', now() - interval '{5} days');
        """.format(
                str(uuid.uuid4()),
                patient_id,
                eval_id,
                prof_user_id,
                body.replace("'", "''"),
                RANDOM.randint(0, 20),
            )
        )
        comment_count += 1
    print(f"Comentarios creados: {comment_count}")

    # ---------- 7. Resumen ----------
    print("\n=== RESUMEN FINAL ===")
    print(
        psql("""
    SELECT 'pacientes=' || count(*) FROM app.patient_profiles
    UNION ALL SELECT 'users=' || count(*) FROM auth."Users"
    UNION ALL SELECT 'patient_professionals=' || count(*) FROM app.patient_professionals
    UNION ALL SELECT 'asignaciones=' || count(*) FROM app.health_test_assignments
    UNION ALL SELECT 'evaluaciones=' || count(*) FROM app.health_test_evaluations
    UNION ALL SELECT 'respuestas=' || count(*) FROM app.health_test_responses
    UNION ALL SELECT 'resultados=' || count(*) FROM app.health_test_results
    UNION ALL SELECT 'alertas=' || count(*) FROM app.health_test_alerts
    UNION ALL SELECT 'comentarios=' || count(*) FROM app.health_test_comments;
    """)
    )


if __name__ == "__main__":
    main()
