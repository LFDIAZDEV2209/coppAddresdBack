"""Genera scripts/generated/rich_dataset_seed.sql: dataset rico de pruebas
para la BD local de CoppAddresd (módulos Media/Programa, Historia Clínica,
Tests de Salud, Nutrición, Rutinas, Telemedicina y device tokens FCM).

REGLAS DE LA MISIÓN (ver CONTINUCION_CONECTARAPP.md §Base de datos local):
- NO crea pacientes ni profesionales nuevos: enriquece los EXISTENTES,
  especialmente los usuarios demo con password `Demo1234!`
  (misma lista que scripts/seed_unified_credentials.py).
- Los seeds SQL se GENERAN (scripts/generate_*_seed.py), nunca archivos
  estáticos. El SQL resultante es idempotente y re-aplicable:
  - `ON CONFLICT DO NOTHING` cuando existe clave única de negocio
    (device_tokens, patient_allergies, nutrition_intake_logs, habit_checks,
    health_test_batteries, weekly_day_templates).
  - Guardas `NOT EXISTS` sobre la clave de negocio cuando la tabla solo tiene
    PK (encounters, clinical_measurements, citas, evaluaciones, etc.).
- NUNCA borra datos ni toca producción. Nunca toca app.program_weeks
  (las semanas activadas conservan su TasksSnapshot congelado por diseño).
- Reproducible: contenido determinista (sin random); fechas relativas a
  current_date/now() para que las ventanas (3 meses, futuras) se mantengan
  frescas en cada re-ejecución.

La asignación de podcasts a las tareas de plantilla replica EXACTAMENTE la
lógica de rotación por weekday de DevProgramContentSeeder:
    podcastIds[(weekday - 1) % count]
sobre el pool de podcasts Publicados con la clave demo
`media/podcasts/demo-copp.mp3` (orden por sort_order, created_at), y
incrementa program_templates.version de las plantillas afectadas.

StorageKey/ThumbnailKey apuntan a las claves demo que el usuario subirá a S3:
    media/podcasts/demo-copp.mp3
    media/thumbnails/demo-copp.jpg

Uso (desde coppAddresdBack/scripts):
    python generate_rich_dataset_seed.py            # solo genera el SQL
    python generate_rich_dataset_seed.py --apply    # genera + aplica + verifica
    python generate_rich_dataset_seed.py --apply --scope all-linked
        # enriquece TODOS los pacientes con usuario vinculado (no solo demo)

Aplicación equivalente manual:
    docker exec -i coppAddresd psql -U app_user -d coppaddresd -v ON_ERROR_STOP=1 \
        -f - < scripts/generated/rich_dataset_seed.sql

Solo para desarrollo local (BD docker `coppaddresd`). NUNCA contra producción.
"""

from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path

try:
    sys.stdout.reconfigure(encoding="utf-8")
except AttributeError:
    pass

SCRIPTS_DIR = Path(__file__).resolve().parent
OUT = SCRIPTS_DIR / "generated" / "rich_dataset_seed.sql"
REPORT = SCRIPTS_DIR / "generated" / "RICH_DATASET_REPORT.md"

# ---------------------------------------------------------------------------
# Constantes de la BD demo (verificadas contra la BD local docker)
# ---------------------------------------------------------------------------

DB = "coppaddresd"
CONTAINER = "coppAddresd"
TZ = "America/Bogota"

# Marca de origen de TODAS las filas creadas por este dataset (created_by /
# assigned_by): UUID exclusivo de rich_dataset, distinto al de
# scripts/seed_telemedicine.py, para que los topes y los conteos de
# verificación no confundan filas de ambos seeds.
SEED_USER = "7e1c4d20-9a41-4f0e-9b5a-5f3a2c1d0e9f"

# Contexto organizacional usado por las citas (los mismos que el seed de
# telemedicina ya usa en esta BD).
ORG_ID = "5fde219a-89ea-4cf9-be48-379e8b1042cb"  # MediQuer Health
CLINIC_ID = "360a13fe-8adc-4a00-94df-04d2fa703b7c"  # Clínica demo
SPEC_OBESITY = "5c6afbf8-d00f-451c-8d71-16983bea0b87"  # Obesity Medicine
SPEC_INTERNAL = "97f1799b-250f-4297-a4d6-2fc882acb325"  # Internal Medicine

STORAGE_KEY = "media/podcasts/demo-copp.mp3"
THUMB_KEY = "media/thumbnails/demo-copp.jpg"

SOURCE_CM = "rich-seed"  # app.clinical_measurements.source
SOURCE_INTAKE = "rich_seed"  # app.nutrition_intake_logs.source

# Usuarios demo con password `Demo1234!` (seed_unified_credentials.py).
DEMO_EMAILS = [
    "luis.prueba@coppaddresd.com",
    "playwright.e2e@coppaddresd.com",
    "test.e2e@coppaddresd.com",
    "juan.perez@coppaddresd.com",
    "maria.gomez@coppaddresd.com",
    "carlos.rodriguez@coppaddresd.com",
    "ana.martinez@coppaddresd.com",
    "luis.fernandez@coppaddresd.com",
    "laura.sanchez@coppaddresd.com",
    "paciente.prueba@mediquer.com",
    "paciente.test.t23@coppaddresd.com",
    *[f"paciente.{i}@mediquer.com" for i in range(1, 25)],
]

# Usuarios prioritarios para tokens FCM demo (8 por plataforma).
FCM_PRIORITY_EMAILS = [
    "luis.prueba@coppaddresd.com",
    "playwright.e2e@coppaddresd.com",
    "test.e2e@coppaddresd.com",
    "juan.perez@coppaddresd.com",
    "maria.gomez@coppaddresd.com",
    "carlos.rodriguez@coppaddresd.com",
    "ana.martinez@coppaddresd.com",
    "paciente.1@mediquer.com",
]

AUTHORS = [
    "Dr. Alejandro Gómez",
    "Dra. Sofía Morales",
    "Dr. Carlos Valencia",
    "Dra. Elena Ruiz",
    "Lic. Mateo Ríos",
    "Dra. Camila Restrepo",
    "Dr. Julián Ossa",
    "Lic. Valeria Duque",
    "Dr. Andrés Villegas",
    "Dra. Paula Ocampo",
]

# ---------------------------------------------------------------------------
# Contenido de media: 40 ítems (30 podcasts + 6 videos + 4 audios) repartidos
# en las 9 categorías de MediaCategory. Duraciones 8-25 min.
# (kind, category, title, description, minutes)
# ---------------------------------------------------------------------------

MEDIA_ITEMS = [
    (
        "Podcast",
        "Nutricion",
        "Ep. 8: Índice glucémico y carga glucémica",
        "Cómo elegir carbohidratos que estabilizan tu glucosa y te mantienen satisfecho por más horas.",
        11,
    ),
    (
        "Podcast",
        "Nutricion",
        "Ep. 9: Proteína, saciedad y masa muscular",
        "Cuánta proteína necesitas por comida y cómo protege tu músculo mientras bajas de peso.",
        13,
    ),
    (
        "Podcast",
        "Nutricion",
        "Ep. 10: Ultraprocesados: lee la etiqueta como experto",
        "Las señales de alerta del empaque y reglas simples para limpiar tu despensa esta semana.",
        9,
    ),
    (
        "Podcast",
        "Nutricion",
        "Ep. 11: Fibra: el nutriente que olvidamos",
        "Por qué la fibra regula glucosa, colesterol y apetito, y cómo llegar a 30 g al día sin sufrir.",
        12,
    ),
    (
        "Podcast",
        "Nutricion",
        "Ep. 12: Grasas que curan, grasas que dañan",
        "Del aguacate al omega-3: qué grasas priorizar y cuáles reducir para proteger tu corazón.",
        10,
    ),
    (
        "Podcast",
        "SaludFisica",
        "Ep. 13: Cardio zona 2: la base de tu resistencia",
        "Entrena en la intensidad correcta para mejorar tu metabolismo sin agotarte.",
        14,
    ),
    (
        "Podcast",
        "SaludFisica",
        "Ep. 14: Fuerza para principiantes: empieza bien",
        "Los cinco patrones básicos de movimiento y cómo progresar sin lesionarte.",
        12,
    ),
    (
        "Podcast",
        "SaludFisica",
        "Ep. 15: Movilidad de cadera y hombro",
        "Rutinas de 10 minutos que desatan la rigidez del escritorio y mejoran tu técnica.",
        10,
    ),
    (
        "Podcast",
        "SaludFisica",
        "Ep. 16: Recuperación: donde ocurre el progreso",
        "Sueño, descanso activo y señales de sobreentrenamiento que debes respetar.",
        9,
    ),
    (
        "Podcast",
        "SaludFisica",
        "Ep. 17: NEAT: los pasos que cambian tu metabolismo",
        "Cómo moverte más fuera del gimnasio y convertir la oficina en tu aliada.",
        11,
    ),
    (
        "Podcast",
        "Habitos",
        "Ep. 18: La anatomía de un hábito que dura",
        "Clave, rutina y recompensa: diseña tu sistema para no depender de la fuerza de voluntad.",
        12,
    ),
    (
        "Podcast",
        "Habitos",
        "Ep. 19: Rituales de mañana en 20 minutos",
        "Una secuencia simple de agua, luz, movimiento y desayuno para arrancar con energía.",
        9,
    ),
    (
        "Podcast",
        "Habitos",
        "Ep. 20: Tu entorno te adelgaza (o engorda)",
        "Rediseña cocina, escritorio y celular para que la opción sana sea la opción fácil.",
        10,
    ),
    (
        "Podcast",
        "Habitos",
        "Ep. 21: Recaídas: volver sin culpa",
        "El protocolo de 48 horas para retomar el plan después de un fin de semana imperfecto.",
        13,
    ),
    (
        "Podcast",
        "Habitos",
        "Ep. 22: El costo de la decisión diaria",
        "Automatiza comidas y entrenamientos para ahorrar voluntad y avanzar en piloto automático.",
        11,
    ),
    (
        "Podcast",
        "BienestarEmocional",
        "Ep. 23: Comida emocional: nombrar el hambre",
        "Aprende a distinguir hambre física de hambre emocional y qué hacer en el momento crítico.",
        12,
    ),
    (
        "Podcast",
        "BienestarEmocional",
        "Ep. 24: Autocompasión: el motor invisible",
        "Dejarte de castigar no es rendirse: la ciencia de tratarte como tratarías a un amigo.",
        11,
    ),
    (
        "Podcast",
        "BienestarEmocional",
        "Ep. 25: Límites sanos y energía",
        "Cómo decir no sin culpa para proteger tu tiempo de sueño, comida y entrenamiento.",
        10,
    ),
    (
        "Podcast",
        "BienestarEmocional",
        "Ep. 26: Emociones: nombrar para regular",
        "El vocabulario emocional reduce el impulso: prácticas de 2 minutos antes de comer o reaccionar.",
        9,
    ),
    (
        "Podcast",
        "Motivacion",
        "Ep. 27: Motivación vs. disciplina: qué usar cuándo",
        "La motivación enciende, el sistema sostiene: cómo combinarlas para no depender del ánimo.",
        10,
    ),
    (
        "Podcast",
        "Motivacion",
        "Ep. 28: Tu porqué profundo",
        "Excava más allá de la báscula: el propósito que te mantiene cuando el entusiasmo baja.",
        12,
    ),
    (
        "Podcast",
        "Motivacion",
        "Ep. 29: Progreso invisible: confía en el proceso",
        "Las mejoras metabólicas que la báscula no muestra y cómo medirlas mes a mes.",
        9,
    ),
    (
        "Podcast",
        "Psicologia",
        "Ep. 30: La trampa del todo o nada",
        "Rompe la mentalidad de dieta perfecta: un desliz no es un fracaso, es un dato.",
        11,
    ),
    (
        "Podcast",
        "Psicologia",
        "Ep. 31: Estrés crónico y metabolismo",
        "Cortisol, antojos y grasa abdominal: el circuito y tres interruptores para apagarlo.",
        13,
    ),
    (
        "Podcast",
        "Psicologia",
        "Ep. 32: Sueño y mente: la dupla olvidada",
        "Cómo la privación de sueño amplifica el hambre y el mal humor, y cómo blindarte.",
        10,
    ),
    (
        "Podcast",
        "CrecimientoPersonal",
        "Ep. 33: La identidad: ser la persona sana",
        "El cambio duradero no empieza en la dieta, empieza en cómo te defines.",
        12,
    ),
    (
        "Podcast",
        "CrecimientoPersonal",
        "Ep. 34: Tu yo del futuro te escribe",
        "Un ejercicio guiado de visualización para alinear las decisiones de hoy con tu meta de 6 meses.",
        10,
    ),
    (
        "Podcast",
        "Mindfulness",
        "Ep. 35: Comer consciente en 5 pasos",
        "Del primer bocado al plato vacío: practica presencia para comer menos y disfrutar más.",
        9,
    ),
    (
        "Podcast",
        "Mindfulness",
        "Ep. 36: Mindfulness para antojos",
        "Surfear el antojo: observarlo, respirarlo y dejarlo pasar sin ceder en 10 minutos.",
        8,
    ),
    (
        "Podcast",
        "Biologia",
        "Ep. 37: Insulina: el interruptor metabólico",
        "Cómo funciona la insulina y por qué entenderla cambia tus decisiones de cada comida.",
        15,
    ),
    (
        "Video",
        "SaludFisica",
        "Video: Rutina de fuerza en casa — 20 minutos",
        "Sesión guiada sin equipo: sentadillas, empujes, bisagra de cadera y core para nivel inicial.",
        20,
    ),
    (
        "Video",
        "SaludFisica",
        "Video: Movilidad matutina — 10 minutos",
        "Secuencia suave de cuello a tobillos para despertar articulaciones antes del día.",
        10,
    ),
    (
        "Video",
        "Nutricion",
        "Video: Batch cooking dominical en 60 minutos",
        "Cocina la base de la semana: proteínas, verduras y carbohidratos con lista de compras.",
        18,
    ),
    (
        "Video",
        "Nutricion",
        "Video: 5 desayunos altos en proteína",
        "Preparaciones de 10 minutos para empezar el día con energía estable.",
        14,
    ),
    (
        "Video",
        "Habitos",
        "Video: Organiza tu cocina a tu favor",
        "Un recorrido práctico para colocar lo sano a la vista y lo ultraprocesado fuera de alcance.",
        8,
    ),
    (
        "Video",
        "Mindfulness",
        "Video: Respiración 4-7-8 guiada",
        "Práctica de 6 minutos para bajar el estrés antes de comer o dormir.",
        8,
    ),
    (
        "Audio",
        "Mindfulness",
        "Audio guiado: Escaneo corporal de 12 minutos",
        "Práctica de atención plena cuerpo a cuerpo para reducir tensión y comer con claridad.",
        12,
    ),
    (
        "Audio",
        "BienestarEmocional",
        "Audio guiado: Respiración para el estrés",
        "Ejercicio de 8 minutos para calmar el sistema nervioso en momentos de ansiedad.",
        8,
    ),
    (
        "Audio",
        "Motivacion",
        "Audio: Pep talk para días grises",
        "Un empujón de 8 minutos para retomar el plan cuando todo pide rendir.",
        8,
    ),
    (
        "Audio",
        "Habitos",
        "Audio: Reencuadre para antojos nocturnos",
        "Guía de 8 minutos para identificar la emoción detrás del antojo de la noche.",
        8,
    ),
]

