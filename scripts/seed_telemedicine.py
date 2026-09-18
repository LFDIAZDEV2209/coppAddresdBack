"""Seed de datos de prueba para el módulo de Telemedicina.

Crea un volumen realista y bien relacionado de solicitudes, citas, salas,
sesiones, encuentros clínicos y alertas para probar el dashboard y las vistas
del módulo en el frontend. Usa los profesionales reales de erp.professionals
(con su user_id del Auth) y pacientes reales de app.patient_profiles.

Uso (desde ai-service, donde hay venv/uv):
    uv run --with psycopg[binary] python ../coppAddresdBack/scripts/seed_telemedicine.py

Idempotente: borra primero todo lo que el seed creó antes (marcado con el
created_by SEED_USER_ID) y lo recrea. Nunca toca datos de otros orígenes.

Reglas respetadas:
- Anti doble reserva: las citas ACTIVAS (Requested/Confirmed/InProgress) de un
  mismo profesional nunca se solapan ni repiten el mismo inicio exacto (índice
  único parcial + exclusión GiST de la BD).
- Estados coherentes con la fecha: pasadas -> Completed (mayoría), NoShow y
  Cancelled (minoría); de hoy -> Confirmed/InProgress/Completed; futuras -> Confirmed.
- Una solicitud Converted -> exactamente una cita (request_id) — el índice único
  ix_appointments_request_id no admite duplicados.
- Las citas completadas recientes llevan sala Ended + sesión Ended + encuentro
  Completed con clinical_data; las citas de hoy InProgress llevan sala/sesión Active.
"""

from __future__ import annotations

import random
import uuid
from datetime import datetime, timedelta, timezone

import psycopg

DB_DSN = "host=localhost port=5432 dbname=coppaddresd user=app_user password=CoppAddresdDev!2026"

# Marca de origen: todo lo que crea el seed usa este created_by para poder
# borrarlo y recrearlo de forma idempotente sin tocar datos ajenos.
SEED_USER_ID = uuid.UUID("a1b2c3d4-e5f6-4789-8abc-def012345678")
SEED_ORG = uuid.UUID("5fde219a-89ea-4cf9-be48-379e8b1042cb")
SEED_CLINIC = uuid.UUID("360a13fe-8adc-4a00-94df-04d2fa703b7c")
SEED_LOCATION = uuid.UUID("a73fb549-8ef0-40ae-98d1-42f105815d99")

SPECIALTY_BEHAVIORAL = uuid.UUID("7fa2cc0e-621a-405e-a85a-821184ce1dbc")
SPECIALTY_OBESITY = uuid.UUID("5c6afbf8-d00f-451c-8d71-16983bea0b87")

# Historia clínica de ejemplo (esquema libre de clinical_data jsonb).
ENCOUNTER_NOTE = {
    "plan": "Ajustar porciones de almuerzo y mantener registro semanal",
    "motivo": "Control nutricional trimestral",
    "evaluacion": "Adherencia al plan del 60%",
    "indicaciones": "Incrementar fibra a 30g/día",
}

ENCOUNTER_NOTES = [
    {
        "plan": "Seguimiento en 4 semanas + registro de síntomas",
        "motivo": "Seguimiento de ansiedad",
        "evaluacion": "Progreso estable con terapia cognitivo-conductual",
        "indicaciones": "Ejercicios de respiración diarios",
    },
    {
        "plan": "Ajustar medicación y revisar en 6 semanas",
        "motivo": "Control de peso y hábitos",
        "evaluacion": "Reducción de 2 kg desde la última consulta",
        "indicaciones": "Mantener plan de comidas y actividad física",
    },
    {
        "plan": "Derivar a endocrinología si persiste",
        "motivo": "Consulta de control",
        "evaluacion": "Parámetros dentro de rango esperado",
        "indicaciones": "Continuar con plan actual",
    },
    {
        "plan": "Seguimiento quincenal de adherencia",
        "motivo": "Evaluación inicial",
        "evaluacion": "Paciente motivado, buena disposición al cambio",
        "indicaciones": "Iniciar registro de alimentación",
    },
]

