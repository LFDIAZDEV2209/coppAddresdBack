"""Seed masivo directo via psycopg (rápido, sin docker exec por fila)."""

import random
import uuid
from datetime import datetime, timedelta

import psycopg

DSN = "host=localhost port=5432 dbname=coppaddresd user=app_user password=CoppAddresdDev!2026"
RND = random.Random(42)


def fetch(conn, sql, params=None):
    with conn.cursor() as cur:
        cur.execute(sql, params or ())
        try:
            return cur.fetchall()
        except psycopg.ProgrammingError:
            return []


def exec_sql(conn, sql, params=None):
    with conn.cursor() as cur:
        cur.execute(sql, params or ())


def main():
    conn = psycopg.connect(DSN, autocommit=False)
    print("Conectado")
    # instrumentos
    with conn.cursor() as cur:
        cur.execute("""
            SELECT i.id, i.code, i.name, v.id, v.scoring_strategy
            FROM app.health_test_instruments i
            JOIN app.health_test_versions v ON v.instrument_id=i.id AND v.status='active' AND v.is_current
            WHERE i.is_active ORDER BY i.sort_order;
        """)
        instruments = cur.fetchall()
    print(f"Instrumentos {len(instruments)}")
    inst_by_code = {r[1]: (r[0], r[3]) for r in instruments}

    # questions
    with conn.cursor() as cur:
        cur.execute("""
            SELECT q.id, q.version_id, COALESCE(q.section,''), q.scoring_direction, COALESCE(max(o.score_value)::text,'0')
            FROM app.health_test_questions q
            JOIN app.health_test_answer_options o ON o.question_id=q.id AND o.is_active
            WHERE q.is_active GROUP BY q.id ORDER BY q.sort_order;
        """)
        qrows = cur.fetchall()
    # options
    with conn.cursor() as cur:
        cur.execute(
            "SELECT o.id, o.question_id, o.score_value::text FROM app.health_test_answer_options o WHERE o.is_active ORDER BY o.sort_order;"
        )
        orows = cur.fetchall()
    with conn.cursor() as cur:
        cur.execute(
            "SELECT r.version_id, r.min_value::text, r.max_value::text, r.label, r.severity FROM app.health_test_score_ranges r WHERE r.is_active;"
        )
        rrows = cur.fetchall()

    options_by_q = {}
    for oid, qid, score in orows:
        options_by_q.setdefault(str(qid), []).append((str(oid), float(score)))
    questions_by_version = {}
    for qid, vid, sec, direction, maxs in qrows:
        questions_by_version.setdefault(str(vid), []).append(
            (str(qid), sec, float(maxs or 0), direction)
        )
    ranges_by_version = {}
    for vid, mn, mx, label, sev in rrows:
        ranges_by_version.setdefault(str(vid), []).append(
            (float(mn), float(mx), label, sev)
        )

    def classify(score, vid):
        for mn, mx, label, sev in ranges_by_version.get(str(vid), []):
            if mn <= score <= mx:
                return label, sev
        nearest = min(
            ranges_by_version.get(str(vid), []),
            key=lambda r: min(abs(score - r[0]), abs(score - r[1])),
            default=None,
        )
        if nearest:
            return nearest[2], nearest[3]
        return "bajo", "low"

    def pick_responses(vid, target_frac):
        picks = []
        for qid, sec, maxs, direction in questions_by_version.get(str(vid), []):
            opts = options_by_q.get(qid, [])
            if not opts:
                continue
            target = target_frac * maxs
            best = min(opts, key=lambda o: abs(o[1] - target))
            picks.append((qid, best[0]))
        return picks

    def compute_score(vid, picks):
        total, max_total = 0, 0
        for qid, oid in picks:
            for o in options_by_q.get(qid, []):
                if o[0] == oid:
                    total += o[1]
                    break
        for qid, sec, maxs, direction in questions_by_version.get(str(vid), []):
            max_total += maxs
        return total, max_total

    # crear baterías adicionales si faltan
    with conn.cursor() as cur:
        cur.execute("SELECT code FROM app.health_test_batteries WHERE is_active;")
        existing_codes = {r[0] for r in cur.fetchall()}
    extra = [
        (
            "bateria-seguimiento",
            "Seguimiento 30 días",
            "Control mensual: nutrición, movimiento, sueño, adherencia y estrés.",
            ["nutricional", "movimiento", "sueno", "iac-adresd", "ers"],
        ),
        (
            "bateria-cardiovascular",
            "Riesgo cardiovascular",
            "Tamizaje cardiometabólico: historia, ORP y estrés.",
            ["historia-clinica", "orp", "ers"],
        ),
        (
            "bateria-adherencia",
            "Adherencia y propósito",
            "Motivación y barreras: IAC + ANTARES + temperamento.",
            ["iac-adresd", "bateria-antares", "temperamento"],
        ),
    ]
    for code, name, desc, codes in extra:
        if code in existing_codes:
            print(f"batería {code} existe")
            continue
        bid = str(uuid.uuid4())
        with conn.cursor() as cur:
            cur.execute(
                "INSERT INTO app.health_test_batteries (id, code, name, description, auto_assign_on_patient_create, is_active, created_at) VALUES (%s,%s,%s,%s,false,true,now()) ON CONFLICT (code) DO NOTHING;",
                (bid, code, name, desc),
            )
        conn.commit()
        print(f"batería creada {code}")
        for idx, ic in enumerate(codes):
            if ic not in inst_by_code:
                continue
            inst_id, ver_id = inst_by_code[ic]
            iid = str(uuid.uuid4())
            with conn.cursor() as cur:
                cur.execute(
                    "INSERT INTO app.health_test_battery_items (id, battery_id, instrument_id, version_id, sort_order, is_required) VALUES (%s,%s,%s,%s,%s,true) ON CONFLICT DO NOTHING;",
                    (iid, bid, inst_id, ver_id, idx),
                )
        conn.commit()
        print(f"  {len(codes)} items")

    # refrescar baterías
    with conn.cursor() as cur:
        cur.execute("SELECT id, code FROM app.health_test_batteries WHERE is_active;")
        batteries = {r[1]: str(r[0]) for r in cur.fetchall()}
        cur.execute("SELECT battery_id, version_id FROM app.health_test_battery_items;")
        b_to_vs = {}
        for bid, vid in cur.fetchall():
            b_to_vs.setdefault(str(bid), []).append(str(vid))
    print(f"Baterías {list(batteries.keys())}")

    # asignar baterías extra a ~40 pacientes cada una
    with conn.cursor() as cur:
        cur.execute(
            "SELECT id FROM app.patient_profiles WHERE status ILIKE 'activ%' ORDER BY random() LIMIT 150;"
        )
        patient_ids = [str(r[0]) for r in cur.fetchall()]
    for code in ["bateria-seguimiento", "bateria-cardiovascular", "bateria-adherencia"]:
        bid = batteries[code]
        vids = b_to_vs.get(bid, [])
        candidates = RND.sample(patient_ids, 35)
        for pid in candidates:
            with conn.cursor() as cur:
                cur.execute(
                    "SELECT 1 FROM app.health_test_battery_assignments WHERE patient_id=%s AND battery_id=%s LIMIT 1;",
                    (pid, bid),
                )
                if cur.fetchone():
                    continue
            ba_id = str(uuid.uuid4())
            assigned_at = datetime(2026, 8, 1) + timedelta(days=RND.randint(0, 27))
            with conn.cursor() as cur:
                cur.execute(
                    "INSERT INTO app.health_test_battery_assignments (id, patient_id, battery_id, status, assigned_at) VALUES (%s,%s,%s,'in_progress',%s) ON CONFLICT DO NOTHING;",
                    (ba_id, pid, bid, assigned_at),
                )
            for vid in vids:
                aid = str(uuid.uuid4())
                try:
                    with conn.cursor() as cur:
                        cur.execute(
                            "INSERT INTO app.health_test_assignments (id, patient_id, battery_assignment_id, version_id, status, assigned_at, expires_at, due_date) VALUES (%s,%s,%s,%s,'pending',%s,%s,%s) ON CONFLICT DO NOTHING;",
                            (
                                aid,
                                pid,
                                ba_id,
                                vid,
                                assigned_at,
                                assigned_at + timedelta(days=30),
                                assigned_at + timedelta(days=14),
                            ),
                        )
                except:
                    pass
        conn.commit()
        print(f"Asignaciones {code} listas")

    # pendientes a convertir
    with conn.cursor() as cur:
        cur.execute("""
            SELECT a.id, a.patient_id, a.version_id, i.code
            FROM app.health_test_assignments a
            JOIN app.health_test_versions v ON v.id=a.version_id
            JOIN app.health_test_instruments i ON i.id=v.instrument_id
            WHERE a.status='pending'
            ORDER BY random() LIMIT 900;
        """)
        pending = [(str(r[0]), str(r[1]), str(r[2]), r[3]) for r in cur.fetchall()]
    print(f"Pendientes {len(pending)}")

    eval_c = 0
    started_c = 0
    for idx, (aid, pid, vid, icode) in enumerate(pending):
        roll = RND.random()
        if roll < 0.22:
            continue
        elif roll < 0.34:
            started_at = datetime(2026, 8, 15) + timedelta(
                days=RND.randint(0, 15), hours=RND.randint(0, 12)
            )
            with conn.cursor() as cur:
                cur.execute(
                    "UPDATE app.health_test_assignments SET status='in_progress', started_at=%s WHERE id=%s;",
                    (started_at, aid),
                )
                eval_id = str(uuid.uuid4())
                cur.execute(
                    "INSERT INTO app.health_test_evaluations (id, assignment_id, patient_id, version_id, status, started_at) VALUES (%s,%s,%s,%s,'started',%s) ON CONFLICT DO NOTHING;",
                    (eval_id, aid, pid, vid, started_at),
                )
                target = RND.uniform(0.3, 0.8)
                picks = pick_responses(vid, target)
                partial = picks[: max(1, int(len(picks) * 0.55))]
                for qid, oid in partial:
                    rid = str(uuid.uuid4())
                    cur.execute(
                        "INSERT INTO app.health_test_responses (id, evaluation_id, question_id, answer_option_id, created_at) VALUES (%s,%s,%s,%s,%s) ON CONFLICT DO NOTHING;",
                        (rid, eval_id, qid, oid, started_at),
                    )
            started_c += 1
        else:
            started_at = datetime(2026, 8, 5) + timedelta(
                days=RND.randint(0, 20), hours=RND.randint(8, 16)
            )
            completed_at = started_at + timedelta(minutes=RND.randint(8, 25))
            target = RND.uniform(0.15, 0.95)
            if RND.random() < 0.18 and icode in ("orp", "iac-adresd"):
                target = 0.85 if icode == "orp" else 0.15
            picks = pick_responses(vid, target)
            total, max_total = compute_score(vid, picks)
            # cap to range max
            max_bound = max(
                (r[1] for r in ranges_by_version.get(vid, [])), default=max_total
            )
            score = round(min(total, max_bound), 2)
            pct = round(score / max_total * 100, 2) if max_total else 0
            qualifier, severity = classify(score, vid)
            with conn.cursor() as cur:
                cur.execute(
                    "UPDATE app.health_test_assignments SET status='completed', completed_at=%s, started_at=%s WHERE id=%s;",
                    (completed_at, started_at, aid),
                )
                eval_id = str(uuid.uuid4())
                cur.execute(
                    "INSERT INTO app.health_test_evaluations (id, assignment_id, patient_id, version_id, status, started_at, completed_at, score, score_percentage) VALUES (%s,%s,%s,%s,'completed',%s,%s,%s,%s) ON CONFLICT DO NOTHING;",
                    (eval_id, aid, pid, vid, started_at, completed_at, score, pct),
                )
                for qid, oid in picks:
                    rid = str(uuid.uuid4())
                    cur.execute(
                        "INSERT INTO app.health_test_responses (id, evaluation_id, question_id, answer_option_id, created_at) VALUES (%s,%s,%s,%s,%s) ON CONFLICT DO NOTHING;",
                        (rid, eval_id, qid, oid, completed_at),
                    )
                # result score
                cur.execute(
                    "INSERT INTO app.health_test_results (id, evaluation_id, result_type, code, label, value, qualifier, severity, created_at) VALUES (%s,%s,'score',%s,'Score total',%s,%s,%s,%s) ON CONFLICT DO NOTHING;",
                    (
                        str(uuid.uuid4()),
                        eval_id,
                        icode,
                        score,
                        qualifier,
                        severity,
                        completed_at,
                    ),
                )
                # subscales
                # need section sums
                sec_sums = {}
                # map qid->section
                q_to_sec = {
                    qid: sec
                    for qid, sec, maxs, dir in questions_by_version.get(vid, [])
                }
                for qid, oid in picks:
                    sec = q_to_sec.get(qid, "")
                    # find score of oid
                    for o_qid, opts in [(k, v) for k, v in options_by_q.items()]:
                        if o_qid == qid:
                            for oid2, sc in opts:
                                if oid2 == oid:
                                    sec_sums[sec] = sec_sums.get(sec, 0) + sc
                                    break
                for sec, sval in sec_sums.items():
                    if not sec:
                        continue
                    squal, ssev = classify(sval, vid)
                    cur.execute(
                        "INSERT INTO app.health_test_results (id, evaluation_id, result_type, code, label, value, qualifier, severity, created_at) VALUES (%s,%s,'subscale',%s,%s,%s,%s,%s,%s) ON CONFLICT DO NOTHING;",
                        (
                            str(uuid.uuid4()),
                            eval_id,
                            f"{icode}.{sec}",
                            sec,
                            round(sval, 2),
                            squal,
                            ssev,
                            completed_at,
                        ),
                    )
                if icode == "iac-adresd":
                    iapnea_val = round(RND.uniform(0, 100), 2)
                    iapnea_sev = "high" if iapnea_val >= 70 else "low"
                    iapnea_qual = "alto" if iapnea_sev == "high" else "bajo"
                    cur.execute(
                        "INSERT INTO app.health_test_results (id, evaluation_id, result_type, code, label, value, qualifier, severity, created_at) VALUES (%s,%s,'indicator','iapnea','Sospecha de apnea',%s,%s,%s,%s) ON CONFLICT DO NOTHING;",
                        (
                            str(uuid.uuid4()),
                            eval_id,
                            iapnea_val,
                            iapnea_qual,
                            iapnea_sev,
                            completed_at,
                        ),
                    )
                    adh_val = round(RND.uniform(0, 100), 2)
                    adh_sev = "high" if adh_val < 40 else "low"
                    adh_qual = "bajo" if adh_sev == "high" else "alto"
                    cur.execute(
                        "INSERT INTO app.health_test_results (id, evaluation_id, result_type, code, label, value, qualifier, severity, created_at) VALUES (%s,%s,'indicator','iadherencia','Índice de adherencia',%s,%s,%s,%s) ON CONFLICT DO NOTHING;",
                        (
                            str(uuid.uuid4()),
                            eval_id,
                            adh_val,
                            adh_qual,
                            adh_sev,
                            completed_at,
                        ),
                    )
            eval_c += 1
        if idx % 100 == 0 and idx > 0:
            conn.commit()
            print(
                f"  {idx}/{len(pending)} -> {eval_c} completadas {started_c} iniciadas"
            )

    conn.commit()
    print(f"Evaluaciones completadas {eval_c} iniciadas {started_c}")

    # alertas
    with conn.cursor() as cur:
        cur.execute("SELECT id, code FROM app.health_test_alert_rules WHERE is_active;")
        rules = {r[1]: str(r[0]) for r in cur.fetchall()}
        # fetch trigger results not yet alerted
        cur.execute("""
            SELECT r.id, r.evaluation_id, r.code, r.qualifier, r.severity, r.value::text, e.patient_id
            FROM app.health_test_results r
            JOIN app.health_test_evaluations e ON e.id=r.evaluation_id
            LEFT JOIN app.health_test_alerts a ON a.result_id=r.id
            WHERE a.id IS NULL AND (
              (r.code='orp' AND r.result_type='score' AND r.severity IN ('high','critical'))
              OR (r.code='iapnea' AND r.result_type='indicator' AND r.severity='high')
              OR (r.code='iadherencia' AND r.result_type='indicator' AND r.severity='high')
            );
        """)
        trig = cur.fetchall()
        code_map = {
            "orp": "orp_riesgo_alto",
            "iapnea": "iapnea_sospecha",
            "iadherencia": "adherencia_baja",
        }
        # map rule code -> severity
        cur.execute(
            "SELECT code, severity, name, message_template FROM app.health_test_alert_rules;"
        )
        rule_info = {r[0]: (r[1], r[2], r[3]) for r in cur.fetchall()}
        alert_cnt = 0
        for rid, eid, code, qual, sev, val, pid in trig:
            rc = code_map.get(code)
            if rc not in rules:
                continue
            rule_id = rules[rc]
            sev_rule, title, tmpl = rule_info[rc]
            body = (tmpl or "Riesgo {label} ({value})").format(
                label=qual or "", value=val
            )
            cur.execute(
                "INSERT INTO app.health_test_alerts (id, patient_id, result_id, rule_id, severity, title, body, status, created_at) VALUES (%s,%s,%s,%s,%s,%s,%s,'active', now() - interval '%s days') ON CONFLICT DO NOTHING;",
                (
                    str(uuid.uuid4()),
                    str(pid),
                    str(rid),
                    rule_id,
                    sev_rule,
                    title,
                    body,
                    RND.randint(0, 10),
                ),
            )
            alert_cnt += 1
        conn.commit()
        print(f"Alertas {alert_cnt}")
        # comentarios
        cur.execute("""
            SELECT e.id, e.patient_id, pp.professional_id, em.user_id
            FROM app.health_test_evaluations e
            JOIN app.patient_professionals pp ON pp.patient_id=e.patient_id
            JOIN erp.professionals p ON p.id=pp.professional_id
            JOIN erp.employees em ON em.id=p.employee_id
            WHERE e.status='completed' ORDER BY random() LIMIT 80;
        """)
        evals = cur.fetchall()
        bodies = [
            "Evolución favorable respecto a evaluación anterior.",
            "Requiere seguimiento en 15 días.",
            "Score estable; mantener plan nutricional.",
            "Derivar a fisioterapia.",
            "Buena adherencia; reforzar higiene de sueño.",
            "Ajustar dosis tras resultado.",
            "Paciente motivado; excelente compromiso.",
            "Riesgo moderado; repetir en 30 días.",
            "Sin hallazgos nuevos; control habitual.",
            "Mejoría subjetiva consistente.",
        ]
        cnt = 0
        for eid, pid, prof_id, user_id in evals:
            cur.execute(
                "SELECT 1 FROM app.health_test_comments WHERE evaluation_id=%s LIMIT 1;",
                (str(eid),),
            )
            if cur.fetchone():
                continue
            body = RND.choice(bodies)
            cur.execute(
                "INSERT INTO app.health_test_comments (id, patient_id, evaluation_id, author_id, body, created_at) VALUES (%s,%s,%s,%s,%s, now() - interval '%s days') ON CONFLICT DO NOTHING;",
                (
                    str(uuid.uuid4()),
                    str(pid),
                    str(eid),
                    str(user_id),
                    body,
                    RND.randint(0, 18),
                ),
            )
            cnt += 1
        conn.commit()
        print(f"Comentarios {cnt}")
        # resumen
        cur.execute(
            "SELECT 'pacientes='||count(*) FROM app.patient_profiles UNION ALL SELECT 'baterias='||count(*) FROM app.health_test_batteries WHERE is_active UNION ALL SELECT 'asignaciones='||count(*) FROM app.health_test_assignments UNION ALL SELECT 'evaluaciones='||count(*) FROM app.health_test_evaluations UNION ALL SELECT 'resultados='||count(*) FROM app.health_test_results UNION ALL SELECT 'alertas='||count(*) FROM app.health_test_alerts;"
        )
        for r in cur.fetchall():
            print(r[0])
    conn.close()
    print("DONE")


if __name__ == "__main__":
    main()