CHAPTER_LABELS = {
    "Nutricion": [
        "Intro y contexto",
        "El error más común",
        "Lo que dice la ciencia",
        "Protocolo de la semana",
        "Cierre y reto",
    ],
    "SaludFisica": [
        "Calentamiento mental",
        "Técnica correcta",
        "Progresión práctica",
        "Prevención de lesiones",
        "Tu reto de 7 días",
    ],
    "Habitos": [
        "Por qué fallamos",
        "El sistema simple",
        "Diseño de entorno",
        "Plan antifallo",
        "Resumen accionable",
    ],
    "BienestarEmocional": [
        "Lo que sientes es válido",
        "El mecanismo interno",
        "Herramienta de 2 minutos",
        "Práctica guiada",
        "Cierre compasivo",
    ],
    "Motivacion": ["El momento crítico", "Reencuadre", "Tu porqué", "Próximo paso"],
    "Psicologia": [
        "Qué está pasando",
        "La conexión mente-cuerpo",
        "Evidencia",
        "Estrategia práctica",
        "Cierre",
    ],
    "CrecimientoPersonal": [
        "La pregunta inicial",
        "Identidad vs. resultados",
        "Ejercicio guiado",
        "Compromiso",
    ],
    "Mindfulness": [
        "Prepara el espacio",
        "Práctica principal",
        "Integración diaria",
        "Cierre",
    ],
    "Biologia": [
        "El interruptor metabólico",
        "Insulina en acción",
        "Datos y matices",
        "Aplicación práctica",
        "Cierre y reto",
    ],
}

TAKEAWAYS = {
    "Nutricion": [
        "Prioriza proteína y fibra en cada plato principal.",
        "Los carbohidratos integrales estabilizan tu glucosa por horas.",
        "Lee etiquetas: más de 5 ingredientes desconocidos, devuélvelo a la repisa.",
    ],
    "SaludFisica": [
        "Dos sesiones de fuerza por semana ya cambian tu metabolismo.",
        "Caminar más fuera del entrenamiento pesa más que el entrenamiento.",
        "Progresa carga o repeticiones cada 2 semanas; nunca a costa de dolor articular.",
    ],
    "Habitos": [
        "Diseña el entorno: la opción sana debe ser la más fácil.",
        "No dependas de la voluntad: automatiza comidas y horarios.",
        "Tras una recaída, el protocolo es volver en 48 horas sin culpa.",
    ],
    "BienestarEmocional": [
        "Antes de comer por estrés, nombra la emoción y respira 4-7-8.",
        "Háblate como le hablarías a un amigo que está empezando.",
        "Proteger tus límites protege tu plan.",
    ],
    "Motivacion": [
        "La motivación te enciende; el sistema te sostiene.",
        "Escribe tu porqué profundo y tenlo visible.",
        "Celebra victorias pequeñas: son datos de que el proceso funciona.",
    ],
    "Psicologia": [
        "Un desliz no borra el progreso: es información, no fracaso.",
        "El estrés crónico eleva cortisol, antojos y grasa abdominal.",
        "Dormir mal amplifica el hambre y el malhumor del día siguiente.",
    ],
    "CrecimientoPersonal": [
        "No persigas resultados: conviértete en la clase de persona que los logra.",
        "Visualiza a tu yo de 6 meses antes de decidir en la mesa.",
        "Cada elección es un voto por tu identidad sana.",
    ],
    "Mindfulness": [
        "Come sentado, sin pantallas y con el primer bocado consciente.",
        "Los antojos son oleadas: obsérvalas 10 minutos y pierden fuerza.",
        "Tres respiraciones profundas antes de cada comida regulan el impulso.",
    ],
    "Biologia": [
        "La insulina alta bloquea la quema de grasa: espacia tus comidas.",
        "Combinar fibra + proteína aplana el pico de glucosa.",
        "Mueve el cuerpo después de comer: baja la glucosa sin medicamentos.",
    ],
}

# Instrumentos COMPLETADOS por paciente (batería seguimiento completa +
# historia-clinica de la batería cardiovascular) y el PARCIAL (orp, en curso).
COMPLETED_INSTRS = [
    ("nutricional", 0),
    ("movimiento", 1),
    ("sueno", 2),
    ("iac-adresd", 3),
    ("ers", 4),
    ("historia-clinica", 5),
]
PARTIAL_INSTR = ("orp", 6)

# (code, nombre, descripción) de las baterías de seguimiento que este dataset
# asegura (la bateria-inicial ya viaja con el seed de catálogos).
EXTRA_BATTERIES = [
    (
        "bateria-seguimiento",
        "Seguimiento 30 días",
        "Control mensual: nutrición, movimiento, sueño, adherencia y estrés.",
    ),
    (
        "bateria-cardiovascular",
        "Riesgo cardiovascular",
        "Tamizaje cardiometabólico: historia clínica, ORP y estrés.",
    ),
    (
        "bateria-adherencia",
        "Adherencia y propósito",
        "Motivación y barreras: IAC, Copp Adresd y temperamento.",
    ),
]

BATTERY_INSTRUMENTS = {
    "bateria-seguimiento": ["nutricional", "movimiento", "sueno", "iac-adresd", "ers"],
    "bateria-cardiovascular": ["historia-clinica", "orp", "ers"],
    "bateria-adherencia": ["iac-adresd", "bateria-antares", "temperamento"],
}

DIAGNOSIS_POOL = [
    # (icd10 code, is_primary, selector) — primarios 0/1/2, secundarios 10/11/12
    ("E11.9", True, 0),
    ("I10", True, 1),
    ("E66.9", True, 2),
    ("E78.5", False, 10),
    ("F32.9", False, 11),
    ("N18.3", False, 12),
]

ALLERGEN_POOL = [
    "Penicillin",
    "NSAIDs",
    "Peanuts",
    "Milk",
    "Eggs",
    "Sulfa",
    "Latex",
    "Shellfish",
]

MED_POOL = [
    ("Metformin", 0),  # ILIKE 'Metformin%'
    ("Losartan", 1),
    ("Amlodipine", 2),
    ("Atorvastatin", 3),
    ("Lisinopril", 4),
    ("Omeprazole", 5),
    ("Sertraline", 6),
    ("Levothyroxine", 7),
]

ROUTINE_POOL = [
    ("Cardio Básico", 0),
    ("Pierna", 1),
    ("Cardio HIIT", 2),
    ("Espalda", 3),
    ("Full Body", 4),
    ("Core y Abdominales", 5),
]


# ---------------------------------------------------------------------------
# Utilidades
# ---------------------------------------------------------------------------


def q(s: str) -> str:
    """Escapa comillas simples para SQL."""
    return s.replace("'", "''")


def sql_str(s: str) -> str:
    return f"'{q(s)}'"


def demo_cte(scope: str) -> str:
    """CTE de pacientes enriquecidos (demo por defecto; all-linked = todos con usuario)."""
    if scope == "all-linked":
        return (
            "demo AS (\n"
            "    SELECT pp.id,\n"
            "           pp.document_number AS doc,\n"
            "           pp.clinic_id,\n"
            "           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord\n"
            "    FROM app.patient_profiles pp\n"
            "    WHERE pp.user_id IS NOT NULL\n"
            ")"
        )
    emails = ",\n           ".join(f"'{e.upper()}'" for e in DEMO_EMAILS)
    return (
        "demo AS (\n"
        "    SELECT pp.id,\n"
        "           pp.document_number AS doc,\n"
        "           pp.clinic_id,\n"
        "           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord\n"
        "    FROM app.patient_profiles pp\n"
        '    JOIN auth."Users" u ON u."Id" = pp.user_id\n'
        '    WHERE u."NormalizedEmail" IN (\n'
        f"           {emails}\n"
        "    )\n"
        ")"
    )


def prof_for_cte() -> str:
    """Profesional asignado de cada paciente demo (creado por la sección C)."""
    return (
        "prof_for AS (\n"
        "    SELECT DISTINCT ON (pp.patient_id) pp.patient_id, pp.professional_id\n"
        "    FROM app.patient_professionals pp\n"
        "    WHERE pp.patient_id IN (SELECT id FROM demo)\n"
        "    ORDER BY pp.patient_id, pp.created_at\n"
        ")"
    )


def ts_dyn(day_expr: str, time_expr: str) -> str:
    """Expresión SQL: 'fecha HH:MM:00' (texto) → timestamp → timestamptz Bogotá.
    day_expr: expresión SQL que produce una fecha (p.ej. `current_date - 14`).
    time_expr: expresión SQL de texto 'HH:MM' (p.ej. `'14:' || lpad(...)`)."""
    return f"((({day_expr})::text || ' ' || ({time_expr}) || ':00')::timestamp AT TIME ZONE '{TZ}')"


# ---------------------------------------------------------------------------
# Sección A: media rico
# ---------------------------------------------------------------------------


def build_media() -> list[str]:
    out: list[str] = []
    out.append("")
    out.append(
        "-- ===================================================================="
    )
    out.append(
        "-- A) MEDIA RICO: 40 ítems Publicados (30 podcasts + 6 videos + 4 audios)"
    )
    out.append(
        f"--    StorageKey/ThumbnailKey -> claves demo que el usuario subirá a S3:"
    )
    out.append(f"--    {STORAGE_KEY} / {THUMB_KEY}")
    out.append("--    day = día de programa (1..83) · month = mes de programa (1..3)")
    out.append(
        "--    83 días / 12 semanas: días 1-28 → mes 1, 29-56 → mes 2, 57-83 → mes 3"
    )
    out.append(
        "--    Idempotencia: guarda NOT EXISTS(title) — misma clave de negocio que usa"
    )
    out.append("--    DevProgramSeeder para asegurar medios (id gen_random_uuid).")
    out.append(
        "-- ===================================================================="
    )

    n = len(MEDIA_ITEMS)
    for i, (kind, category, title, desc, minutes) in enumerate(MEDIA_ITEMS):
        duration = minutes * 60
        program_day = int(8 + 75 * i / (n - 1) + 0.5)  # 8..83
        month = 1 if program_day <= 28 else (2 if program_day <= 56 else 3)
        sort_order = 8 + i  # después de los Ep. 1-7 existentes (max sort_order = 7)
        author = AUTHORS[i % len(AUTHORS)]
        content_type = "video/mp4" if kind == "Video" else "audio/mpeg"
        published_days_ago = n - i  # el último se publica ~hoy

        labels = CHAPTER_LABELS[category]
        n_ch = min(4, len(labels))
        fractions = (0.0, 0.28, 0.58, 0.82, 0.92)
        chapters = [
            {
                "atSeconds": min(duration - 5, int(duration * fractions[c] / 5) * 5),
                "label": labels[c],
            }
            for c in range(n_ch)
        ]
        chapters[0]["atSeconds"] = 0
        takeaways = TAKEAWAYS[category]

        out.append("")
        out.append(f"-- {sort_order}. [{kind} · {category}] {title}")
        out.append("INSERT INTO app.media_items")
        out.append(
            "    (title, description, media_type, storage_key, content_type, file_size_bytes,"
        )
        out.append(
            "     duration_secs, status, sort_order, published_at, created_at, updated_at,"
        )
        out.append(
            "     author, category, day, month, thumbnail_key, chapters, takeaways)"
        )
        out.append("SELECT")
        out.append(f"    {sql_str(title)},")
        out.append(f"    {sql_str(desc)},")
        out.append(f"    '{kind}',")
        out.append(f"    '{STORAGE_KEY}',")
        out.append(f"    '{content_type}',")
        out.append("    NULL,")
        out.append(f"    {duration},")
        out.append("    'Published',")
        out.append(f"    {sort_order},")
        out.append(f"    now() - interval '{published_days_ago} days',")
        out.append("    now(),")
        out.append("    now(),")
        out.append(f"    {sql_str(author)},")
        out.append(f"    '{category}',")
        out.append(f"    {program_day},")
        out.append(f"    {month},")
        out.append(f"    '{THUMB_KEY}',")
        out.append(f"    {sql_str(json.dumps(chapters, ensure_ascii=False))}::jsonb,")
        out.append(f"    {sql_str(json.dumps(takeaways, ensure_ascii=False))}::jsonb")
        out.append("WHERE NOT EXISTS (")
        out.append("    SELECT 1 FROM app.media_items x")
        out.append(f"    WHERE x.title = {sql_str(title)} AND x.status = 'Published'")
        out.append(");")

    return out


# ---------------------------------------------------------------------------
# Sección E: mediciones clínicas (3 meses) + vital_signs semanales
# ---------------------------------------------------------------------------