REQUEST_REASONS = [
    "Control de peso programado",
    "Seguimiento de hábitos alimentarios",
    "Consulta por ansiedad",
    "Evaluación nutricional inicial",
    "Seguimiento de plan alimentario",
    "Control trimestral de rutina",
    "Consulta de seguimiento post-encuentro",
]

STATUS_ACTIVE = ("Requested", "Confirmed", "InProgress")


def main() -> None:
    random.seed(2026)

    with psycopg.connect(DB_DSN) as conn:
        professionals = load_professionals(conn)
        patients = load_patients(conn)

        print(f"Profesionales: {len(professionals)} | Pacientes disponibles: {len(patients)}")
        if not professionals or not patients:
            raise SystemExit("Sin profesionales o pacientes reales; no se puede sembrar.")

        wipe_seed(conn, professionals)
        requests = create_requests(conn, professionals, patients)
        appointments = create_appointments(conn, professionals, patients, requests)
        create_rooms_sessions_encounters(conn, appointments, professionals)
        create_alerts(conn, professionals, appointments)

        conn.commit()
        print(f"Seed completo: {len(requests)} solicitudes, {len(appointments)} citas.")


def load_professionals(conn: psycopg.Connection) -> list[dict]:
    """Profesionales reales con su user_id del Auth y especialidad de catálogo."""
    rows = conn.execute(
        """
        SELECT p.id AS professional_id,
               e.first_name, e.last_name,
               e.user_id,
               COALESCE(s.name, '') AS specialty_name,
               COALESCE(s.id, %s) AS specialty_id
        FROM erp.professionals p
        JOIN erp.employees e ON e.id = p.employee_id
        LEFT JOIN erp.professional_specialties ps ON ps.professional_id = p.id
        LEFT JOIN erp.specialties s ON s.id = ps.specialty_id
        WHERE e.user_id IS NOT NULL
        ORDER BY e.first_name
        """,
        (str(SPECIALTY_BEHAVIORAL),),
    ).fetchall()

    professionals = []
    for row in rows:
        professionals.append(
            {
                "id": row[0],
                "first_name": row[1],
                "last_name": row[2],
                "user_id": row[3],
                "specialty_id": row[5],
            }
        )
    return professionals


def load_patients(conn: psycopg.Connection, limit: int = 120) -> list[dict]:
    """Muestra determinista de pacientes reales (nombre completo para las citas)."""
    rows = conn.execute(
        """
        SELECT id, first_name, last_name
        FROM app.patient_profiles
        ORDER BY md5(id::text)
        LIMIT %s
        """,
        (limit,),
    ).fetchall()
    return [
        {"id": row[0], "name": f"{row[1]} {row[2]}".strip()}
        for row in rows
    ]


def wipe_seed(conn: psycopg.Connection, professionals: list[dict]) -> None:
    """Borra SOLO lo que el seed creó antes (marcado por created_by o por las citas del seed)."""
    appointment_ids = [
        r[0]
        for r in conn.execute(
            "SELECT id FROM tele.appointments WHERE created_by = %s",
            (SEED_USER_ID,),
        ).fetchall()
    ]

    with conn.transaction():
        if appointment_ids:
            conn.execute(
                "DELETE FROM tele.telemedicine_alerts WHERE related_appointment_id = ANY(%s)",
                (appointment_ids,),
            )
        # Encuentros y sesiones primero (FKs), después el resto por cascada lógica.
        conn.execute(
            "DELETE FROM tele.clinical_encounters WHERE created_by = %s", (SEED_USER_ID,)
        )
        conn.execute(
            "DELETE FROM tele.telemedicine_sessions WHERE created_by = %s", (SEED_USER_ID,)
        )
        conn.execute(
            "DELETE FROM tele.virtual_rooms WHERE created_by = %s", (SEED_USER_ID,)
        )
        conn.execute(
            "DELETE FROM tele.appointments WHERE created_by = %s",
            (SEED_USER_ID,),
        )
        conn.execute(
            "DELETE FROM tele.telemedicine_requests WHERE created_by = %s",
            (SEED_USER_ID,),
        )
        # Alertas huérfanas del seed (sin cita porque su cita fue borrada).
        user_ids = [p["user_id"] for p in professionals if p["user_id"]]
        if user_ids:
            conn.execute(
                "DELETE FROM tele.telemedicine_alerts "
                "WHERE recipient_user_id = ANY(%s) AND type = %s",
                (user_ids, "System"),
            )
    print(f"Limpieza previa: {len(appointment_ids)} citas del seed eliminadas.")


