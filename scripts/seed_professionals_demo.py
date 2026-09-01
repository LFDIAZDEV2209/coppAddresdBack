"""Seed de datos de prueba del directorio de profesionales (ERP).

Crea profesionales con distintos tipos, especialidades, roles (scoped por
clínica), estados y clínicas para probar el directorio rediseñado y sus
filtros (estado, especialidad, rol, clínica) y las StatCards.

Uso (desde ai-service, donde hay venv/uv):
    uv run --with psycopg[binary] python ../coppAddresdBack/scripts/seed_professionals_demo.py

Idempotente: borra primero lo que el seed creó antes (usuarios por email,
empleados por email con su extensión y asignaciones) y lo recrea. Nunca toca
datos de otros orígenes. No borra filas de catálogos (tipos/especialidades).

Cobertura del demo:
- Tipos: Physician, Registered Dietitian, Clinical Psychologist, Physical
  Therapist, Health Coach, Registered Nurse.
- Múltiples especialidades (algunos con 2-3).
- Estados: Active, Invited (sin user_id), Inactive.
- Roles scoped por clínica: Professional (clínico) y Finance (no clínico)
  para probar el filtro por rol.
- 2 clínicas (si la demo de telemedicina ya creó la segunda; si no, se crea).
"""

from __future__ import annotations

import uuid
from datetime import datetime, timezone

import psycopg

DB_DSN = "host=localhost port=5432 dbname=coppaddresd user=app_user password=CoppAddresdDev!2026"

SEED_MARK = "seed-professionals-demo"
DEMO_PASSWORD_HASH = None  # los usuarios demo no tienen password (solo enlazados)

# (email, first, last, type_code, specialty_codes, status, role_name)
DEMO_PROFESSIONALS = [
    (
        "ana.torres@coppaddresd.com",
        "Ana",
        "Torres",
        "REGISTERED_DIETITIAN",
        ["CLINICAL_NUTRITION", "SPORTS_NUTRITION"],
        "Active",
        "Professional",
    ),
    (
        "carlos.ruiz@coppaddresd.com",
        "Carlos",
        "Ruiz",
        "CLINICAL_PSYCHOLOGIST",
        ["CLINICAL_PSYCHOLOGY", "BEHAVIORAL_HEALTH"],
        "Active",
        "Professional",
    ),
    (
        "lucia.mendez@coppaddresd.com",
        "Lucía",
        "Méndez",
        "PHYSICIAN",
        ["OBESITY_MEDICINE", "FAMILY_MEDICINE", "PREVENTIVE_MEDICINE"],
        "Active",
        "Professional",
    ),
    (
        "pedro.salas@coppaddresd.com",
        "Pedro",
        "Salas",
        "PHYSICAL_THERAPIST",
        ["PHYSICAL_THERAPY"],
        "Active",
        "Professional",
    ),
    (
        "maria.gomez@coppaddresd.com",
        "María",
        "Gómez",
        "HEALTH_COACH",
        ["WELLNESS_COACHING", "HEALTH_EDUCATION"],
        "Invited",
        "Professional",
    ),
    (
        "jorge.vega@coppaddresd.com",
        "Jorge",
        "Vega",
        "REGISTERED_NURSE",
        ["MEDSURG_NURSING"],
        "Inactive",
        "Professional",
    ),
    (
        "rosa.pineda@coppaddresd.com",
        "Rosa",
        "Pineda",
        "CLINICAL_PSYCHOLOGIST",
        ["BARIATRIC_PSYCHOLOGY"],
        "Active",
        "Professional",
    ),
    (
        "felipe.castro@coppaddresd.com",
        "Felipe",
        "Castro",
        None,
        [],
        "Active",
        "Finance",
    ),
]


def esc(value: str) -> str:
    return value.replace("'", "''")