# (code, alias, obs por k, días atrás = base + paso*k, hora, cap, fórmula valor)
# Las fórmulas usan: ws/we (peso inicial/final), g.k (0 = más reciente) y ord.
CM_METRICS = [
    dict(
        code="weight",
        step_days=7,
        base_days=2,
        k_max=12,
        cap=13,
        hour="09:00",
        value="ROUND(we + (ws - we) * g.k / 12.0 + noise, 1)",
    ),
    dict(
        code="waist",
        step_days=7,
        base_days=3,
        k_max=12,
        cap=13,
        hour="09:30",
        value="ROUND((we + 8) + ((ws + 12) - (we + 8)) * g.k / 12.0 + noise * 2, 1)",
    ),
    dict(
        code="heart_rate",
        step_days=7,
        base_days=4,
        k_max=12,
        cap=13,
        hour="08:00",
        value="ROUND(67 + 11 * g.k / 12.0 + noise * 3, 0)",
    ),
    dict(
        code="systolic_bp",
        step_days=14,
        base_days=2,
        k_max=6,
        cap=7,
        hour="08:30",
        value="ROUND(120 + 18 * g.k / 6.0 + noise * 8, 0)",
    ),
    dict(
        code="diastolic_bp",
        step_days=14,
        base_days=2,
        k_max=6,
        cap=7,
        hour="08:35",
        value="ROUND(76 + 12 * g.k / 6.0 + noise * 6, 0)",
    ),
    dict(
        code="hba1c",
        step_days=30,
        base_days=6,
        k_max=2,
        cap=3,
        hour="07:30",
        value="ROUND((a1_end + (a1_start - a1_end) * g.k / 2.0 + noise * 0.4)::numeric, 1)",
    ),
    dict(
        code="glucose_fasting",
        step_days=30,
        base_days=7,
        k_max=2,
        cap=3,
        hour="07:00",
        value="ROUND(96 + 39 * g.k / 2.0 + noise * 10, 0)",
    ),
]


def build_measurements(scope: str) -> list[str]:
    out: list[str] = []
    out.append("")
    out.append(
        "-- ===================================================================="
    )
    out.append(
        "-- E) MEDICIONES CLÍNICAS (app.clinical_measurements, source='rich-seed')"
    )
    out.append("--    Ventana: últimos 3 meses. Varias por semana: peso/cintura/FC")
    out.append("--    semanales; TA sistólica/diastólica quincenal; HbA1c/glucosa")
    out.append(
        "--    mensual. Tendencia de mejora con ruido determinista (sin random)."
    )
    out.append(
        "-- ===================================================================="
    )
    for m in CM_METRICS:
        day_expr = f"current_date - (g.k * {m['step_days']} + {m['base_days']})"
        obs = ts_dyn(day_expr, f"'{m['hour']}'")
        out.append(f"-- Métrica {m['code']} ({m['k_max'] + 1} registros/paciente)")
        out.append("WITH")
        out.append(f"    {demo_cte(scope)},")
        out.append("    m AS (SELECT mm.id, mm.code, mm.default_unit_id")
        out.append(
            f"          FROM app.measurement_metrics mm WHERE mm.code = '{m['code']}')"
        )
        out.append("INSERT INTO app.clinical_measurements")
        out.append(
            "    (id, patient_id, metric_id, unit_id, value, observed_at, recorded_at, source, source_key)"
        )
        out.append("SELECT gen_random_uuid(), d.id, m.id, m.default_unit_id,")
        out.append(f"       {m['value']},")
        out.append(f"       {obs},")
        out.append("       now(),")
        out.append(f"       '{SOURCE_CM}',")
        out.append(
            f"       'rich:' || d.doc || ':' || m.code || ':' || ({day_expr})::text"
        )
        out.append("FROM demo d")
        out.append("JOIN m ON true")
        out.append("CROSS JOIN generate_series(0, %d) AS g(k)" % m["k_max"])
        out.append("CROSS JOIN LATERAL (")
        out.append("    SELECT (82 + (d.ord % 9) * 2.5) AS ws,")
        out.append("           (82 + (d.ord % 9) * 2.5 - (6 + d.ord % 5)) AS we,")
        out.append("           (8.2 - (d.ord % 3) * 0.5) AS a1_start,")
        out.append("           (8.2 - (d.ord % 3) * 0.5 - 1.4) AS a1_end,")
        out.append("           (((d.ord * 13 + g.k * 7) % 100) - 50) / 50.0 AS noise")
        out.append(") w")
        out.append("WHERE NOT EXISTS (")
        out.append("    SELECT 1 FROM app.clinical_measurements x")
        out.append("    WHERE x.patient_id = d.id AND x.metric_id = m.id")
        out.append(f"      AND x.observed_at = {obs}")
        out.append(")")
        out.append("  AND (SELECT count(*) FROM app.clinical_measurements y")
        out.append(f"       WHERE y.patient_id = d.id AND y.source = '{SOURCE_CM}'")
        out.append("         AND y.metric_id = m.id) < %d;" % m["cap"])
        out.append("")
    return out


def build_vital_signs(scope: str) -> list[str]:
    out: list[str] = []
    out.append("")
    out.append(
        "-- ===================================================================="
    )
    out.append(
        "-- E2) SIGNOS VITALES SEMANALES (app.vital_signs): 13 registros/paciente"
    )
    out.append("--     coherentes con la tendencia de clinical_measurements.")
    out.append(
        "-- ===================================================================="
    )
    meas = ts_dyn("current_date - (g.k * 7 + 1)", "'08:00'")
    out.append("WITH")
    out.append(f"    {demo_cte(scope)}")
    out.append("INSERT INTO app.vital_signs")
    out.append("    (id, patient_id, measured_at, systolic, diastolic, heart_rate,")
    out.append("     temperature_c, o2_saturation, height_cm, weight_kg, created_at)")
    out.append("SELECT gen_random_uuid(), d.id,")
    out.append(f"       {meas},")
    out.append("       ROUND(120 + 18 * g.k / 12.0 + noise * 8, 0)::int,")
    out.append("       ROUND(76 + 12 * g.k / 12.0 + noise * 6, 0)::int,")
    out.append("       ROUND(67 + 11 * g.k / 12.0 + noise * 6, 0)::int,")
    out.append("       36.5,")
    out.append("       ROUND(97 - 3 * g.k / 12.0, 0)::int,")
    out.append("       (165 + d.ord % 10)::numeric,")
    out.append("       ROUND(we + (ws - we) * g.k / 12.0 + noise, 1),")
    out.append("       now()")
    out.append("FROM demo d")
    out.append("CROSS JOIN generate_series(0, 12) AS g(k)")
    out.append("CROSS JOIN LATERAL (")
    out.append("    SELECT (82 + (d.ord % 9) * 2.5) AS ws,")
    out.append("           (82 + (d.ord % 9) * 2.5 - (6 + d.ord % 5)) AS we,")
    out.append("           (((d.ord * 13 + g.k * 7) % 100) - 50) / 50.0 AS noise")
    out.append(") w")
    out.append("WHERE NOT EXISTS (")
    out.append("    SELECT 1 FROM app.vital_signs x")
    out.append(f"    WHERE x.patient_id = d.id AND x.measured_at = {meas}")
    out.append(")")
    out.append(
        "  AND (SELECT count(*) FROM app.vital_signs y WHERE y.patient_id = d.id) < 13;"
    )
    return out


# ---------------------------------------------------------------------------
# Sección F: diagnósticos, alergias y medicamentos
# ---------------------------------------------------------------------------


def build_clinical_lists(scope: str) -> list[str]:
    out: list[str] = []
    out.append("")
    out.append(
        "-- ===================================================================="
    )
    out.append("-- F) DIAGNÓSTICOS (ICD-10), ALERGIAS y MEDICAMENTOS por paciente.")
    out.append("--    Solo catálogos existentes (app.icd10_codes/app.allergens/app.")
    out.append("--    medications); si un código no existe en la BD, no se inserta.")
    out.append(
        "-- ===================================================================="
    )

    # F1) Diagnósticos: 1 primario (rotación) + 2 secundarios
    out.append("-- F1) Diagnósticos: 1 primario + 2 secundarios por paciente")
    out.append("WITH")
    out.append(f"    {demo_cte(scope)}")
    out.append("INSERT INTO app.patient_diagnoses")
    out.append("    (id, patient_id, icd10_code_id, is_primary, created_at)")
    out.append("SELECT gen_random_uuid(), d.id, c.id, v.is_primary,")
    out.append("       now() - ((80 - v.sel) || ' days')::interval")
    out.append("FROM demo d")
    out.append("JOIN (VALUES")
    dx_vals = ", ".join(
        f"('{code}', {'true' if prim else 'false'}, {sel})"
        for code, prim, sel in DIAGNOSIS_POOL
    )
    out.append(f"    {dx_vals}")
    out.append(") AS v(code, is_primary, sel) ON true")
    out.append("JOIN app.icd10_codes c ON c.code = v.code")
    out.append("WHERE ((v.sel < 3 AND v.sel = d.ord % 3)")
    out.append("   OR (v.sel >= 10 AND (v.sel - 10) IN")
    out.append("        ((d.ord / 3) % 3, ((d.ord / 3) + 1) % 3)))")
    out.append("  AND NOT EXISTS (")
    out.append("      SELECT 1 FROM app.patient_diagnoses x")
    out.append("      WHERE x.patient_id = d.id AND x.icd10_code_id = c.id")
    out.append("  );")
    out.append("")

    # F2) Alergias: 2 por paciente (clave única real → ON CONFLICT)
    out.append("-- F2) Alergias: 2 por paciente (ON CONFLICT sobre clave única real)")
    out.append("WITH")
    out.append(f"    {demo_cte(scope)}")
    out.append("INSERT INTO app.patient_allergies")
    out.append("    (id, patient_id, allergen_id, notes, created_at)")
    out.append("SELECT gen_random_uuid(), d.id, a.id,")
    out.append("       'Referida por el paciente en la evaluación inicial',")
    out.append("       now() - ((79 - v.sel) || ' days')::interval")
    out.append("FROM demo d")
    out.append("JOIN (VALUES")
    alg_vals = ", ".join(f"('{name}', {i})" for i, name in enumerate(ALLERGEN_POOL))
    out.append(f"    {alg_vals}")
    out.append(") AS v(name, sel) ON true")
    out.append("JOIN app.allergens a ON a.name = v.name")
    out.append("WHERE v.sel = d.ord % 8 OR v.sel = (d.ord + 3) % 8")
    out.append("ON CONFLICT (patient_id, allergen_id) DO NOTHING;")
    out.append("")

    # F3) Medicamentos: 2 por paciente del catálogo (ILIKE por prefijo)
    out.append("-- F3) Medicamentos: 2 por paciente (freq/variedad por rotación)")
    out.append("WITH")
    out.append(f"    {demo_cte(scope)}")
    out.append("INSERT INTO app.patient_medications")
    out.append("    (id, patient_id, medication_id, frequency, sort_order, created_at)")
    out.append("SELECT gen_random_uuid(), d.id, m.id,")
    out.append(
        "       (ARRAY['1 vez al día','Cada 12 horas','Con el desayuno'])[1 + v.sel % 3],"
    )
    out.append("       CASE WHEN v.sel = d.ord % 8 THEN 0 ELSE 1 END,")
    out.append("       now() - ((78 - v.sel) || ' days')::interval")
    out.append("FROM demo d")
    out.append("JOIN (VALUES")
    med_vals = ", ".join(f"('{name}', {i})" for name, i in MED_POOL)
    out.append(f"    {med_vals}")
    out.append(") AS v(name, sel) ON true")
    out.append("JOIN LATERAL (")
    out.append("    SELECT mm.id FROM app.medications mm")
    out.append("    WHERE mm.name ILIKE v.name || '%'")
    out.append("    ORDER BY mm.name LIMIT 1")
    out.append(") m ON true")
    out.append("WHERE (v.sel = d.ord % 8 OR v.sel = (d.ord + 1) % 8)")
    out.append("  AND NOT EXISTS (")
    out.append("      SELECT 1 FROM app.patient_medications x")
    out.append("      WHERE x.patient_id = d.id AND x.medication_id = m.id")
    out.append("  );")
    return out


def build_template_assignment() -> list[str]:
    out: list[str] = []
    out.append("")
    out.append(
        "-- ===================================================================="
    )
    out.append("-- B) ASIGNACIÓN DE PODCASTS A TAREAS DE PLANTILLA")
    out.append("--    MISMA lógica de rotación por weekday de DevProgramContentSeeder:")
    out.append("--        media_id = pool[(weekday - 1) % count]")
    out.append("--    pool = podcasts Publicados con la clave demo (orden sort_order,")
    out.append("--    created_at) — idéntico al ORDER BY del seeder. Solo toca tareas")
    out.append("--    task_code='podcast' de plantillas Active. NUNCA toca")
    out.append("--    app.program_weeks (los snapshots de semanas activadas quedan")
    out.append(
        "--    congelados por diseño; solo las activaciones futuras heredan esto)."
    )
    out.append("--    Si cambió algo, bump de program_templates.version (trazabilidad")
    out.append("--    TemplateVersionAtStart, igual que el seeder).")
    out.append(
        "-- ===================================================================="
    )
    out.append("WITH pool AS (")
    out.append("    SELECT m.id,")
    out.append(
        "           row_number() OVER (ORDER BY m.sort_order, m.created_at) - 1 AS rn"
    )
    out.append("    FROM app.media_items m")
    out.append("    WHERE m.media_type = 'Podcast'")
    out.append("      AND m.status = 'Published'")
    out.append(f"      AND m.storage_key = '{STORAGE_KEY}'")
    out.append("),")
    out.append("upd AS (")
    out.append("    UPDATE app.weekly_day_templates t")
    out.append("    SET media_id = p.id")
    out.append("    FROM pool p")
    out.append("    JOIN app.program_templates pt ON pt.status = 'Active'")
    out.append("    WHERE t.task_code = 'podcast'")
    out.append("      AND t.template_id = pt.id")
    out.append("      AND t.media_id IS DISTINCT FROM p.id")
    out.append("      AND p.rn = (t.weekday - 1) % (SELECT count(*) FROM pool)")
    out.append("    RETURNING t.template_id")
    out.append(")")
    out.append("UPDATE app.program_templates pt")
    out.append("SET version = pt.version + 1,")
    out.append("    updated_at = now()")
    out.append("WHERE pt.id IN (SELECT DISTINCT template_id FROM upd);")
    return out