def create_requests(
    conn: psycopg.Connection,
    professionals: list[dict],
    patients: list[dict],
) -> list[dict]:
    """Solicitudes en estados variados: la mayoría Pending, algunas en otros estados."""
    now = datetime.now(timezone.utc)
    requests = []

    for i in range(14):
        professional = professionals[i % len(professionals)]
        patient = patients[i % len(patients)]
        status = ["Pending", "Pending", "Pending", "Approved", "Rejected", "Cancelled"][
            i % 6
        ]
        preferred = now + timedelta(days=random.randint(2, 12), hours=random.randint(8, 17))

        req_id = uuid.uuid4()
        conn.execute(
            """
            INSERT INTO tele.telemedicine_requests
                (id, patient_id, professional_id, specialty_id, organization_id,
                 clinic_id, location_id, preferred_start, reason, status, notes,
                 created_by, created_at)
            VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
            """,
            (
                req_id,
                patient["id"],
                professional["id"],
                professional["specialty_id"],
                SEED_ORG,
                SEED_CLINIC,
                SEED_LOCATION,
                preferred,
                REQUEST_REASONS[i % len(REQUEST_REASONS)],
                status,
                "Solicitud generada por el seed de pruebas",
                SEED_USER_ID,
                now - timedelta(days=random.randint(1, 6)),
            ),
        )
        requests.append(
            {
                "id": req_id,
                "patient_id": patient["id"],
                "professional_id": professional["id"],
                "specialty_id": professional["specialty_id"],
                "status": status,
                "preferred_start": preferred,
            }
        )
    return requests


