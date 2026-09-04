import json, uuid

try:
    import psycopg
except ImportError:
    raise SystemExit("psycopg missing")
DSN = "postgresql://app_user:CoppAddresdDev!2026@localhost:5432/coppaddresd"
with psycopg.connect(DSN) as conn:
    with conn.cursor() as cur:
        cur.execute(
            "SELECT id, total_weeks FROM app.program_templates WHERE code='default-83w'"
        )
        template_id, total_weeks = cur.fetchone()
        cur.execute(
            "SELECT weekday, task_code, points, sort_order, routine_id, nutrition_plan_id, media_id FROM app.weekly_day_templates WHERE template_id=%s ORDER BY weekday, sort_order",
            (template_id,),
        )
        rows = cur.fetchall()
        snapshot = [
            {
                "weekday": r[0],
                "task_code": r[1],
                "points": r[2],
                "sort_order": r[3],
                "routine_id": str(r[4]) if r[4] else None,
                "nutrition_plan_id": str(r[5]) if r[5] else None,
                "media_id": str(r[6]) if r[6] else None,
            }
            for r in rows
        ]
        snapshot_json = json.dumps(snapshot)
        print(f"Snapshot entries {len(snapshot)}")
        cur.execute(
            "SELECT id, current_week_number FROM app.program_enrollments WHERE status='Active'"
        )
        enrolls = cur.fetchall()
        total = 0
        for eid, cw in enrolls:
            # Set weeks 1..cw to Active with snapshot, rest Locked empty
            cur.execute(
                """
                UPDATE app.program_weeks
                SET status = CASE WHEN week_number <= %s THEN 'Active' ELSE 'Locked' END,
                    tasks_snapshot = CASE WHEN week_number <= %s THEN %s::jsonb ELSE '[]'::jsonb END,
                    activated_at = CASE WHEN week_number <= %s THEN COALESCE(activated_at, now()) ELSE NULL END,
                    template_version_at_start = 1
                WHERE enrollment_id=%s
            """,
                (cw, cw, snapshot_json, cw, eid),
            )
            total += cur.rowcount
        conn.commit()
        print(f"Updated {total} week rows for {len(enrolls)} enrollments")
        # verify
        cur.execute("SELECT count(*) FROM app.program_weeks WHERE status='Active'")
        print("Active weeks now:", cur.fetchone()[0])
        cur.execute("SELECT count(*) FROM app.program_weeks WHERE status='Locked'")
        print("Locked weeks:", cur.fetchone()[0])