# ---------------------------------------------------------------------------
# Sección C: vínculo paciente ↔ profesional
# ---------------------------------------------------------------------------


def build_patient_professionals(scope: str) -> list[str]:
    out: list[str] = []
    out.append("")
    out.append(
        "-- ===================================================================="
    )
    out.append("-- C) VÍNCULO PACIENTE ↔ PROFESIONAL (Assigned/Active)")
    out.append("--    Solo crea la asignación si el paciente demo NO tiene ninguna;")
    out.append("--    los vínculos existentes (seed_full_demo_data) NO se tocan.")
    out.append(
        "-- ===================================================================="
    )
    out.append("WITH")
    out.append(f"    {demo_cte(scope)},")
    out.append("    profs AS (SELECT p.id FROM erp.professionals p)")
    out.append("INSERT INTO app.patient_professionals")
    out.append(
        "    (patient_id, professional_id, clinic_id, relationship_type, status, created_by, created_at, updated_at)"
    )
    out.append("SELECT d.id, pr.id, d.clinic_id, 'Assigned', 'Active',")
    # created_by tiene FK a auth."Users": resuelve el marcador a un usuario
    # real existente (determinista; el resto de tablas no exige FK).
    out.append(
        '       (SELECT u."Id" FROM auth."Users" u ORDER BY u."NormalizedEmail" LIMIT 1), now(), now()'
    )
    out.append("FROM demo d")
    out.append("JOIN LATERAL (")
    out.append("    SELECT p.id FROM profs p")
    out.append("    ORDER BY p.id")
    out.append("    OFFSET (d.ord % (SELECT count(*) FROM profs)) LIMIT 1")
    out.append(") pr ON true")
    out.append("WHERE NOT EXISTS (")
    out.append(
        "    SELECT 1 FROM app.patient_professionals x WHERE x.patient_id = d.id"
    )
    out.append(");")
    return out


# ---------------------------------------------------------------------------
# Sección D: encounters canónicos (historia clínica)
# ---------------------------------------------------------------------------


def build_encounters(scope: str) -> list[str]:
    day_off = "current_date - (v.days_ago + (d.ord % 6))"
    time_expr = "'14:' || lpad((((d.ord + v.k) % 4) * 15)::text, 2, '0')"
    ts = ts_dyn(day_off, time_expr)
    out: list[str] = []
    out.append("")
    out.append(
        "-- ===================================================================="
    )
    out.append("-- D) ENCOUNTERS CANÓNICOS (app.encounters): 4 por paciente en los")
    out.append("--    últimos 3 meses (evaluación inicial, seguimiento, telemedicina,")
    out.append("--    control trimestral), con motivo y notas clínicas coherentes.")
    out.append(
        "-- ===================================================================="
    )
    out.append("WITH")
    out.append(f"    {demo_cte(scope)},")
    out.append(f"    {prof_for_cte()},")
    out.append("    enc AS (")
    out.append("        SELECT * FROM (VALUES")
    out.append(
        "            (0, 'consulta_periodica', 'Evaluación inicial del programa Copp Adresd',"
    )
    out.append(
        "             'Evaluación inicial: antecedentes de sobrepeso metabólico, sin alergias conocidas relevantes. Se acuerda plan nutricional de 1400-1800 kcal, batería inicial de tests y meta de 5% de peso en 12 semanas. Paciente motivado, con apoyo familiar.', 84),"
    )
    out.append("            (1, 'seguimiento', 'Seguimiento de adherencia y hábitos',")
    out.append(
        "             'Control de seguimiento: adherencia al plan nutricional en torno al 70%, sueño mejorable (6 h promedio). Se refuerza higiene del sueño, registro de ingesta diario y rutina de fuerza 2 veces por semana. Continuar medicación según prescripción.', 62),"
    )
    out.append(
        "            (2, 'telemedicina', 'Control de peso y glucosa por telemedicina',"
    )
    out.append(
        "             'Consulta remota: reporta mayor energía y -3 kg desde el inicio. Glucosa en ayunas en descenso. Se ajusta porción de cena, se mantiene ejercicio en zona 2 y se agenda control de laboratorio (HbA1c y perfil lipídico).', 38),"
    )
    out.append(
        "            (3, 'consulta_periodica', 'Control trimestral y ajuste del plan',"
    )
    out.append(
        "             'Control trimestral: evolución favorable de peso y perímetro de cintura. Presión arterial en rango objetivo. Se mantiene plan actual, se avanza a rutina intermedia y se re-evalúa batería de seguimiento a 30 días.', 14)"
    )
    out.append("        ) AS v(k, etype, reason, notes, days_ago)")
    out.append("    )")
    out.append("INSERT INTO app.encounters")
    out.append(
        "    (id, patient_id, professional_id, type, status, started_at, ended_at, reason, notes, created_by, created_at, updated_at)"
    )
    out.append(
        "SELECT gen_random_uuid(), d.id, pf.professional_id, v.etype, 'completed',"
    )
    out.append(f"       {ts},")
    out.append(f"       {ts} + interval '45 minutes',")
    out.append("       v.reason, v.notes,")
    out.append(f"       '{SEED_USER}', now(), now()")
    out.append("FROM demo d")
    out.append("JOIN prof_for pf ON pf.patient_id = d.id")
    out.append("JOIN enc v ON true")
    out.append("WHERE (SELECT count(*) FROM app.encounters x")
    out.append(
        f"       WHERE x.patient_id = d.id AND x.created_by = '{SEED_USER}') < 4"
    )
    out.append("  AND NOT EXISTS (")
    out.append("      SELECT 1 FROM app.encounters x")
    out.append("      WHERE x.patient_id = d.id AND x.type = v.etype")
    out.append(f"        AND x.started_at = {ts}")
    out.append("  );")
    return out


# ---------------------------------------------------------------------------
# Sección G: Tests de Salud (baterías + asignaciones + respuestas + resultados)
# ---------------------------------------------------------------------------