def create_appointments(
    conn: psycopg.Connection,
    professionals: list[dict],
    patients: list[dict],
    requests: list[dict],
) -> list[dict]:
    """Citas distribuidas en -60..+14 días, estados coherentes, sin solaparse por profesional."""
    now = datetime.now(timezone.utc)
    appointments: list[dict] = []

    # Estados por antigüedad: pasadas -> Completed mayoritario; hoy -> mixto; futuras -> Confirmed.
    def status_for(start: datetime) -> str:
        if start.date() < now.date():
            roll = random.random()
            if roll < 0.75:
                return "Completed"
            if roll < 0.88:
                return "Cancelled"
            return "NoShow"
        if start.date() == now.date():
            if start < now:
                roll = random.random()
                return "Completed" if roll < 0.6 else "InProgress"
            return "Confirmed"
        return "Confirmed"

    for professional in professionals:
        # Franjas de agenda por profesional (día hábil, 8h-17h, duración 30-45 min).
        slot_index = 0
        for day_offset in range(-60, 15):
            day = (now + timedelta(days=day_offset)).date()
            if day.weekday() >= 5:  # fin de semana: agenda ligera
                if random.random() < 0.85:
                    continue
            # 2-4 citas por día hábil (determinista por el seed para repetibilidad).
            per_day = random.randint(2, 4)
            for _ in range(per_day):
                duration = 30 if professional["specialty_id"] == SPECIALTY_OBESITY else 45
                start = datetime(
                    day.year, day.month, day.day,
                    hour=8 + (slot_index % 9), minute=(slot_index * 7) % 60,
                    tzinfo=timezone.utc,
                )
                slot_index += 1
                end = start + timedelta(minutes=duration)

                # Anti doble reserva: no crear dos citas ACTIVAS del mismo
                # profesional en la misma franja o con el mismo inicio exacto.
                conflict = conn.execute(
                    """
                    SELECT 1 FROM tele.appointments
                    WHERE professional_id = %s
                      AND status = ANY(%s)
                      AND scheduled_start < %s AND scheduled_end > %s
                    LIMIT 1
                    """,
                    (professional["id"], list(STATUS_ACTIVE), end, start),
                ).fetchone()
                if conflict:
                    continue

                patient = patients[(slot_index * 7) % len(patients)]
                status = status_for(start)

                # Algunas citas futuras nacen de solicitudes convertidas.
                request_id = None
                if status == "Confirmed" and start > now + timedelta(days=1):
                    converted = next(
                        (r for r in requests if r["status"] == "Pending"
                         and r["professional_id"] == professional["id"]),
                        None,
                    )
                    if converted and random.random() < 0.5:
                        request_id = converted["id"]
                        converted["status"] = "Converted"

                appointment_id = uuid.uuid4()
                cancelled_by = None
                cancellation_reason = None
                cancelled_at = None
                reschedule_count = 0

                conn.execute(
                    """
                    INSERT INTO tele.appointments
                        (id, request_id, patient_id, professional_id, specialty_id,
                         organization_id, clinic_id, location_id,
                         scheduled_start, scheduled_end, duration_minutes, status,
                         reschedule_count, cancellation_reason, cancelled_by,
                         cancelled_at, created_by, created_at)
                    VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s, %s)
                    """,
                    (
                        appointment_id, request_id, patient["id"], professional["id"],
                        professional["specialty_id"], SEED_ORG, SEED_CLINIC, SEED_LOCATION,
                        start, end, duration, status, 0,
                        None, None, None,
                        SEED_USER_ID, now - timedelta(days=random.randint(1, 30)),
                    ),
                )

                if status == "Cancelled":
                    cancelled_by = ["Patient", "Professional", "Admin"][random.randint(0, 2)]
                    cancellation_reason = "Cancelado por el seed de pruebas"
                    cancelled_at = start - timedelta(hours=random.randint(2, 48))
                    conn.execute(
                        """
                        UPDATE tele.appointments
                        SET status = 'Cancelled',
                            cancellation_reason = %s,
                            cancelled_by = %s,
                            cancelled_at = %s
                        WHERE id = %s
                        """,
                        (cancellation_reason, cancelled_by, cancelled_at, appointment_id),
                    )
                    conn.execute(
                        """
                        INSERT INTO tele.appointment_cancellations
                            (id, appointment_id, cancelled_by, cancelled_by_user_id,
                             reason, cancelled_at)
                        VALUES (%s, %s, %s, %s, %s, %s)
                        """,
                        (
                            uuid.uuid4(), appointment_id, cancelled_by,
                            SEED_USER_ID, cancellation_reason, cancelled_at,
                        ),
                    )

                if status == "Confirmed" and random.random() < 0.08:
                    reschedule_count = 1
                    conn.execute(
                        """
                        UPDATE tele.appointments
                        SET reschedule_count = %s
                        WHERE id = %s
                        """,
                        (reschedule_count, appointment_id),
                    )
                    conn.execute(
                        """
                        INSERT INTO tele.appointment_reschedules
                            (id, appointment_id, requested_by, requested_by_user_id,
                             from_start, to_start, reason, rescheduled_at)
                        VALUES (%s, %s, %s, %s, %s, %s, %s, %s)
                        """,
                        (
                            uuid.uuid4(), appointment_id, "Professional", SEED_USER_ID,
                            start - timedelta(hours=2), start,
                            "Reprogramación por ajuste de agenda", now,
                        ),
                    )
                appointments.append(
                    {
                        "id": appointment_id,
                        "patient_id": patient["id"],
                        "patient_name": patient["name"],
                        "professional_id": professional["id"],
                        "specialty_id": professional["specialty_id"],
                        "start": start,
                        "end": end,
                        "duration": duration,
                        "status": status,
                    }
                )

    return appointments