def ensure_clinic(conn, org_id: uuid.UUID, name: str) -> uuid.UUID:
    """Devuelve la clínica por nombre (creándola si no existe)."""
    with conn.cursor() as cur:
        cur.execute(
            "SELECT id FROM erp.clinics WHERE organization_id = %s AND name = %s",
            (org_id, name),
        )
        row = cur.fetchone()
        if row:
            return row[0]
        clinic_id = uuid.uuid4()
        cur.execute(
            """
            INSERT INTO erp.clinics (id, organization_id, name, is_active, created_at)
            VALUES (%s, %s, %s, true, now())
            """,
            (clinic_id, org_id, name),
        )
        return clinic_id


def ensure_location(conn, clinic_id: uuid.UUID, name: str) -> uuid.UUID:
    with conn.cursor() as cur:
        cur.execute(
            "SELECT id FROM erp.locations WHERE clinic_id = %s AND name = %s",
            (clinic_id, name),
        )
        row = cur.fetchone()
        if row:
            return row[0]
        location_id = uuid.uuid4()
        cur.execute(
            """
            INSERT INTO erp.locations (id, clinic_id, name, is_active, created_at)
            VALUES (%s, %s, %s, true, now())
            """,
            (location_id, clinic_id, name),
        )
        return location_id


def main() -> None:
    with psycopg.connect(DB_DSN) as conn:
        with conn.cursor() as cur:
            org = cur.execute(
                "SELECT id, name FROM erp.organizations ORDER BY created_at LIMIT 1"
            ).fetchone()
            if org is None:
                raise SystemExit(
                    "No hay organizaciones en la BD — siembra el ERP primero."
                )
            org_id, org_name = org

            clinic_principal = ensure_clinic(conn, org_id, "Clínica Principal")
            clinic_secundaria = ensure_clinic(conn, org_id, "Clínica El Poblado")
            ensure_location(conn, clinic_principal, "Sede Centro")
            ensure_location(conn, clinic_secundaria, "Sede Sur")

            # Roles a usar (se siembran en el arranque del Auth Service).
            roles = {}
            for role_name in {"Professional", "Finance"}:
                roles[role_name] = cur.execute(
                    'SELECT "Id" FROM auth."Roles" WHERE "Name" = %s', (role_name,)
                ).fetchone()[0]

            # Aplicación ERP (acceso de usuario).
            erp_app = cur.execute(
                'SELECT "Id" FROM auth."Applications" WHERE "Code" = \'erp\''
            ).fetchone()[0]

        # --- Limpieza idempotente (solo lo que creó el seed) ---
        with conn.cursor() as cur:
            for email, *_ in DEMO_PROFESSIONALS:
                cur.execute(
                    'SELECT "Id" FROM auth."Users" WHERE "Email" = %s', (email,)
                )
                user = cur.fetchone()
                if user:
                    cur.execute(
                        'DELETE FROM auth."ScopedRoleAssignments" WHERE "UserId" = %s',
                        (user[0],),
                    )
                    cur.execute(
                        'DELETE FROM auth."UserApplications" WHERE "UserId" = %s',
                        (user[0],),
                    )
                    cur.execute('DELETE FROM auth."Users" WHERE "Id" = %s', (user[0],))
                cur.execute("SELECT id FROM erp.employees WHERE email = %s", (email,))
                employee = cur.fetchone()
                if employee:
                    cur.execute(
                        "DELETE FROM erp.employees WHERE id = %s", (employee[0],)
                    )

        # --- Creación ---
        for index, (
            email,
            first,
            last,
            type_code,
            specialty_codes,
            status,
            role_name,
        ) in enumerate(DEMO_PROFESSIONALS):
            with conn.cursor() as cur:
                # Usuario de Auth (sin password: solo para probar el filtro por rol
                # y el vínculo; los invitados reales usan el flujo de invitación).
                user_id = uuid.uuid4()
                cur.execute(
                    """
                    INSERT INTO auth."Users"
                        ("Id", "UserName", "Email", "FirstName", "LastName",
                         "IsActive", "EmailConfirmed", "PhoneNumberConfirmed",
                         "TwoFactorEnabled", "AccessFailedCount",
                         "LockoutEnabled", "ConcurrencyStamp", "SecurityStamp",
                         "CreatedAt")
                    VALUES (%s, %s, %s, %s, %s, true, true, false, false, 0,
                            false, %s, %s, now())
                    """,
                    (
                        user_id,
                        email,
                        email,
                        first,
                        last,
                        uuid.uuid4().hex,
                        uuid.uuid4().hex,
                    ),
                )
                cur.execute(
                    'INSERT INTO auth."UserApplications" ("UserId", "ApplicationId", "CreatedAt") VALUES (%s, %s, now())',
                    (user_id, erp_app),
                )

                employee_id = uuid.uuid4()
                cur.execute(
                    """
                    INSERT INTO erp.employees
                        (id, user_id, organization_id, email, first_name, last_name,
                         job_title, status, created_by, created_at)
                    VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, now())
                    """,
                    (
                        employee_id,
                        user_id,
                        org_id,
                        email,
                        first,
                        last,
                        "Profesional demo" if type_code else "Finanzas demo",
                        status,
                        None,
                    ),
                )

                professional_id = None
                if type_code:
                    professional_id = uuid.uuid4()
                    cur.execute(
                        "SELECT id FROM erp.professional_types WHERE code = %s",
                        (type_code,),
                    )
                    type_row = cur.fetchone()
                    if type_row is None:
                        raise SystemExit(
                            f"Tipo de profesional desconocido: {type_code}"
                        )
                    cur.execute(
                        """
                        INSERT INTO erp.professionals
                            (id, employee_id, professional_type_id, bio, created_at)
                        VALUES (%s, %s, %s, %s, now())
                        """,
                        (
                            professional_id,
                            employee_id,
                            type_row[0],
                            f"Perfil demo de {first}.",
                        ),
                    )
                    for spec_code in specialty_codes:
                        spec = cur.execute(
                            "SELECT id FROM erp.specialties WHERE code = %s",
                            (spec_code,),
                        ).fetchone()
                        if spec is None:
                            raise SystemExit(f"Especialidad desconocida: {spec_code}")
                        cur.execute(
                            """
                            INSERT INTO erp.professional_specialties
                                (professional_id, specialty_id, is_primary, created_at)
                            VALUES (%s, %s, false, now())
                            """,
                            (professional_id, spec[0]),
                        )

                clinic_id = clinic_principal if index % 3 != 2 else clinic_secundaria
                cur.execute(
                    """
                    INSERT INTO erp.employee_clinics (employee_id, clinic_id, is_primary, status, created_at)
                    VALUES (%s, %s, true, 'Active', now())
                    """,
                    (employee_id, clinic_id),
                )
                if index % 4 == 0:
                    cur.execute(
                        """
                        INSERT INTO erp.employee_clinics (employee_id, clinic_id, is_primary, status, created_at)
                        VALUES (%s, %s, false, 'Active', now())
                        """,
                        (employee_id, clinic_secundaria),
                    )

                # Rol scoped por clínica (el filtro por rol del directorio lo resuelve).
                cur.execute(
                    """
                    INSERT INTO auth."ScopedRoleAssignments"
                        ("Id", "UserId", "RoleId", "ScopeType", "ScopeId",
                         "GrantedBy", "CreatedAt")
                    VALUES (%s, %s, %s, 'Clinic', %s, NULL, now())
                    """,
                    (uuid.uuid4(), user_id, roles[role_name], clinic_id),
                )

            print(f"  {first} {last} ({type_code or 'staff'}, {role_name}, {status})")

        print("\nSeed de profesionales demo completado.")
        print(f"Organización: {org_name} | Clínicas: Principal + El Poblado")


if __name__ == "__main__":
    main()