def build_health_tests(scope: str) -> list[str]:
    out: list[str] = []
    out.append("")
    out.append(
        "-- ===================================================================="
    )
    out.append("-- G) TESTS DE SALUD por paciente demo:")
    out.append("--    · bateria-seguimiento   -> COMPLETADA (respuestas + resultados)")
    out.append("--    · bateria-cardiovascular-> EN CURSO (historia-clinica completa,")
    out.append("--      orp a medias, ers pendiente)")
    out.append("--    · bateria-adherencia    -> PENDIENTE (solo asignaciones)")
    out.append("--    Respuestas deterministas: opción cuyo score es el más cercano a")
    out.append(
        "--    una fracción objetivo por paciente (sin random). Scores = suma de"
    )
    out.append(
        "--    las opciones elegidas; severity/qualifier desde los rangos reales."
    )
    out.append(
        "-- ===================================================================="
    )

    # G1) Baterías
    out.append("-- G1) Asegura las 3 baterías de seguimiento")
    out.append("INSERT INTO app.health_test_batteries")
    out.append(
        "    (id, code, name, description, auto_assign_on_patient_create, is_active, created_at)"
    )
    out.append("SELECT * FROM (VALUES")
    for code, name, desc in EXTRA_BATTERIES:
        out.append(
            f"    (gen_random_uuid(), '{code}', {sql_str(name)}, {sql_str(desc)}, false, true, now()),"
        )
    out[-1] = out[-1].rstrip(",")
    out.append(
        ") AS t(id, code, name, description, auto_assign_on_patient_create, is_active, created_at)"
    )
    out.append("ON CONFLICT (code) DO NOTHING;")
    out.append("")

    # G2) Ítems de batería
    out.append("-- G2) Ítems de batería (instrumento + versión vigente)")
    out.append("INSERT INTO app.health_test_battery_items")
    out.append(
        "    (id, battery_id, instrument_id, version_id, sort_order, is_required)"
    )
    out.append("SELECT gen_random_uuid(), b.id, i.id, v.id, x.pos, true")
    out.append("FROM (VALUES")
    for bcode, instruments in BATTERY_INSTRUMENTS.items():
        for pos, icode in enumerate(instruments):
            out.append(f"    ('{bcode}', '{icode}', {pos}),")
    out[-1] = out[-1].rstrip(",")
    out.append(") AS x(bcode, icode, pos)")
    out.append("JOIN app.health_test_batteries b ON b.code = x.bcode")
    out.append("JOIN app.health_test_instruments i ON i.code = x.icode")
    out.append(
        "JOIN app.health_test_versions v ON v.instrument_id = i.id AND v.is_current"
    )
    out.append("WHERE NOT EXISTS (SELECT 1 FROM app.health_test_battery_items bi")
    out.append(
        "                  WHERE bi.battery_id = b.id AND bi.instrument_id = i.id);"
    )
    out.append("")

    # G3) Asignaciones de batería por paciente
    out.append(
        "-- G3) Asignaciones de batería (seguimiento completada, cardiovascular en"
    )
    out.append("--     curso, adherencia pendiente) con fechas relativas a hoy")
    ts10 = ts_dyn("current_date - (v.days_ago + (d.ord % 5))", "'10:00'")
    out.append("WITH")
    out.append(f"    {demo_cte(scope)}")
    out.append("INSERT INTO app.health_test_battery_assignments")
    out.append(
        "    (id, patient_id, battery_id, status, assigned_by, assigned_at, due_date, completed_at)"
    )
    out.append("SELECT gen_random_uuid(), d.id, b.id, v.status,")
    out.append(f"       '{SEED_USER}',")
    out.append(f"       {ts10} + ((((d.ord * 3) % 4)::text || ' hours'))::interval,")
    out.append("       ba.assigned_at + interval '14 days',")
    out.append(
        "       CASE WHEN v.status = 'completed' THEN ba.assigned_at + interval '10 days' END"
    )
    out.append("FROM demo d")
    out.append("JOIN (VALUES")
    out.append("    ('bateria-seguimiento', 'completed', 62),")
    out.append("    ('bateria-cardiovascular', 'in_progress', 56),")
    out.append("    ('bateria-adherencia', 'pending', 50)")
    out.append(") AS v(bcode, status, days_ago) ON true")
    out.append("JOIN app.health_test_batteries b ON b.code = v.bcode")
    out.append("JOIN LATERAL (")
    out.append(f"    SELECT {ts10}")
    out.append(
        "           + ((((d.ord * 3) % 4)::text || ' hours'))::interval AS assigned_at"
    )
    out.append(") ba ON true")
    out.append("WHERE NOT EXISTS (SELECT 1 FROM app.health_test_battery_assignments x")
    out.append("                  WHERE x.patient_id = d.id AND x.battery_id = b.id);")
    out.append("")

    # G4) Asignaciones por instrumento
    out.append("-- G4) Asignaciones por instrumento (estado según rol de la batería)")
    out.append("WITH")
    out.append(f"    {demo_cte(scope)}")
    out.append("INSERT INTO app.health_test_assignments")
    out.append(
        "    (id, patient_id, battery_assignment_id, version_id, status, priority, assigned_by,"
    )
    out.append(
        "     assigned_at, due_date, expires_at, started_at, completed_at, notes)"
    )
    out.append("SELECT gen_random_uuid(), ba.patient_id, ba.id, bi.version_id,")
    out.append("       CASE")
    out.append("         WHEN ba.status = 'completed' THEN 'completed'")
    out.append(
        "         WHEN ba.status = 'in_progress' AND i.code = 'orp' THEN 'in_progress'"
    )
    out.append("         ELSE 'pending'")
    out.append("       END,")
    out.append(f"       bi.sort_order, '{SEED_USER}',")
    out.append("       ba.assigned_at, ba.due_date, ba.due_date + interval '16 days',")
    out.append("       CASE WHEN ba.status = 'completed'")
    out.append("               OR (ba.status = 'in_progress' AND i.code = 'orp')")
    out.append("            THEN ba.assigned_at + interval '2 hours' END,")
    out.append("       CASE WHEN ba.status = 'completed'")
    out.append(
        "            THEN ba.assigned_at + interval '2 hours' + interval '11 minutes' END,"
    )
    out.append("       NULL")
    out.append("FROM app.health_test_battery_assignments ba")
    out.append("JOIN app.health_test_batteries b ON b.id = ba.battery_id")
    out.append(
        "    AND b.code IN ('bateria-seguimiento', 'bateria-cardiovascular', 'bateria-adherencia')"
    )
    out.append("JOIN app.health_test_battery_items bi ON bi.battery_id = b.id")
    out.append("JOIN app.health_test_versions v ON v.id = bi.version_id")
    out.append("JOIN app.health_test_instruments i ON i.id = v.instrument_id")
    out.append("WHERE ba.patient_id IN (SELECT id FROM demo)")
    out.append("  AND NOT EXISTS (SELECT 1 FROM app.health_test_assignments x")
    out.append("                  WHERE x.patient_id = ba.patient_id")
    out.append("                    AND x.battery_assignment_id = ba.id")
    out.append("                    AND x.version_id = bi.version_id);")
    out.append("")

    # G5) Evaluaciones completadas + la parcial (orp)
    out.append("-- G5) Evaluaciones: completadas (seguimiento + historia-clinica) y la")
    out.append("--     parcial 'started' para orp")
    out.append("WITH")
    out.append(f"    {demo_cte(scope)}")
    out.append("INSERT INTO app.health_test_evaluations")
    out.append(
        "    (id, assignment_id, patient_id, version_id, status, started_at, completed_at)"
    )
    out.append("SELECT gen_random_uuid(), a.id, a.patient_id, a.version_id,")
    out.append("       CASE WHEN i.code = 'orp' THEN 'started' ELSE 'completed' END,")
    out.append("       a.started_at,")
    out.append("       CASE WHEN i.code = 'orp' THEN NULL ELSE a.completed_at END")
    out.append("FROM app.health_test_assignments a")
    out.append(
        "JOIN app.health_test_battery_assignments ba ON ba.id = a.battery_assignment_id"
    )
    out.append("JOIN app.health_test_batteries b ON b.id = ba.battery_id")
    out.append("JOIN app.health_test_versions v ON v.id = a.version_id")
    out.append("JOIN app.health_test_instruments i ON i.id = v.instrument_id")
    out.append("WHERE a.status IN ('completed', 'in_progress')")
    out.append("  AND ba.patient_id IN (SELECT id FROM demo)")
    out.append("  AND (b.code = 'bateria-seguimiento'")
    out.append(
        "       OR (b.code = 'bateria-cardiovascular' AND i.code IN ('historia-clinica', 'orp')))"
    )
    out.append(
        "  AND NOT EXISTS (SELECT 1 FROM app.health_test_evaluations x WHERE x.assignment_id = a.id);"
    )
    out.append("")

    # G6) Respuestas por instrumento completado
    for icode, off in COMPLETED_INSTRS:
        out.append(
            f"-- G6) Respuestas: {icode} (evaluaciones completadas de este dataset)"
        )
        out.append("WITH")
        out.append(f"    {demo_cte(scope)},")
        out.append("    evs AS (")
        out.append("        SELECT ev.id, ev.patient_id, ev.started_at")
        out.append("        FROM app.health_test_evaluations ev")
        out.append(
            "        JOIN app.health_test_assignments a ON a.id = ev.assignment_id"
        )
        out.append(
            "        JOIN app.health_test_battery_assignments ba ON ba.id = a.battery_assignment_id"
        )
        out.append("        JOIN app.health_test_batteries b ON b.id = ba.battery_id")
        out.append("        JOIN app.health_test_versions v ON v.id = ev.version_id")
        out.append(
            f"        JOIN app.health_test_instruments i ON i.id = v.instrument_id AND i.code = '{icode}'"
        )
        out.append("        WHERE ev.status = 'completed'")
        out.append("          AND ev.patient_id IN (SELECT id FROM demo)")
        out.append(
            "          AND b.code IN ('bateria-seguimiento', 'bateria-cardiovascular')"
        )
        out.append("    ),")
        out.append("    qs AS (")
        out.append(
            "        SELECT qt.id, qt.type, qt.section, qt.sort_order, qt.code AS qcode, qt.default_value,"
        )
        out.append(
            "               (SELECT max(o.score_value) FROM app.health_test_answer_options o"
        )
        out.append(
            "                WHERE o.question_id = qt.id AND o.is_active) AS max_score"
        )
        out.append("        FROM app.health_test_questions qt")
        out.append(
            "        WHERE qt.version_id = (SELECT v2.id FROM app.health_test_versions v2"
        )
        out.append(
            f"                               JOIN app.health_test_instruments i2 ON i2.id = v2.instrument_id"
        )
        out.append(
            f"                               WHERE i2.code = '{icode}' AND v2.is_current)"
        )
        out.append("          AND qt.is_active")
        out.append("    ),")
        out.append("    pick AS (")
        out.append("        SELECT DISTINCT ON (evs.id, qs.id)")
        out.append(
            "               evs.id AS evaluation_id, qs.id AS question_id, o.id AS answer_option_id,"
        )
        out.append("               evs.started_at")
        out.append("        FROM evs")
        out.append("        CROSS JOIN qs")
        out.append("        JOIN demo d ON d.id = evs.patient_id")
        out.append(
            "        JOIN app.health_test_answer_options o ON o.question_id = qs.id AND o.is_active"
        )
        out.append("        WHERE qs.type IN ('scale', 'single', 'multi')")
        out.append("        ORDER BY evs.id, qs.id,")
        out.append(
            "                 abs(o.score_value - ((ARRAY[0.15, 0.35, 0.55, 0.75, 0.9])[1 + ((d.ord + %d) %% 5)] * qs.max_score)),"
            % off
        )
        out.append("                 o.sort_order")
        out.append("    )")
        out.append("INSERT INTO app.health_test_responses")
        out.append("    (id, evaluation_id, question_id, answer_option_id, created_at)")
        out.append(
            "SELECT gen_random_uuid(), p.evaluation_id, p.question_id, p.answer_option_id, p.started_at"
        )
        out.append("FROM pick p")
        out.append("WHERE NOT EXISTS (SELECT 1 FROM app.health_test_responses r")
        out.append(
            "                  WHERE r.evaluation_id = p.evaluation_id AND r.question_id = p.question_id);"
        )
        out.append("")
        if icode == "historia-clinica":
            out.append(
                "-- G6b) Respuestas numéricas/abiertas (historia-clinica): valores por código"
            )
            out.append("WITH")
            out.append(f"    {demo_cte(scope)},")
            out.append("    evs AS (")
            out.append("        SELECT ev.id, ev.patient_id, ev.started_at")
            out.append("        FROM app.health_test_evaluations ev")
            out.append(
                "        JOIN app.health_test_assignments a ON a.id = ev.assignment_id"
            )
            out.append(
                "        JOIN app.health_test_battery_assignments ba ON ba.id = a.battery_assignment_id"
            )
            out.append(
                "        JOIN app.health_test_batteries b ON b.id = ba.battery_id"
            )
            out.append(
                "        JOIN app.health_test_versions v ON v.id = ev.version_id"
            )
            out.append(
                "        JOIN app.health_test_instruments i ON i.id = v.instrument_id AND i.code = 'historia-clinica'"
            )
            out.append("        WHERE ev.status = 'completed'")
            out.append("          AND ev.patient_id IN (SELECT id FROM demo)")
            out.append(
                "          AND b.code IN ('bateria-seguimiento', 'bateria-cardiovascular')"
            )
            out.append("    ),")
            out.append("    qs AS (")
            out.append(
                "        SELECT qt.id, qt.type, qt.code AS qcode, qt.default_value"
            )
            out.append("        FROM app.health_test_questions qt")
            out.append(
                "        WHERE qt.version_id = (SELECT v2.id FROM app.health_test_versions v2"
            )
            out.append(
                "                               JOIN app.health_test_instruments i2 ON i2.id = v2.instrument_id"
            )
            out.append(
                "                               WHERE i2.code = 'historia-clinica' AND v2.is_current)"
            )
            out.append("          AND qt.is_active AND qt.type IN ('num', 'open')")
            out.append("    )")
            out.append("INSERT INTO app.health_test_responses")
            out.append("    (id, evaluation_id, question_id, value_text, created_at)")
            out.append("SELECT gen_random_uuid(), evs.id, qs.id,")
            out.append("       CASE qs.type")
            out.append("         WHEN 'num' THEN CASE qs.qcode")
            out.append(
                "             WHEN 'peso' THEN '88' WHEN 'talla' THEN '170' WHEN 'cintura' THEN '102'"
            )
            out.append(
                "             WHEN 'cadera' THEN '105' WHEN 'muneca' THEN '17' WHEN 'edad' THEN '45'"
            )
            out.append("             ELSE COALESCE(qs.default_value::text, '0') END")
            out.append("         ELSE (ARRAY[")
            out.append(
                "             'Quería recuperar energía y control de mi glucosa.',"
            )
            out.append(
                "             'Mi familia tiene historia de diabetes y quiero prevenirla.',"
            )
            out.append(
                "             'Me cansé de sentirme sin energía; quiero vivir mejor.',"
            )
            out.append(
                "             'Por mis hijos: quiero estar presente y sano muchos años.',"
            )
            out.append(
                "             'Tras un susto de salud decidí tomar mi vida en serio.']"
            )
            out.append("             )[1 + (d.ord % 5)]")
            out.append("       END,")
            out.append("       evs.started_at")
            out.append("FROM evs")
            out.append("CROSS JOIN qs")
            out.append("JOIN demo d ON d.id = evs.patient_id")
            out.append("WHERE NOT EXISTS (SELECT 1 FROM app.health_test_responses r")
            out.append(
                "                  WHERE r.evaluation_id = evs.id AND r.question_id = qs.id);"
            )
            out.append("")

    # G6c) Respuestas parciales del orp (en curso)
    out.append(
        "-- G6c) Respuestas PARCIALES de orp (evaluación 'started': la mitad par)"
    )
    out.append("WITH")
    out.append(f"    {demo_cte(scope)},")
    out.append("    evs AS (")
    out.append("        SELECT ev.id, ev.patient_id, ev.started_at")
    out.append("        FROM app.health_test_evaluations ev")
    out.append("        JOIN app.health_test_assignments a ON a.id = ev.assignment_id")
    out.append(
        "        JOIN app.health_test_battery_assignments ba ON ba.id = a.battery_assignment_id"
    )
    out.append(
        "        JOIN app.health_test_batteries b ON b.id = ba.battery_id AND b.code = 'bateria-cardiovascular'"
    )
    out.append("        JOIN app.health_test_versions v ON v.id = ev.version_id")
    out.append(
        "        JOIN app.health_test_instruments i ON i.id = v.instrument_id AND i.code = 'orp'"
    )
    out.append("        WHERE ev.status = 'started'")
    out.append("          AND ev.patient_id IN (SELECT id FROM demo)")
    out.append("    ),")
    out.append("    qs AS (")
    out.append("        SELECT qt.id, qt.type, qt.sort_order,")
    out.append(
        "               (SELECT max(o.score_value) FROM app.health_test_answer_options o"
    )
    out.append(
        "                WHERE o.question_id = qt.id AND o.is_active) AS max_score"
    )
    out.append("        FROM app.health_test_questions qt")
    out.append(
        "        WHERE qt.version_id = (SELECT v2.id FROM app.health_test_versions v2"
    )
    out.append(
        "                               JOIN app.health_test_instruments i2 ON i2.id = v2.instrument_id"
    )
    out.append(
        "                               WHERE i2.code = 'orp' AND v2.is_current)"
    )
    out.append("          AND qt.is_active AND qt.type IN ('scale', 'single', 'multi')")
    out.append("    ),")
    out.append("    pick AS (")
    out.append("        SELECT DISTINCT ON (evs.id, qs.id)")
    out.append(
        "               evs.id AS evaluation_id, qs.id AS question_id, o.id AS answer_option_id,"
    )
    out.append("               evs.started_at")
    out.append("        FROM evs")
    out.append("        CROSS JOIN qs")
    out.append("        JOIN demo d ON d.id = evs.patient_id")
    out.append(
        "        JOIN app.health_test_answer_options o ON o.question_id = qs.id AND o.is_active"
    )
    out.append("        WHERE qs.sort_order % 2 = 0")
    out.append("        ORDER BY evs.id, qs.id,")
    out.append(
        "                 abs(o.score_value - ((ARRAY[0.15, 0.35, 0.55, 0.75, 0.9])[1 + ((d.ord + %d) %% 5)] * qs.max_score)),"
        % PARTIAL_INSTR[1]
    )
    out.append("                 o.sort_order")
    out.append("    )")
    out.append("INSERT INTO app.health_test_responses")
    out.append("    (id, evaluation_id, question_id, answer_option_id, created_at)")
    out.append(
        "SELECT gen_random_uuid(), p.evaluation_id, p.question_id, p.answer_option_id, p.started_at"
    )
    out.append("FROM pick p")
    out.append("WHERE NOT EXISTS (SELECT 1 FROM app.health_test_responses r")
    out.append(
        "                  WHERE r.evaluation_id = p.evaluation_id AND r.question_id = p.question_id);"
    )
    out.append("")

    # G7) Score de evaluaciones + resultados score
    out.append(
        "-- G7) Score y score_percentage de las evaluaciones completadas propias"
    )
    out.append("WITH")
    out.append(f"    {demo_cte(scope)},")
    out.append("    myev AS (")
    out.append("        SELECT ev.id")
    out.append("        FROM app.health_test_evaluations ev")
    out.append("        JOIN app.health_test_assignments a ON a.id = ev.assignment_id")
    out.append(
        "        JOIN app.health_test_battery_assignments ba ON ba.id = a.battery_assignment_id"
    )
    out.append("        JOIN app.health_test_batteries b ON b.id = ba.battery_id")
    out.append("        WHERE ev.status = 'completed' AND ev.score IS NULL")
    out.append("          AND ev.patient_id IN (SELECT id FROM demo)")
    out.append(
        "          AND b.code IN ('bateria-seguimiento', 'bateria-cardiovascular')"
    )
    out.append("    ),")
    out.append("    agg AS (")
    out.append("        SELECT r.evaluation_id,")
    out.append("               sum(o.score_value) AS total,")
    out.append(
        "               sum((SELECT max(x.score_value) FROM app.health_test_answer_options x"
    )
    out.append(
        "                    WHERE x.question_id = r.question_id AND x.is_active)) AS max_total"
    )
    out.append("        FROM app.health_test_responses r")
    out.append(
        "        JOIN app.health_test_answer_options o ON o.id = r.answer_option_id"
    )
    out.append("        WHERE r.evaluation_id IN (SELECT id FROM myev)")
    out.append("        GROUP BY r.evaluation_id")
    out.append("    ),")
    out.append("    upd AS (")
    out.append("        UPDATE app.health_test_evaluations ev")
    out.append("        SET score = agg.total,")
    out.append(
        "            score_percentage = round(agg.total * 100.0 / NULLIF(agg.max_total, 0), 2),"
    )
    out.append("            completed_at = ev.started_at + interval '11 minutes'")
    out.append("        FROM agg WHERE agg.evaluation_id = ev.id")
    out.append("    )")
    out.append("INSERT INTO app.health_test_results")
    out.append(
        "    (id, evaluation_id, result_type, code, label, value, qualifier, severity, created_at)"
    )
    out.append(
        "SELECT gen_random_uuid(), agg.evaluation_id, 'score', i.code, 'Score total',"
    )
    out.append("       agg.total, rg.label, rg.severity, ev.completed_at")
    out.append("FROM agg")
    out.append("JOIN app.health_test_evaluations ev ON ev.id = agg.evaluation_id")
    out.append("JOIN app.health_test_versions v ON v.id = ev.version_id")
    out.append("JOIN app.health_test_instruments i ON i.id = v.instrument_id")
    out.append("JOIN LATERAL (")
    out.append("    SELECT rq.label, rq.severity")
    out.append("    FROM app.health_test_score_ranges rq")
    out.append("    WHERE rq.version_id = v.id AND rq.is_active")
    out.append("      AND agg.total >= rq.min_value AND agg.total <= rq.max_value")
    out.append("    ORDER BY rq.min_value DESC LIMIT 1")
    out.append(") rg ON true")
    out.append("WHERE NOT EXISTS (SELECT 1 FROM app.health_test_results x")
    out.append("                  WHERE x.evaluation_id = agg.evaluation_id")
    out.append("                    AND x.result_type = 'score' AND x.code = i.code);")
    out.append("")

    # G8) Subescalas por sección
    out.append(
        "-- G8) Resultados 'subscale' (suma por sección, interpretada con los rangos)"
    )
    out.append("WITH")
    out.append(f"    {demo_cte(scope)},")
    out.append("    myev AS (")
    out.append("        SELECT ev.id, ev.completed_at")
    out.append("        FROM app.health_test_evaluations ev")
    out.append("        JOIN app.health_test_assignments a ON a.id = ev.assignment_id")
    out.append(
        "        JOIN app.health_test_battery_assignments ba ON ba.id = a.battery_assignment_id"
    )
    out.append("        JOIN app.health_test_batteries b ON b.id = ba.battery_id")
    out.append("        WHERE ev.status = 'completed'")
    out.append("          AND ev.patient_id IN (SELECT id FROM demo)")
    out.append(
        "          AND b.code IN ('bateria-seguimiento', 'bateria-cardiovascular')"
    )
    out.append("    ),")
    out.append("    sec AS (")
    out.append("        SELECT r.evaluation_id, q.section, sum(o.score_value) AS sval")
    out.append("        FROM app.health_test_responses r")
    out.append(
        "        JOIN app.health_test_answer_options o ON o.id = r.answer_option_id"
    )
    out.append("        JOIN app.health_test_questions q ON q.id = r.question_id")
    out.append("        WHERE r.evaluation_id IN (SELECT id FROM myev)")
    out.append("          AND q.section IS NOT NULL AND q.section <> ''")
    out.append("        GROUP BY r.evaluation_id, q.section")
    out.append("    )")
    out.append("INSERT INTO app.health_test_results")
    out.append(
        "    (id, evaluation_id, result_type, code, label, value, qualifier, severity, created_at)"
    )
    out.append("SELECT gen_random_uuid(), sec.evaluation_id, 'subscale',")
    out.append(
        "       i.code || '.' || sec.section, sec.section, sec.sval, rg.label, rg.severity,"
    )
    out.append("       myev.completed_at")
    out.append("FROM sec")
    out.append("JOIN myev ON myev.id = sec.evaluation_id")
    out.append("JOIN app.health_test_evaluations ev ON ev.id = sec.evaluation_id")
    out.append("JOIN app.health_test_versions v ON v.id = ev.version_id")
    out.append("JOIN app.health_test_instruments i ON i.id = v.instrument_id")
    out.append("JOIN LATERAL (")
    out.append("    SELECT rq.label, rq.severity")
    out.append("    FROM app.health_test_score_ranges rq")
    out.append("    WHERE rq.version_id = v.id AND rq.is_active")
    out.append("      AND sec.sval >= rq.min_value AND sec.sval <= rq.max_value")
    out.append("    ORDER BY rq.min_value DESC LIMIT 1")
    out.append(") rg ON true")
    out.append("WHERE NOT EXISTS (SELECT 1 FROM app.health_test_results x")
    out.append("                  WHERE x.evaluation_id = sec.evaluation_id")
    out.append("                    AND x.result_type = 'subscale'")
    out.append("                    AND x.code = i.code || '.' || sec.section);")
    out.append("")

    # G9) Indicadores derivados (solo iac-adresd)
    out.append("-- G9) Indicadores derivados del IAC-ADRESD: iapnea e iadherencia")
    out.append("WITH")
    out.append(f"    {demo_cte(scope)},")
    out.append("    myev_iac AS (")
    out.append("        SELECT ev.id, ev.completed_at, ev.score_percentage")
    out.append("        FROM app.health_test_evaluations ev")
    out.append("        JOIN app.health_test_versions v ON v.id = ev.version_id")
    out.append(
        "        JOIN app.health_test_instruments i ON i.id = v.instrument_id AND i.code = 'iac-adresd'"
    )
    out.append("        WHERE ev.status = 'completed' AND ev.score IS NOT NULL")
    out.append("          AND ev.patient_id IN (SELECT id FROM demo)")
    out.append("    ),")
    out.append("    rop AS (")
    out.append("        SELECT r.evaluation_id, sum(o.score_value) AS sval")
    out.append("        FROM app.health_test_responses r")
    out.append("        JOIN app.health_test_questions q ON q.id = r.question_id")
    out.append(
        "        JOIN app.health_test_answer_options o ON o.id = r.answer_option_id"
    )
    out.append("        WHERE r.evaluation_id IN (SELECT id FROM myev_iac)")
    out.append("          AND q.section = 'Red flags · Ronquidos'")
    out.append("        GROUP BY r.evaluation_id")
    out.append("    )")
    out.append("INSERT INTO app.health_test_results")
    out.append(
        "    (id, evaluation_id, result_type, code, label, value, qualifier, severity, created_at)"
    )
    out.append(
        "SELECT gen_random_uuid(), m.id, 'indicator', v.code, v.label, v.val, v.qual, v.sev, m.completed_at"
    )
    out.append("FROM myev_iac m")
    out.append("JOIN LATERAL (")
    out.append("    SELECT 'iapnea' AS code, 'Sospecha de apnea' AS label,")
    out.append("           COALESCE(rop.sval, 0) AS val,")
    out.append(
        "           CASE WHEN COALESCE(rop.sval, 0) >= 2 THEN 'alto' ELSE 'bajo' END AS qual,"
    )
    out.append(
        "           CASE WHEN COALESCE(rop.sval, 0) >= 2 THEN 'high' ELSE 'low' END AS sev"
    )
    out.append("    FROM (SELECT r2.evaluation_id, sum(o2.score_value) AS sval")
    out.append("          FROM app.health_test_responses r2")
    out.append("          JOIN app.health_test_questions q2 ON q2.id = r2.question_id")
    out.append(
        "          JOIN app.health_test_answer_options o2 ON o2.id = r2.answer_option_id"
    )
    out.append(
        "          WHERE r2.evaluation_id = m.id AND q2.section = 'Red flags · Ronquidos'"
    )
    out.append("          GROUP BY r2.evaluation_id) rop")
    out.append(") v ON true")
    out.append("WHERE NOT EXISTS (SELECT 1 FROM app.health_test_results x")
    out.append("                  WHERE x.evaluation_id = m.id")
    out.append(
        "                    AND x.result_type = 'indicator' AND x.code = v.code);"
    )
    out.append("")
    # El segundo INSERT necesita su propio WITH: los CTEs no cruzan sentencias.
    out.append("WITH")
    out.append(f"    {demo_cte(scope)},")
    out.append("    myev_iac AS (")
    out.append("        SELECT ev.id, ev.completed_at, ev.score_percentage")
    out.append("        FROM app.health_test_evaluations ev")
    out.append("        JOIN app.health_test_versions v ON v.id = ev.version_id")
    out.append(
        "        JOIN app.health_test_instruments i ON i.id = v.instrument_id AND i.code = 'iac-adresd'"
    )
    out.append("        WHERE ev.status = 'completed' AND ev.score IS NOT NULL")
    out.append("          AND ev.patient_id IN (SELECT id FROM demo)")
    out.append("    )")
    out.append("INSERT INTO app.health_test_results")
    out.append(
        "    (id, evaluation_id, result_type, code, label, value, qualifier, severity, created_at)"
    )
    out.append(
        "SELECT gen_random_uuid(), m.id, 'indicator', 'iadherencia', 'Índice de adherencia',"
    )
    out.append("       m.score_percentage,")
    out.append("       CASE WHEN m.score_percentage < 40 THEN 'bajo' ELSE 'alto' END,")
    out.append(
        "       CASE WHEN m.score_percentage < 40 THEN 'high' ELSE 'low' END, m.completed_at"
    )
    out.append("FROM myev_iac m")
    out.append("WHERE NOT EXISTS (SELECT 1 FROM app.health_test_results x")
    out.append("                  WHERE x.evaluation_id = m.id")
    out.append(
        "                    AND x.result_type = 'indicator' AND x.code = 'iadherencia');"
    )
    out.append("")

    # G10) Cierre de la batería de seguimiento
    out.append("-- G10) La batería de seguimiento queda 'completed' cuando todos sus")
    out.append("--     instrumentos están completados")
    out.append("WITH")
    out.append(f"    {demo_cte(scope)}")
    out.append("UPDATE app.health_test_battery_assignments ba")
    out.append(
        "SET status = 'completed', completed_at = ba.assigned_at + interval '10 days'"
    )
    out.append("FROM app.health_test_batteries b")
    out.append("WHERE ba.battery_id = b.id AND b.code = 'bateria-seguimiento'")
    out.append("  AND ba.status <> 'completed'")
    out.append("  AND ba.patient_id IN (SELECT id FROM demo)")
    out.append("  AND NOT EXISTS (SELECT 1 FROM app.health_test_assignments a")
    out.append(
        "                  WHERE a.battery_assignment_id = ba.id AND a.status <> 'completed');"
    )
    return out