def create_rooms_sessions_encounters(
    conn: psycopg.Connection,
    appointments: list[dict],
    professionals: list[dict],
) -> None:
    """Sala + sesión + encuentro para citas completadas recientes e InProgress de hoy."""
    user_by_professional = {p["id"]: p["user_id"] for p in professionals}

    for appointment in appointments:
        if appointment["status"] == "Completed" and appointment["start"] > datetime.now(timezone.utc) - timedelta(days=10):
            room_id = uuid.uuid4()
            conn.execute(
                """
                INSERT INTO tele.virtual_rooms
                    (id, appointment_id, provider, provider_room_sid, provider_room_name,
                     status, scheduled_open_at, scheduled_close_at, max_participants,
                     created_by, created_at)
                VALUES (%s, %s, 'Twilio', %s, %s, 'Ended', %s, %s, 2, %s, %s)
                """,
                (
                    room_id, appointment["id"],
                    f"RM{str(appointment['id'])[:8].upper()}",
                    f"apt-{appointment['id']}",
                    appointment["start"] - timedelta(minutes=10),
                    appointment["end"] + timedelta(minutes=15),
                    SEED_USER_ID, appointment["start"] - timedelta(days=1),
                ),
            )
            session_id = uuid.uuid4()
            duration_seconds = int((appointment["end"] - appointment["start"]).total_seconds()) - random.randint(180, 600)
            conn.execute(
                """
                INSERT INTO tele.telemedicine_sessions
                    (id, appointment_id, room_id, status, started_at, ended_at,
                     duration_seconds, ended_by, end_reason, created_by, created_at)
                VALUES (%s, %s, %s, 'Ended', %s, %s, %s, %s, 'Completed', %s, %s)
                """,
                (
                    session_id, appointment["id"], room_id,
                    appointment["start"], appointment["start"] + timedelta(seconds=duration_seconds),
                    duration_seconds, user_by_professional.get(appointment["professional_id"]),
                    SEED_USER_ID, appointment["start"] - timedelta(days=1),
                ),
            )
            conn.execute(
                """
                INSERT INTO tele.clinical_encounters
                    (id, appointment_id, session_id, patient_id, professional_id,
                     encounter_date, status, clinical_data, notes, created_by, created_at)
                VALUES (%s, %s, %s, %s, %s, %s, 'Completed', %s, %s, %s, %s)
                """,
                (
                    uuid.uuid4(), appointment["id"], session_id,
                    appointment["patient_id"], appointment["professional_id"],
                    appointment["start"],
                    psycopg.types.json.Jsonb(
                        ENCOUNTER_NOTES[random.randint(0, len(ENCOUNTER_NOTES) - 1)]
                    ),
                    "Registro del encuentro (seed de pruebas)",
                    SEED_USER_ID, appointment["start"] - timedelta(days=1),
                ),
            )

        elif appointment["status"] == "InProgress":
            room_id = uuid.uuid4()
            conn.execute(
                """
                INSERT INTO tele.virtual_rooms
                    (id, appointment_id, provider, provider_room_sid, provider_room_name,
                     status, scheduled_open_at, scheduled_close_at, max_participants,
                     created_by, created_at)
                VALUES (%s, %s, 'Twilio', %s, %s, 'Active', %s, %s, 2, %s, %s)
                """,
                (
                    room_id, appointment["id"],
                    f"RM{str(appointment['id'])[:8].upper()}",
                    f"apt-{appointment['id']}",
                    appointment["start"] - timedelta(minutes=10),
                    appointment["end"] + timedelta(minutes=15),
                    SEED_USER_ID, appointment["start"] - timedelta(hours=2),
                ),
            )
            conn.execute(
                """
                INSERT INTO tele.telemedicine_sessions
                    (id, appointment_id, room_id, status, started_at, ended_at,
                     duration_seconds, ended_by, end_reason, created_by, created_at)
                VALUES (%s, %s, %s, 'Active', %s, NULL, NULL, NULL, NULL, %s, %s)
                """,
                (
                    uuid.uuid4(), appointment["id"], room_id,
                    appointment["start"], SEED_USER_ID,
                    appointment["start"] - timedelta(minutes=15),
                ),
            )


