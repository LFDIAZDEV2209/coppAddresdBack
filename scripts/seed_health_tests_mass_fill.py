"""Seed masivo para Tests de Salud: llena evaluaciones, resultados, alertas y baterías adicionales.

Objetivo: poblar BD dev con datos realistas para que TODAS las vistas health-tests muestren gráficas, filtros y análisis IA con volumen.

- Convierte ~75% de asignaciones pending en evaluaciones started/completed con scoring real.
- Crea 2 baterías adicionales (Seguimiento y Cardiovascular) y asigna a subconjuntos de pacientes.
- Genera resultados (score, subescalas, indicadores) + alertas + comentarios.

Uso:
  cd ai-service
  uv run --with psycopg[binary] python ../coppAddresdBack/scripts/seed_health_tests_mass_fill.py

Idempotente: las pendientes ya completadas se omiten en re-ejecución.
"""

import random
import subprocess
import uuid
from datetime import datetime, timedelta

DB = "coppaddresd"
RND = random.Random(42)


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
        capture_output=True,
        check=True,
    )
    if "ERROR" in r.stderr:
        raise RuntimeError(f"SQL ERROR: {r.stderr[:800]}\nSQL: {sql[:400]}")
    return r.stdout


def fetch_rows(sql: str):
    out = psql(sql).strip()
    if not out:
        return []
    return [line.split("|") for line in out.splitlines() if line]