# ---------------------------------------------------------------------------
# Sección H: rutinas de ejercicio asignadas
# ---------------------------------------------------------------------------


def build_routines(scope: str) -> list[str]:
    out: list[str] = []
    out.append("")
    out.append(
        "-- ===================================================================="
    )
    out.append("-- H) RUTINAS DE EJERCICIO ASIGNADAS: 2 activas por paciente")
    out.append("--    (rotación determinista sobre el catálogo existente).")
    out.append(
        "-- ===================================================================="
    )
    out.append("WITH")
    out.append(f"    {demo_cte(scope)}")
    out.append("INSERT INTO app.routine_assignments")
    out.append(
        "    (id, patient_id, routine_id, start_date, end_date, frequency, status, notes, created_by, created_at, updated_at)"
    )
    out.append("SELECT gen_random_uuid(), d.id, r.id,")
    out.append("       current_date - (60 + (d.ord % 10)),")
    out.append("       current_date + 90,")
    out.append("       3, 1,")
    out.append("       CASE v.sel % 3")
    out.append(
        "         WHEN 0 THEN 'Empezar con series cortas y aumentar progresivamente.'"
    )
    out.append(
        "         WHEN 1 THEN 'Priorizar técnica sobre carga; registrar el esfuerzo en la app.'"
    )
    out.append(
        "         ELSE 'Combinar con una caminata de 10 minutos de calentamiento.' END,"
    )
    out.append(f"       '{SEED_USER}', now(), now()")
    out.append("FROM demo d")
    out.append("JOIN (VALUES")
    rt_vals = ", ".join(f"('{name}', {i})" for name, i in ROUTINE_POOL)
    out.append(f"    {rt_vals}")
    out.append(") AS v(name, sel) ON true")
    out.append("JOIN app.exercise_routines r ON r.name = v.name")
    out.append("WHERE (v.sel = d.ord % 6 OR v.sel = (d.ord + 2) % 6)")
    out.append("  AND NOT EXISTS (SELECT 1 FROM app.routine_assignments x")
    out.append("                  WHERE x.patient_id = d.id AND x.routine_id = r.id);")
    return out


# ---------------------------------------------------------------------------
# Sección I: ingesta de nutrición (21 días)
# ---------------------------------------------------------------------------