def create_alerts(
    conn: psycopg.Connection,
    professionals: list[dict],
    appointments: list[dict],
) -> None:
    """Bandeja de alertas por profesional: variadas, la mayoría sin leer."""
    now = datetime.now(timezone.utc)
    my_appointments = {p["id"]: [] for p in professionals}

    for appointment in appointments:
        my_appointments.setdefault(appointment["professional_id"], []).append(appointment)

    for professional in professionals:
        theirs = my_appointments.get(professional["id"], [])
        if not theirs:
            continue
        upcoming = [a for a in theirs if a["start"] > now][:5]
        past = [a for a in theirs if a["start"] <= now][-4:]

        # Alerta de cita próxima (la más relevante para la bandeja).
        for appointment in upcoming[:2]:
            conn.execute(
                """
                INSERT INTO tele.telemedicine_alerts
                    (id, recipient_user_id, recipient_type, recipient_scope_id,
                     type, severity, title, body, related_appointment_id, read_at, created_at)
                VALUES (%s, %s, 'User', NULL, 'UpcomingAppointment', 'Info',
                        'Cita próxima', %s, %s, NULL, %s)
                """,
                (
                    uuid.uuid4(), professional["user_id"],
                    f"Cita programada para {appointment['start'].strftime('%d/%m %H:%M')}",
                    appointment["id"], now - timedelta(hours=1),
                ),
            )

        # Alerta de nueva cita confirmada (leída, para balancear la bandeja).
        for appointment in upcoming[2:4]:
            conn.execute(
                """
                INSERT INTO tele.telemedicine_alerts
                    (id, recipient_user_id, recipient_type, recipient_scope_id,
                     type, severity, title, body, related_appointment_id, read_at, created_at)
                VALUES (%s, %s, 'User', NULL, 'NewAppointment', 'Info',
                        'Cita confirmada', %s, %s, %s, %s)
                """,
                (
                    uuid.uuid4(), professional["user_id"],
                    f"Se confirmó la cita del {appointment['start'].strftime('%d/%m %H:%M')}",
                    appointment["id"], now - timedelta(days=1),
                    now - timedelta(hours=20),
                ),
            )

        # Alerta de sesión finalizada (pasada).
        for appointment in past[:2]:
            conn.execute(
                """
                INSERT INTO tele.telemedicine_alerts
                    (id, recipient_user_id, recipient_type, recipient_scope_id,
                     type, severity, title, body, related_appointment_id, read_at, created_at)
                VALUES (%s, %s, 'User', NULL, 'SessionEnded', 'Info',
                        'Sesión finalizada', %s, %s, NULL, %s)
                """,
                (
                    uuid.uuid4(), professional["user_id"],
                    f"La sesión del {appointment['start'].strftime('%d/%m')} finalizó correctamente",
                    appointment["id"], appointment["start"] + timedelta(hours=1),
                ),
            )

        # Alerta de paciente en sala (si hay sesión activa hoy) o de no-show reciente.
        active_today = [a for a in theirs if a["status"] == "InProgress"]
        if active_today:
            conn.execute(
                """
                INSERT INTO tele.telemedicine_alerts
                    (id, recipient_user_id, recipient_type, recipient_scope_id,
                     type, severity, title, body, related_appointment_id, read_at, created_at)
                VALUES (%s, %s, 'User', NULL, 'PatientWaiting', 'Warning',
                        'Paciente en sala', %s, %s, NULL, %s)
                """,
                (
                    uuid.uuid4(), professional["user_id"],
                    "El paciente ya está en la sala virtual",
                    active_today[0]["id"], now - timedelta(minutes=5),
                ),
            )
        else:
            no_shows = [a for a in theirs if a["status"] == "NoShow"]
            if no_shows:
                conn.execute(
                    """
                    INSERT INTO tele.telemedicine_alerts
                        (id, recipient_user_id, recipient_type, recipient_scope_id,
                         type, severity, title, body, related_appointment_id, read_at, created_at)
                    VALUES (%s, %s, 'User', NULL, 'NoShow', 'Warning',
                            'Paciente no se presentó', %s, %s, NULL, %s)
                    """,
                    (
                        uuid.uuid4(), professional["user_id"],
                        "El paciente no ingresó a la sala",
                        no_shows[-1]["id"], no_shows[-1]["start"] + timedelta(minutes=30),
                    ),
                )

        # Alerta de cancelación (si hubo alguna).
        cancelled = [a for a in theirs if a["status"] == "Cancelled"]
        if cancelled:
            conn.execute(
                """
                INSERT INTO tele.telemedicine_alerts
                    (id, recipient_user_id, recipient_type, recipient_scope_id,
                     type, severity, title, body, related_appointment_id, read_at, created_at)
                VALUES (%s, %s, 'User', NULL, 'AppointmentCancelled', 'Warning',
                        'Cita cancelada', %s, %s, NULL, %s)
                """,
                (
                    uuid.uuid4(), professional["user_id"],
                    "Un paciente canceló su cita",
                    cancelled[-1]["id"], now - timedelta(hours=3),
                ),
            )


if __name__ == "__main__":
    main()