def main():
    print("=== Seed masivo Health Tests ===")

    # 1. Catálogos
    instruments = fetch_rows(
        "SELECT i.id, i.code, i.name, v.id, v.scoring_strategy FROM app.health_test_instruments i "
        "JOIN app.health_test_versions v ON v.instrument_id=i.id AND v.status='active' AND v.is_current "
        "WHERE i.is_active ORDER BY i.sort_order;"
    )
    print(f"Instrumentos: {len(instruments)}")
    # code -> (inst_id, version_id)
    inst_by_code = {r[1]: (r[0], r[3]) for r in instruments}
    version_ids = [r[3] for r in instruments]

    questions = fetch_rows(
        "SELECT q.id, q.version_id, q.code, COALESCE(q.section,''), q.scoring_direction, COALESCE(max(o.score_value)::text,'0') "
        "FROM app.health_test_questions q "
        "JOIN app.health_test_answer_options o ON o.question_id=q.id AND o.is_active "
        "WHERE q.is_active GROUP BY q.id ORDER BY q.sort_order;"
    )
    options = fetch_rows(
        "SELECT o.id, o.question_id, o.score_value::text, o.text FROM app.health_test_answer_options o WHERE o.is_active ORDER BY o.sort_order;"
    )
    ranges = fetch_rows(
        "SELECT r.version_id, r.min_value::text, r.max_value::text, r.label, r.severity FROM app.health_test_score_ranges r WHERE r.is_active;"
    )
    clinics = [
        r[0] for r in fetch_rows("SELECT id FROM erp.clinics ORDER BY name LIMIT 8;")
    ]
    professionals = fetch_rows(
        "SELECT p.id, e.user_id FROM erp.professionals p JOIN erp.employees e ON e.id=p.employee_id WHERE e.status='Active' ORDER BY random() LIMIT 30;"
    )
    battery_inicial = fetch_rows(
        "SELECT id FROM app.health_test_batteries WHERE code='bateria-inicial' LIMIT 1;"
    )
    if not battery_inicial:
        raise SystemExit("Batería inicial no existe. Corre migraciones.")
    battery_inicial_id = battery_inicial[0][0]

    # 2. Crear baterías adicionales si no existen
    extra_batteries = [
        (
            "bateria-seguimiento",
            "Seguimiento 30 días",
            "Control mensual: nutrición, movimiento, sueño, adherencia y estrés.",
            ["nutricional", "movimiento", "sueno", "iac-adresd", "ers"],
        ),
        (
            "bateria-cardiovascular",
            "Riesgo cardiovascular",
            "Tamizaje cardiometabólico: historia, ORP y estrés relacional.",
            ["historia-clinica", "orp", "ers"],
        ),
        (
            "bateria-adherencia",
            "Adherencia y propósito",
            "Motivación y barreras: adherencia IAC + propósito ANTARES.",
            ["iac-adresd", "bateria-antares", "temperamento"],
        ),
    ]
    for code, name, desc, codes in extra_batteries:
        exists = fetch_rows(
            f"SELECT id FROM app.health_test_batteries WHERE code='{code}' LIMIT 1;"
        )
        if exists:
            print(f"  batería {code} existe, skip")
            continue
        bid = str(uuid.uuid4())
        psql(
            f"INSERT INTO app.health_test_batteries (id, code, name, description, auto_assign_on_patient_create, is_active, created_at) VALUES ('{bid}','{code}','{name}','{desc}', false, true, now()) ON CONFLICT (code) DO NOTHING;"
        )
        print(f"  batería creada: {name} ({code})")
        # items
        for idx, ic in enumerate(codes):
            if ic not in inst_by_code:
                continue
            inst_id, ver_id = inst_by_code[ic]
            iid = str(uuid.uuid4())
            psql(
                f"INSERT INTO app.health_test_battery_items (id, battery_id, instrument_id, version_id, sort_order, is_required) VALUES ('{iid}','{bid}','{inst_id}','{ver_id}',{idx}, true) ON CONFLICT DO NOTHING;"
            )
        print(f"    -> {len(codes)} tests")

    # refrescar baterías
    batteries = fetch_rows(
        "SELECT id, code, name FROM app.health_test_batteries WHERE is_active ORDER BY code;"
    )
    print(f"Baterías activas: {len(batteries)} -> {[r[1] for r in batteries]}")
    # id -> testIds
    battery_items = fetch_rows(
        "SELECT battery_id, instrument_id, version_id FROM app.health_test_battery_items;"
    )
    battery_to_versions = {}
    for bid, iid, vid in battery_items:
        battery_to_versions.setdefault(bid, []).append(vid)

    options_by_q = {}
    for oid, qid, score, text in options:
        options_by_q.setdefault(qid, []).append((oid, float(score), text))
    questions_by_version = {}
    for qid, vid, code, section, direction, maxs in questions:
        questions_by_version.setdefault(vid, []).append(
            (qid, section, float(maxs or 0), direction)
        )
    ranges_by_version = {}
    for vid, mn, mx, label, sev in ranges:
        ranges_by_version.setdefault(vid, []).append((float(mn), float(mx), label, sev))

    def classify(score, vid):
        for mn, mx, label, sev in ranges_by_version.get(vid, []):
            if mn <= score <= mx:
                return label, sev
        nearest = min(
            ranges_by_version.get(vid, []),
            key=lambda r: min(abs(score - r[0]), abs(score - r[1])),
            default=None,
        )
        if nearest:
            return nearest[2], nearest[3]
        return "bajo", "low"

    def pick_responses(vid, target_frac):
        picks = []
        for qid, section, maxs, direction in questions_by_version.get(vid, []):
            opts = options_by_q.get(qid, [])
            if not opts:
                continue
            target = target_frac * maxs
            best = min(opts, key=lambda o: abs(o[1] - target))
            picks.append((qid, best[0], best[2]))
        return picks

    def compute_score(vid, picks):
        total, max_total = 0, 0
        for qid, oid, _ in picks:
            for o in options_by_q.get(qid, []):
                if o[0] == oid:
                    total += o[1]
                    break
        for qid, section, maxs, direction in questions_by_version.get(vid, []):
            max_total += maxs
        return total, max_total

    def subscale_scores(vid, picks):
        sums = {}
        for qid, oid, _ in picks:
            sec = ""
            for q in questions_by_version.get(vid, []):
                if q[0] == qid:
                    sec = q[1]
                    break
            for o in options_by_q.get(qid, []):
                if o[0] == oid:
                    sums[sec] = sums.get(sec, 0) + o[1]
                    break
        return sums

    # 3. Asignar baterías adicionales a subconjunto de pacientes (para diversidad de filtro)
    all_patients = fetch_rows(
        "SELECT id FROM app.patient_profiles WHERE status ILIKE 'activ%' ORDER BY random() LIMIT 120;"
    )
    # asignar seguimiento a 45 pacientes, cardiovascular a 35, adherencia a 30
    battery_assign_map = {}
    for code, name, desc, codes in extra_batteries:
        bid = fetch_rows(
            f"SELECT id FROM app.health_test_batteries WHERE code='{code}' LIMIT 1;"
        )[0][0]
        battery_assign_map[code] = bid

    # para cada batería extra, crear battery_assignments + health_test_assignments donde no existan
    for code, bid in battery_assign_map.items():
        cnt = 0
        # elegir pacientes aleatorios que no tengan ya esa batería
        candidates = fetch_rows(
            f"SELECT p.id FROM app.patient_profiles p WHERE p.status ILIKE 'activ%' AND NOT EXISTS (SELECT 1 FROM app.health_test_battery_assignments ba WHERE ba.patient_id=p.id AND ba.battery_id='{bid}') ORDER BY random() LIMIT 40;"
        )
        for (pid,) in candidates:
            ba_id = str(uuid.uuid4())
            assigned_at = (
                datetime(2026, 8, 1) + timedelta(days=RND.randint(0, 27))
            ).strftime("%Y-%m-%d 10:00:00+00")
            psql(
                f"INSERT INTO app.health_test_battery_assignments (id, patient_id, battery_id, status, assigned_at) VALUES ('{ba_id}','{pid}','{bid}','in_progress','{assigned_at}') ON CONFLICT DO NOTHING;"
            )
            # crear assignments por cada test de la batería
            for vid in battery_to_versions.get(bid, []):
                aid = str(uuid.uuid4())
                psql(
                    f"INSERT INTO app.health_test_assignments (id, patient_id, battery_assignment_id, version_id, status, assigned_by, assigned_at, expires_at, due_date) VALUES ('{aid}','{pid}','{ba_id}','{vid}','pending', NULL, '{assigned_at}', '{assigned_at}'::timestamptz + interval '30 days', '{assigned_at}'::timestamptz + interval '14 days') ON CONFLICT DO NOTHING;"
                )
                cnt += 1
        print(f"Asignaciones batería {code}: {cnt} tests asignados")

    # 4. Convertir pendientes en evaluaciones
    pending = fetch_rows(
        "SELECT a.id, a.patient_id, a.version_id, i.code FROM app.health_test_assignments a JOIN app.health_test_versions v ON v.id=a.version_id JOIN app.health_test_instruments i ON i.id=v.instrument_id WHERE a.status='pending' ORDER BY random() LIMIT 1000;"
    )
    print(f"Pendientes a procesar: {len(pending)}")
    # versión -> code map
    version_to_code = {r[3]: r[1] for r in instruments}

    eval_created = 0
    started_created = 0
    for idx, (aid, pid, vid, icode) in enumerate(pending):
        roll = RND.random()
        # distribución: 68% completed, 12% started, 20% remain pending
        if roll < 0.20:
            continue
        elif roll < 0.32:
            # started con respuestas parciales
            started_at = (
                datetime(2026, 8, 15)
                + timedelta(days=RND.randint(0, 15), hours=RND.randint(0, 12))
            ).strftime("%Y-%m-%d %H:%M:%S+00")
            psql(
                f"UPDATE app.health_test_assignments SET status='in_progress', started_at='{started_at}' WHERE id='{aid}';"
            )
            eval_id = str(uuid.uuid4())
            psql(
                f"INSERT INTO app.health_test_evaluations (id, assignment_id, patient_id, version_id, status, started_at) VALUES ('{eval_id}','{aid}','{pid}','{vid}','started','{started_at}') ON CONFLICT DO NOTHING;"
            )
            target = RND.uniform(0.3, 0.8)
            picks = pick_responses(vid, target)
            partial = picks[: max(1, int(len(picks) * 0.6))]
            if partial:
                vals = ",".join(
                    f"('{str(uuid.uuid4())}','{eval_id}','{qid}','{oid}','{started_at}')"
                    for qid, oid, _ in partial
                )
                psql(
                    f"INSERT INTO app.health_test_responses (id, evaluation_id, question_id, answer_option_id, created_at) VALUES {vals} ON CONFLICT DO NOTHING;"
                )
            started_created += 1
        else:
            # completed
            started_at = (
                datetime(2026, 8, 5)
                + timedelta(days=RND.randint(0, 20), hours=RND.randint(8, 16))
            ).strftime("%Y-%m-%d %H:%M:%S+00")
            completed_at = (
                datetime.strptime(started_at, "%Y-%m-%d %H:%M:%S+00")
                + timedelta(minutes=RND.randint(8, 25))
            ).strftime("%Y-%m-%d %H:%M:%S+00")
            target = RND.uniform(0.15, 0.95)
            # sesgar algunos pacientes a riesgo alto (para alertas)
            if RND.random() < 0.18:
                # forzar score alto para ORP o bajo para adherencia -> alerta
                if icode in ("orp", "iac-adresd"):
                    target = 0.85 if icode == "orp" else 0.15
                else:
                    target = RND.uniform(0.75, 0.95)
            picks = pick_responses(vid, target)
            total, max_total = compute_score(vid, picks)
            max_bound = max(
                (r[1] for r in ranges_by_version.get(vid, [])), default=max_total
            )
            score = round(min(total, max_bound), 2)
            pct = round(score / max_total * 100, 2) if max_total else 0
            qualifier, severity = classify(score, vid)

            psql(
                f"UPDATE app.health_test_assignments SET status='completed', completed_at='{completed_at}', started_at='{started_at}' WHERE id='{aid}';"
            )
            eval_id = str(uuid.uuid4())
            psql(
                f"INSERT INTO app.health_test_evaluations (id, assignment_id, patient_id, version_id, status, started_at, completed_at, score, score_percentage) VALUES ('{eval_id}','{aid}','{pid}','{vid}','completed','{started_at}','{completed_at}',{score},{pct}) ON CONFLICT DO NOTHING;"
            )
            vals = ",".join(
                f"('{str(uuid.uuid4())}','{eval_id}','{qid}','{oid}','{completed_at}')"
                for qid, oid, _ in picks
            )
            if vals:
                psql(
                    f"INSERT INTO app.health_test_responses (id, evaluation_id, question_id, answer_option_id, created_at) VALUES {vals} ON CONFLICT DO NOTHING;"
                )
            # resultados
            result_vals = []
            result_vals.append(
                f"('{str(uuid.uuid4())}','{eval_id}','score','{icode}','Score total',{score},'{qualifier}','{severity}','{completed_at}')"
            )
            for sec, sval in subscale_scores(vid, picks).items():
                if not sec:
                    continue
                squal, ssev = classify(sval, vid)
                sval_r = round(sval, 2)
                # escapar sec
                sec_esc = sec.replace("'", "''")
                result_vals.append(
                    f"('{str(uuid.uuid4())}','{eval_id}','subscale','{icode}.{sec_esc}','{sec_esc}',{sval_r},'{squal}','{ssev}','{completed_at}')"
                )
            if icode == "iac-adresd":
                # indicadores
                iapnea_val = round(RND.uniform(0, 100), 2)
                iapnea_sev = "high" if iapnea_val >= 70 else "low"
                iapnea_qual = "alto" if iapnea_sev == "high" else "bajo"
                result_vals.append(
                    f"('{str(uuid.uuid4())}','{eval_id}','indicator','iapnea','Sospecha de apnea',{iapnea_val},'{iapnea_qual}','{iapnea_sev}','{completed_at}')"
                )
                adh_val = round(RND.uniform(0, 100), 2)
                adh_sev = "high" if adh_val < 40 else "low"
                adh_qual = "bajo" if adh_sev == "high" else "alto"
                result_vals.append(
                    f"('{str(uuid.uuid4())}','{eval_id}','indicator','iadherencia','Índice de adherencia',{adh_val},'{adh_qual}','{adh_sev}','{completed_at}')"
                )
            # para orp, el score ya dispara alerta si alto/crítico; no necesita indicador extra
            psql(
                f"INSERT INTO app.health_test_results (id, evaluation_id, result_type, code, label, value, qualifier, severity, created_at) VALUES {','.join(result_vals)} ON CONFLICT DO NOTHING;"
            )
            eval_created += 1
            # cada 200 insert pausa leve
            if idx % 200 == 0 and idx > 0:
                print(
                    f"  ... {idx}/{len(pending)} procesados ({eval_created} completados)"
                )

    print(
        f"Evaluaciones completadas creadas: {eval_created}, en progreso: {started_created}"
    )

    # 5. Alertas desde reglas reales
    rules = fetch_rows(
        "SELECT id, code, name, severity, message_template FROM app.health_test_alert_rules WHERE is_active;"
    )
    rule_by_code = {r[1]: r for r in rules}
    map_code = {
        "orp": "orp_riesgo_alto",
        "iapnea": "iapnea_sospecha",
        "iadherencia": "adherencia_baja",
    }
    triggered = fetch_rows("""
        SELECT r.id, r.evaluation_id, r.code, r.qualifier, r.severity, r.value::text, e.patient_id
        FROM app.health_test_results r JOIN app.health_test_evaluations e ON e.id=r.evaluation_id
        WHERE (r.code='orp' AND r.result_type='score' AND r.severity IN ('high','critical'))
           OR (r.code='iapnea' AND r.result_type='indicator' AND r.severity='high')
           OR (r.code='iadherencia' AND r.result_type='indicator' AND r.severity='high');
    """)
    alert_cnt = 0
    for rid, eid, code, qual, sev, val, pid in triggered:
        rule_code = map_code.get(code)
        rule = rule_by_code.get(rule_code)
        if not rule:
            continue
        exists = fetch_rows(
            f"SELECT 1 FROM app.health_test_alerts WHERE result_id='{rid}' AND rule_id='{rule[0]}' LIMIT 1;"
        )
        if exists:
            continue
        title = rule[2].replace("'", "''")
        body = (
            (rule[4] or "Riesgo {label} ({value})")
            .format(label=qual or "", value=val)
            .replace("'", "''")
        )
        psql(
            f"INSERT INTO app.health_test_alerts (id, patient_id, result_id, rule_id, severity, title, body, status, created_at) VALUES ('{str(uuid.uuid4())}','{pid}','{rid}','{rule[0]}','{rule[3]}','{title}','{body}','active', now() - interval '{RND.randint(0, 10)} days') ON CONFLICT DO NOTHING;"
        )
        alert_cnt += 1
    print(f"Alertas creadas: {alert_cnt}")

    # 6. Comentarios (20% de evaluaciones completadas)
    completed_evals = fetch_rows("""
        SELECT e.id, e.patient_id, pp.professional_id, em.user_id
        FROM app.health_test_evaluations e
        JOIN app.patient_professionals pp ON pp.patient_id=e.patient_id
        JOIN erp.professionals p ON p.id=pp.professional_id
        JOIN erp.employees em ON em.id=p.employee_id
        WHERE e.status='completed' ORDER BY random() LIMIT 80;
    """)
    bodies = [
        "Evolución favorable respecto a evaluación anterior.",
        "Requiere seguimiento en 15 días.",
        "Score estable; mantener plan nutricional.",
        "Derivar a fisioterapia por limitación de movimiento.",
        "Buena adherencia; reforzar higiene de sueño.",
        "Ajustar dosis tras este resultado.",
        "Paciente motivado; excelente compromiso.",
        "Riesgo moderado; repetir evaluación en 30 días.",
        "Sin hallazgos nuevos; control habitual.",
        "Mejoría subjetiva consistente con resultado.",
    ]
    comment_cnt = 0
    for eid, pid, prof_id, user_id in completed_evals:
        if fetch_rows(
            f"SELECT 1 FROM app.health_test_comments WHERE evaluation_id='{eid}' LIMIT 1;"
        ):
            continue
        body = RND.choice(bodies).replace("'", "''")
        psql(
            f"INSERT INTO app.health_test_comments (id, patient_id, evaluation_id, author_id, body, created_at) VALUES ('{str(uuid.uuid4())}','{pid}','{eid}','{user_id}','{body}', now() - interval '{RND.randint(0, 18)} days') ON CONFLICT DO NOTHING;"
        )
        comment_cnt += 1
    print(f"Comentarios creados: {comment_cnt}")

    # resumen
    print(
        psql("""
        SELECT 'pacientes='||count(*) FROM app.patient_profiles
        UNION ALL SELECT 'baterias='||count(*) FROM app.health_test_batteries WHERE is_active
        UNION ALL SELECT 'asignaciones='||count(*) FROM app.health_test_assignments
        UNION ALL SELECT 'evaluaciones='||count(*) FROM app.health_test_evaluations
        UNION ALL SELECT 'resultados='||count(*) FROM app.health_test_results
        UNION ALL SELECT 'alertas='||count(*) FROM app.health_test_alerts
        UNION ALL SELECT 'battery_assignments='||count(*) FROM app.health_test_battery_assignments;
    """)
    )


if __name__ == "__main__":
    main()