def build_nutrition(scope: str) -> list[str]:
    out: list[str] = []
    out.append("")
    out.append(
        "-- ===================================================================="
    )
    out.append("-- I) NUTRICIÓN: habit_checks + nutrition_intake_logs de los últimos")
    out.append("--    21 días (5 comidas/día: des, alm, mer, cen, agua). Macros")
    out.append("--    coherentes con el objetivo calórico por paciente. ON CONFLICT")
    out.append("--    sobre las claves únicas reales; no pisa registros existentes.")
    out.append(
        "-- ===================================================================="
    )
    out.append("-- I1) habit_checks (idempotente por paciente+plantilla+fecha)")
    out.append("WITH")
    out.append(f"    {demo_cte(scope)},")
    out.append("    days AS (SELECT generate_series(1, 21) AS k)")
    out.append("INSERT INTO app.habit_checks")
    out.append(
        "    (id, patient_id, habit_template_id, local_date, is_done, created_at)"
    )
    out.append("SELECT gen_random_uuid(), d.id, ht.id, current_date - g.k, true, now()")
    out.append("FROM demo d")
    out.append("CROSS JOIN days g")
    out.append(
        "JOIN app.habit_templates ht ON ht.code IN ('des', 'alm', 'mer', 'cen', 'agua')"
    )
    out.append("ON CONFLICT (patient_id, habit_template_id, local_date) DO NOTHING;")
    out.append("")
    out.append("-- I2) Ingestas con macros por comida (25/35/10/30% del objetivo)")
    out.append("WITH")
    out.append(f"    {demo_cte(scope)},")
    out.append("    days AS (SELECT generate_series(1, 21) AS k)")
    out.append("INSERT INTO app.nutrition_intake_logs")
    out.append(
        "    (id, patient_id, habit_check_id, local_date, meal_code, calories, protein_g,"
    )
    out.append(
        "     carbs_g, fat_g, fiber_g, water_ml, source, nutrition_plan_id, created_at)"
    )
    out.append("SELECT gen_random_uuid(), d.id, hc.id, hc.local_date, ht.code,")
    out.append("       CASE WHEN ht.code = 'agua' THEN NULL")
    out.append("            ELSE ROUND(kcal.target * sh.s * jit.j, 0) END,")
    out.append("       CASE WHEN ht.code = 'agua' THEN NULL")
    out.append(
        "            ELSE ROUND(kcal.target * sh.s * jit.j * 0.25 / 4.0, 1) END,"
    )
    out.append("       CASE WHEN ht.code = 'agua' THEN NULL")
    out.append(
        "            ELSE ROUND(kcal.target * sh.s * jit.j * 0.45 / 4.0, 1) END,"
    )
    out.append("       CASE WHEN ht.code = 'agua' THEN NULL")
    out.append(
        "            ELSE ROUND(kcal.target * sh.s * jit.j * 0.30 / 9.0, 1) END,"
    )
    out.append(
        "       CASE ht.code WHEN 'des' THEN 6.0 WHEN 'alm' THEN 10.0 WHEN 'mer' THEN 3.0"
    )
    out.append("                    WHEN 'cen' THEN 9.0 ELSE NULL END,")
    out.append(
        "       CASE WHEN ht.code = 'agua' THEN 1500 + (d.ord % 5) * 250 ELSE NULL END,"
    )
    out.append(f"       '{SOURCE_INTAKE}',")
    out.append("       (SELECT npa.plan_id FROM app.nutrition_plan_assignments npa")
    out.append("        WHERE npa.patient_id = d.id AND npa.status = 1")
    out.append("        ORDER BY npa.start_date DESC LIMIT 1),")
    out.append("       now()")
    out.append("FROM demo d")
    out.append("CROSS JOIN days g")
    out.append(
        "JOIN app.habit_templates ht ON ht.code IN ('des', 'alm', 'mer', 'cen', 'agua')"
    )
    out.append(
        "JOIN app.habit_checks hc ON hc.patient_id = d.id AND hc.habit_template_id = ht.id"
    )
    out.append("    AND hc.local_date = current_date - g.k")
    out.append(
        "CROSS JOIN LATERAL (SELECT (ARRAY[1400, 1600, 1800, 1900])[1 + d.ord % 4] AS target) kcal"
    )
    out.append(
        "CROSS JOIN LATERAL (SELECT CASE ht.code WHEN 'des' THEN 0.25 WHEN 'alm' THEN 0.35"
    )
    out.append(
        "                            WHEN 'mer' THEN 0.10 WHEN 'cen' THEN 0.30 ELSE 0 END AS s) sh"
    )
    out.append(
        "CROSS JOIN LATERAL (SELECT 0.9 + ((d.ord * 7 + g.k) % 7)::numeric / 30.0 AS j) jit"
    )
    out.append("ON CONFLICT (patient_id, local_date, meal_code) DO NOTHING;")
    return out


# ---------------------------------------------------------------------------
# Sección J: citas de telemedicina (pasadas y futuras)
# ---------------------------------------------------------------------------


def build_appointments(scope: str) -> list[str]:
    out: list[str] = []
    out.append("")
    out.append(
        "-- ===================================================================="
    )
    out.append("-- J) TELEMEDICINA: 5 citas pasadas por paciente (3 Completed,")
    out.append(
        "--    1 Cancelled, 1 NoShow) y 2 futuras Confirmed, ligando el paciente"
    )
    out.append("--    con su profesional existente. Respeta el índice único parcial de")
    out.append("--    anti doble reserva (Requested/Confirmed/InProgress) con guardas")
    out.append("--    NOT EXISTS (professional, scheduled_start).")
    out.append(
        "-- ===================================================================="
    )
    out.append("-- J1) Citas pasadas (ventana 3 meses)")
    ts_slot = ts_dyn(
        "current_date - (8 + v.k * 17 + (d.ord % 9))",
        "'13:' || lpad((((d.ord + v.k) % 2) * 30)::text, 2, '0')",
    )
    out.append("WITH")
    out.append(f"    {demo_cte(scope)},")
    out.append(f"    {prof_for_cte()},")
    out.append("    slots AS (")
    out.append("        SELECT d.id AS patient_id, d.ord, v.k, v.status,")
    out.append(f"               {ts_slot} AS ts")
    out.append("        FROM demo d")
    out.append(
        "        JOIN (VALUES (0, 'Completed'), (1, 'Completed'), (2, 'Completed'),"
    )
    out.append("             (3, 'Cancelled'), (4, 'NoShow')) AS v(k, status) ON true")
    out.append("    )")
    out.append("INSERT INTO tele.appointments")
    out.append(
        "    (id, patient_id, professional_id, specialty_id, organization_id, clinic_id,"
    )
    out.append(
        "     scheduled_start, scheduled_end, duration_minutes, status, reschedule_count,"
    )
    out.append("     cancellation_reason, cancelled_by, cancelled_at, no_show_reason,")
    out.append("     created_by, created_at, updated_at, completed_at)")
    out.append("SELECT gen_random_uuid(), s.patient_id, pf.professional_id,")
    out.append(
        f"       (ARRAY['{SPEC_OBESITY}', '{SPEC_INTERNAL}']::uuid[])[1 + s.ord % 2],"
    )
    out.append(f"       '{ORG_ID}', '{CLINIC_ID}',")
    out.append("       s.ts, s.ts + interval '30 minutes', 30, s.status, 0,")
    out.append(
        "       CASE s.status WHEN 'Cancelled' THEN 'Solicitud del paciente' END,"
    )
    out.append("       CASE s.status WHEN 'Cancelled'")
    out.append(
        "            THEN (ARRAY['Patient', 'Professional'])[1 + s.ord % 2] END,"
    )
    out.append(
        "       CASE s.status WHEN 'Cancelled' THEN s.ts - interval '2 days' END,"
    )
    out.append(
        "       CASE s.status WHEN 'NoShow' THEN 'El paciente no se conectó a la sala virtual' END,"
    )
    out.append(f"       '{SEED_USER}', s.ts - interval '7 days', now(),")
    out.append(
        "       CASE s.status WHEN 'Completed' THEN s.ts + interval '38 minutes' END"
    )
    out.append("FROM slots s")
    out.append("JOIN prof_for pf ON pf.patient_id = s.patient_id")
    out.append("WHERE (SELECT count(*) FROM tele.appointments x")
    out.append(
        "       WHERE x.patient_id = s.patient_id AND x.created_by = '%s'" % SEED_USER
    )
    out.append("         AND x.scheduled_start < now()) < 5")
    out.append("  AND NOT EXISTS (SELECT 1 FROM tele.appointments y")
    out.append("                  WHERE y.patient_id = s.patient_id")
    out.append("                    AND y.professional_id = pf.professional_id")
    out.append("                    AND y.scheduled_start = s.ts);")
    out.append("")
    out.append("-- J2) Citas futuras Confirmed (2 por paciente; día distinto por slot;")
    out.append("--     hora/minuto deterministas que garantizan separación >= 30 min")
    out.append("--     entre slots del mismo profesional; guarda anti-solapamiento por")
    out.append("--     rango contra citas activas existentes (exclusión GiST).")
    ts_fut = ts_dyn("current_date + 7 + ((d.ord * 2 + i.i) % 30)", "'13:00'")
    out.append("WITH")
    out.append(f"    {demo_cte(scope)},")
    out.append(f"    {prof_for_cte()},")
    out.append("    slots AS (")
    out.append("        SELECT d.id AS patient_id, d.ord, i.i,")
    out.append(f"               {ts_fut}")
    out.append("               + ((((d.ord + i.i) % 9)::text || ' hours'))::interval")
    out.append(
        "               + ((((d.ord % 2) * 30)::text || ' minutes'))::interval AS ts"
    )
    out.append("        FROM demo d CROSS JOIN (VALUES (0), (1)) AS i(i)")
    out.append("    )")
    out.append("INSERT INTO tele.appointments")
    out.append(
        "    (id, patient_id, professional_id, specialty_id, organization_id, clinic_id,"
    )
    out.append(
        "     scheduled_start, scheduled_end, duration_minutes, status, reschedule_count,"
    )
    out.append("     created_by, created_at, updated_at)")
    out.append("SELECT gen_random_uuid(), s.patient_id, pf.professional_id,")
    out.append(
        f"       (ARRAY['{SPEC_OBESITY}', '{SPEC_INTERNAL}']::uuid[])[1 + s.ord % 2],"
    )
    out.append(f"       '{ORG_ID}', '{CLINIC_ID}',")
    out.append("       s.ts, s.ts + interval '30 minutes', 30, 'Confirmed', 0,")
    out.append(f"       '{SEED_USER}', now(), now()")
    out.append("FROM slots s")
    out.append("JOIN prof_for pf ON pf.patient_id = s.patient_id")
    out.append("WHERE (SELECT count(*) FROM tele.appointments x")
    out.append(
        "       WHERE x.patient_id = s.patient_id AND x.created_by = '%s'" % SEED_USER
    )
    out.append("         AND x.scheduled_start > now()) < 2")
    out.append("  AND NOT EXISTS (SELECT 1 FROM tele.appointments y")
    out.append("                  WHERE y.professional_id = pf.professional_id")
    out.append(
        "                    AND y.status IN ('Requested', 'Confirmed', 'InProgress')"
    )
    out.append("                    AND tstzrange(y.scheduled_start, y.scheduled_end)")
    out.append(
        "                        && tstzrange(s.ts, s.ts + interval '30 minutes'))"
    )
    out.append("  AND NOT EXISTS (SELECT 1 FROM tele.appointments y")
    out.append(
        "                  WHERE y.patient_id = s.patient_id AND y.scheduled_start = s.ts);"
    )
    return out


# ---------------------------------------------------------------------------
# Sección K: device tokens demo FCM
# ---------------------------------------------------------------------------


def build_device_tokens() -> list[str]:
    out: list[str] = []
    out.append("")
    out.append(
        "-- ===================================================================="
    )
    out.append("-- K) DEVICE TOKENS DEMO para push FCM: 8 usuarios prioritarios ×")
    out.append("--    (ios, android) = 8 tokens por plataforma con prefijo fcm-seed-*.")
    out.append("--    Idempotente por la clave única (user_id, token).")
    out.append(
        "-- ===================================================================="
    )
    out.append("WITH fcm_users AS (")
    out.append('    SELECT u."Id" AS uid, v.prio')
    out.append('    FROM auth."Users" u')
    out.append("    JOIN (VALUES")
    for i, email in enumerate(FCM_PRIORITY_EMAILS):
        out.append(f"        ('{email.upper()}', {i}),")
    out[-1] = out[-1].rstrip(",")
    out.append('    ) AS v(email, prio) ON u."NormalizedEmail" = v.email')
    out.append(")")
    out.append("INSERT INTO app.device_tokens")
    out.append("    (id, user_id, token, platform, created_at)")
    out.append(
        "SELECT gen_random_uuid(), f.uid, 'fcm-seed-' || p.plat || '-' || (f.prio + 1),"
    )
    out.append("       p.plat, now()")
    out.append("FROM fcm_users f")
    out.append("CROSS JOIN (VALUES ('ios'), ('android')) AS p(plat)")
    out.append("WHERE f.prio < 8")
    out.append("ON CONFLICT (user_id, token) DO NOTHING;")
    return out


# ---------------------------------------------------------------------------
# Sección L: consultas de verificación (se ejecutan con --apply)
# ---------------------------------------------------------------------------


def build_verification() -> list[str]:
    out: list[str] = []
    out.append("")
    out.append(
        "-- ===================================================================="
    )
    out.append("-- L) VERIFICACIÓN (se ejecuta como parte del archivo; los SELECT solo")
    out.append("--    muestran, no modifican)")
    out.append(
        "-- ===================================================================="
    )
    out.append(f"-- Medios demo por categoría: {STORAGE_KEY}")
    out.append(
        "SELECT category, count(*), round(avg(duration_secs) / 60.0, 1) AS minutos_prom"
    )
    out.append("FROM app.media_items")
    out.append(f"WHERE storage_key = '{STORAGE_KEY}'")
    out.append("GROUP BY category ORDER BY count(*) DESC;")
    out.append("")
    out.append("-- Rotación por weekday en las plantillas activas")
    out.append("SELECT pt.code, t.weekday, m.title")
    out.append("FROM app.weekly_day_templates t")
    out.append("JOIN app.program_templates pt ON pt.id = t.template_id")
    out.append("JOIN app.media_items m ON m.id = t.media_id")
    out.append("WHERE t.task_code = 'podcast'")
    out.append("ORDER BY pt.code, t.weekday LIMIT 14;")
    return out


def run_psql(sql: str, tuples: bool = True) -> tuple[int, str, str]:
    """Ejecuta SQL contra la BD local docker (psql via docker exec)."""
    argv = [
        "docker",
        "exec",
        "-i",
        CONTAINER,
        "psql",
        "-U",
        "app_user",
        "-d",
        DB,
        "-v",
        "ON_ERROR_STOP=1",
    ]
    if tuples:
        argv += ["-t", "-A"]
    p = subprocess.run(
        argv,
        input=sql,
        text=True,
        encoding="utf-8",
        errors="replace",
        capture_output=True,
    )
    return p.returncode, p.stdout, p.stderr


COUNT_QUERIES: list[tuple[str, str]] = [
    ("media_items (total)", "SELECT count(*) FROM app.media_items;"),
    (
        f"media_items demo (storage_key={STORAGE_KEY})",
        f"SELECT count(*) FROM app.media_items WHERE storage_key = '{STORAGE_KEY}';",
    ),
    (
        "clinical_measurements (total)",
        "SELECT count(*) FROM app.clinical_measurements;",
    ),
    (
        f"clinical_measurements source={SOURCE_CM}",
        f"SELECT count(*) FROM app.clinical_measurements WHERE source = '{SOURCE_CM}';",
    ),
    ("health_test_results (total)", "SELECT count(*) FROM app.health_test_results;"),
    (
        "health_test_evaluations (total)",
        "SELECT count(*) FROM app.health_test_evaluations;",
    ),
    (
        "health_test_responses (total)",
        "SELECT count(*) FROM app.health_test_responses;",
    ),
    ("tele.appointments (total)", "SELECT count(*) FROM tele.appointments;"),
    (
        "tele.appointments creadas por este seed",
        f"SELECT count(*) FROM tele.appointments WHERE created_by = '{SEED_USER}';",
    ),
    (
        f"nutrition_intake_logs source={SOURCE_INTAKE}",
        f"SELECT count(*) FROM app.nutrition_intake_logs WHERE source = '{SOURCE_INTAKE}';",
    ),
    (
        "encounters creados por este seed",
        f"SELECT count(*) FROM app.encounters WHERE created_by = '{SEED_USER}';",
    ),
    (
        "device_tokens fcm-seed-*",
        "SELECT count(*) FROM app.device_tokens WHERE token LIKE 'fcm-seed-%';",
    ),
    (
        "routine_assignments creados por este seed",
        f"SELECT count(*) FROM app.routine_assignments WHERE created_by = '{SEED_USER}';",
    ),
    (
        "program_weeks (NUNCA tocado por este seed)",
        "SELECT count(*) FROM app.program_weeks;",
    ),
    (
        "program_templates (code/version)",
        "SELECT code || ' v' || version FROM app.program_templates;",
    ),
]

SAMPLE_QUERIES: list[tuple[str, str]] = [
    (
        "Media demo por categoría (duración media en minutos)",
        """
SELECT category, count(*) AS items, round(avg(duration_secs) / 60.0, 1) AS minutos_prom
FROM app.media_items
WHERE storage_key = '{STORAGE_KEY}'
GROUP BY category
ORDER BY count(*) DESC;
""",
    ),
    (
        "Rotación por weekday de podcasts en las plantillas (misma lógica del seeder)",
        """
SELECT pt.code AS plantilla, t.weekday, m.sort_order, m.title
FROM app.weekly_day_templates t
JOIN app.program_templates pt ON pt.id = t.template_id
JOIN app.media_items m ON m.id = t.media_id
WHERE t.task_code = 'podcast' AND pt.code = 'program-coppaddresd-83-days'
ORDER BY t.weekday;
""",
    ),
    (
        "Resultados de Tests de Salud del paciente demo 55551234 (score/severity)",
        """
SELECT i.code AS instrumento, r.result_type, r.code, r.value, r.qualifier, r.severity
FROM app.health_test_results r
JOIN app.health_test_evaluations e ON e.id = r.evaluation_id
JOIN app.health_test_versions v ON v.id = e.version_id
JOIN app.health_test_instruments i ON i.id = v.instrument_id
JOIN app.patient_profiles p ON p.id = e.patient_id
WHERE p.document_number = '55551234'
  AND e.started_at > now() - interval '90 days'
ORDER BY e.completed_at DESC, r.result_type
LIMIT 12;
""",
    ),
    (
        "Citas de telemedicina del seed por estado",
        f"""
SELECT status, count(*) AS citas
FROM tele.appointments
WHERE created_by = '{SEED_USER}'
GROUP BY status
ORDER BY citas DESC;
""",
    ),
]


def collect_counts() -> list[tuple[str, str]]:
    rows: list[tuple[str, str]] = []
    for label, sql in COUNT_QUERIES:
        rc, out, err = run_psql(sql)
        rows.append((label, out.strip() if rc == 0 else f"ERROR: {err.strip()[:160]}"))
    return rows


def collect_samples() -> list[tuple[str, str, str]]:
    rows: list[tuple[str, str, str]] = []
    for label, raw_sql in SAMPLE_QUERIES:
        sql = raw_sql.format(STORAGE_KEY=STORAGE_KEY)
        rc, out, err = run_psql(sql)
        rows.append(
            (label, sql, out.strip() if rc == 0 else f"ERROR: {err.strip()[:300]}")
        )
    return rows


def md_cell(v: str) -> str:
    return v.replace("\n", "; ").replace("|", "/")


def write_report(
    path: Path,
    scope: str,
    sql_bytes: int,
    before: list[tuple[str, str]],
    after: list[tuple[str, str]],
    samples: list[tuple[str, str, str]],
) -> None:
    lines: list[str] = []
    lines.append("# Informe: dataset rico de pruebas (rich_dataset_seed)")
    lines.append("")
    lines.append(f"- Generado por: `scripts/generate_rich_dataset_seed.py`")
    lines.append(
        f"- Archivo SQL: `scripts/generated/rich_dataset_seed.sql` ({sql_bytes:,} bytes)"
    )
    lines.append(f"- Alcance (scope): `{scope}`")
    lines.append(
        "- BD objetivo: docker `coppAddresd` → psql `-U app_user -d coppaddresd` (localhost:5432)"
    )
    lines.append(
        "- Reglas cumplidas: sin pacientes/profesionales nuevos · sin borrados ·"
    )
    lines.append(
        "  upserts idempotentes · `app.program_weeks` nunca escrito (semanas congeladas intactas)."
    )
    lines.append("")
    lines.append("## Qué genera")
    lines.append("")
    lines.append("| Sección | Contenido |")
    lines.append("|---|---|")
    lines.append(
        f"| A | 40 media_items Publicados (30 podcasts + 6 videos + 4 audios) en las 9 categorías; 8-25 min; chapters/takeaways JSON coherentes; day=1..83 y month=1..3 del programa 83 días/12 semanas; claves demo `{STORAGE_KEY}` / `{THUMB_KEY}` |"
    )
    lines.append(
        "| B | Asignación de podcasts a `weekly_day_templates` con la MISMA rotación por weekday de DevProgramContentSeeder (`pool[(weekday-1) % count]`); bump de `program_templates.version` solo si cambió algo |"
    )
    lines.append(
        "| C | `patient_professionals` (Assigned/Active) solo para pacientes demo sin vínculo |"
    )
    lines.append(
        "| D | 4 `app.encounters` canónicos por paciente (3 meses) con motivo y notas clínicas coherentes |"
    )
    lines.append(
        "| E | `clinical_measurements` (source=rich-seed): peso/cintura/FC semanales, TA quincenal, HbA1c/glucosa mensuales con tendencia de mejora; +13 `vital_signs` semanales por paciente |"
    )
    lines.append(
        "| F | Diagnósticos (ICD-10 reales), 2 alergias y 2 medicamentos por paciente |"
    )
    lines.append(
        "| G | Tests de Salud: batería seguimiento COMPLETADA + cardiovascular EN CURSO (historia-clinica completa, orp a medias, ers pendiente) + adherencia PENDIENTE; respuestas deterministas, scores/subescalas/indicadores con severity de los rangos reales |"
    )
    lines.append("| H | 2 rutinas de ejercicio activas por paciente |")
    lines.append(
        "| I | 21 días de `habit_checks` + `nutrition_intake_logs` (5 comidas/día con macros coherentes) |"
    )
    lines.append(
        "| J | 5 citas pasadas (3 Completed / 1 Cancelled / 1 NoShow) + 2 futuras Confirmed por paciente, ligando paciente ↔ profesional existentes |"
    )
    lines.append(
        "| K | 8 tokens `fcm-seed-*` por plataforma (ios/android) para los usuarios demo prioritarios |"
    )
    lines.append("")
    lines.append("## Conteos antes / después")
    lines.append("")
    lines.append("| Métrica | Antes | Después | Delta |")
    lines.append("|---|---|---|---|")
    for (label, b), (_, a) in zip(before, after):
        try:
            delta = int(a.split(";")[0].strip()) - int(b.split(";")[0].strip())
            delta_s = str(delta)
        except ValueError:
            delta_s = "-"
        lines.append(f"| {md_cell(label)} | {md_cell(b)} | {md_cell(a)} | {delta_s} |")
    lines.append("")
    lines.append("## Consultas de muestra")
    lines.append("")
    for i, (label, sql, out) in enumerate(samples, 1):
        lines.append(f"### {i}) {label}")
        lines.append("")
        lines.append("```sql")
        lines.append(sql.strip())
        lines.append("```")
        lines.append("")
        lines.append("```text")
        lines.append(out if out else "(sin filas)")
        lines.append("```")
        lines.append("")
    lines.append("## Notas de operación")
    lines.append("")
    lines.append("### Semanas congeladas (ProgramWeek.TasksSnapshot)")
    lines.append("Este seed NUNCA inserta ni actualiza `app.program_weeks`: el conteo")
    lines.append(
        "`program_weeks` de la tabla de arriba debe quedar idéntico antes/después."
    )
    lines.append(
        "Las semanas ya activadas conservan su snapshot horneado por diseño; solo"
    )
    lines.append(
        "las activaciones futuras heredan los podcasts nuevos de la plantilla."
    )
    lines.append("")
    lines.append("### Claves S3 pendientes de subir por el usuario")
    lines.append("")
    lines.append(f"- `media/podcasts/demo-copp.mp3` (audio de TODOS los 40 ítems)")
    lines.append(
        f"- `media/thumbnails/demo-copp.jpg` (miniatura de TODOS los 40 ítems)"
    )
    lines.append("")
    lines.append("### Idempotencia y re-ejecución")
    lines.append(
        "- `ON CONFLICT DO NOTHING` donde existe clave única de negocio: baterías"
    )
    lines.append("  `(code)`, alergias `(patient_id, allergen_id)`, ingestas")
    lines.append("  `(patient_id, local_date, meal_code)`, habit_checks")
    lines.append(
        "  `(patient_id, habit_template_id, local_date)`, tokens `(user_id, token)`."
    )
    lines.append(
        "- Guardas `NOT EXISTS` sobre clave de negocio donde la tabla solo tiene PK"
    )
    lines.append(
        "  (encounters, measurements, citas, evaluaciones, respuestas, resultados)."
    )
    lines.append(
        "- Todas las filas llevan marca de origen: `created_by`/`source` con el"
    )
    lines.append(
        f"  marcador `{SEED_USER}` (o `source = '{SOURCE_CM}'/'{SOURCE_INTAKE}'`)."
    )
    lines.append("- Las ventanas de fechas son relativas a `current_date`, así que la")
    lines.append("  re-ejecución mantiene el dataset fresco; los topes por paciente")
    lines.append(
        "  (4 encounters, 13 mediciones/métrica, 5 citas pasadas, 2 futuras) evitan"
    )
    lines.append("  crecimiento indefinido.")
    lines.append("")
    lines.append("### Cómo re-ejecutar")
    lines.append("")
    lines.append("```powershell")
    lines.append("cd coppAddresdBack/scripts")
    lines.append("python generate_rich_dataset_seed.py --apply")
    lines.append("```")
    lines.append("")
    lines.append("Manual:")
    lines.append("")
    lines.append("```powershell")
    lines.append(
        "Get-Content .\\generated\\rich_dataset_seed.sql -Raw | docker exec -i coppAddresd psql -U app_user -d coppaddresd -v ON_ERROR_STOP=1"
    )
    lines.append("```")
    lines.append("")
    lines.append(
        "> NUNCA ejecutar contra producción. Solo BD local docker `coppaddresd`."
    )
    lines.append("")
    path.write_text("\n".join(lines), encoding="utf-8", newline="\n")


def main() -> None:
    scope = "demo"
    apply = False
    args = sys.argv[1:]
    if "--apply" in args:
        apply = True
        args.remove("--apply")
    if "--scope" in args:
        idx = args.index("--scope")
        scope = args[idx + 1]
        del args[idx : idx + 2]
    if scope not in ("demo", "all-linked"):
        raise SystemExit("--scope debe ser 'demo' o 'all-linked'")

    header = [
        "-- ============================================================================",
        "-- rich_dataset_seed.sql — GENERADO por scripts/generate_rich_dataset_seed.py",
        "-- NO EDITAR A MANO: regenerar con  python generate_rich_dataset_seed.py",
        f"-- Alcance: pacientes con usuario demo (password Demo1234!) — scope={scope}",
        "-- Idempotente / sin borrados / nunca toca app.program_weeks (semanas congeladas).",
        f"-- Marca de origen: created_by/source {SEED_USER}",
        "-- Solo desarrollo local. NUNCA contra producción.",
        "-- ============================================================================",
        "",
        "BEGIN;",
    ]
    footer = ["", "COMMIT;", ""]

    sections = [
        build_media(),
        build_template_assignment(),
        build_patient_professionals(scope),
        build_encounters(scope),
        build_measurements(scope),
        build_vital_signs(scope),
        build_clinical_lists(scope),
        build_health_tests(scope),
        build_routines(scope),
        build_nutrition(scope),
        build_appointments(scope),
        build_device_tokens(),
        build_verification(),
    ]

    sql_text = (
        "\n".join(header)
        + "\n"
        + "\n".join(part for section in sections for part in section)
        + "\n"
        + "\n".join(footer)
    )

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(sql_text, encoding="utf-8", newline="\n")
    print(f"Seed generado en {OUT} ({len(sql_text):,} bytes)")

    if not apply:
        return

    print("== Conteos ANTES ==")
    before = collect_counts()
    for label, value in before:
        print(f"  {label}: {md_cell(value)}")

    print("== Aplicando el seed (psql ON_ERROR_STOP=1) ==")
    rc, out, err = run_psql(sql_text, tuples=False)
    if rc != 0:
        print("ERROR aplicando el seed:")
        print(err)
        raise SystemExit(1)

    print("== Conteos DESPUÉS ==")
    after = collect_counts()
    for label, value in after:
        print(f"  {label}: {md_cell(value)}")

    print("== Consultas de muestra ==")
    samples = collect_samples()
    for label, _, out in samples:
        print(f"-- {label}")
        print(out or "(sin filas)")
        print()

    write_report(REPORT, scope, len(sql_text), before, after, samples)
    print(f"Informe escrito en {REPORT}")


if __name__ == "__main__":
    main()
