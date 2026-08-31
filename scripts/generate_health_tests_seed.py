"""Genera Migrations/Seed/AddHealthTestsCatalogSeed.sql con el seed del
módulo Tests de Salud: la batería inicial ANTARES (9 instrumentos) fiel a
`antares-paciente/src/data/tests.ts`, con preguntas, opciones, rangos,
indicadores y reglas de alerta.

Reglas:
- Idempotente: todos los INSERT usan ON CONFLICT DO NOTHING sobre la clave
  única de negocio (code de instrumento/versión/pregunta/indicador/regla).
- Los IDs de pregunta/opción/rango se derivan de manera determinista
  (UUID v5) para que el SQL sea estable entre ejecuciones y re-aplicable.
- El seed referencia versiones de instrumentos por (instrument_id, version_number)
  y asigna a la batería la versión activa al publicar.

Ejecutar desde coppAddresdBack/scripts con:
  python generate_health_tests_seed.py
"""

import re
import uuid
from pathlib import Path

OUT = (
    Path(__file__).resolve().parent.parent
    / "src"
    / "CoppAddresd.Infrastructure"
    / "Migrations"
    / "Seed"
    / "AddHealthTestsCatalogSeed.sql"
)

NS = uuid.UUID("6f3e9f2a-1c4b-4d7e-8a0b-3c5d9e2f1a00")


def v5(name: str) -> str:
    return str(uuid.uuid5(NS, name))


def q(s: str) -> str:
    """Escapa una cadena para SQL (comillas simples duplicadas)."""
    return s.replace("'", "''")


# (code, nombre, descripción, categoría, sort_order, points, scoring_strategy)
INSTRUMENTS = [
    (
        "historia-clinica",
        "Historia clínica biológica",
        "Antecedentes · Examen físico · Sistemas",
        "clinico",
        1,
        50,
        "inventory",
    ),
    (
        "temperamento",
        "Test de temperamento",
        "Sanguíneo · Colérico · Melancólico · Flemático",
        "psicologico",
        2,
        40,
        "subscale",
    ),
    (
        "nutricional",
        "Test nutricional y hábitos",
        "Alimentación · Conducta · Motivación",
        "nutricion",
        3,
        40,
        "subscale",
    ),
    (
        "movimiento",
        "Movimiento y actividad física",
        "AMAF · Nivel funcional · Capacidad",
        "movimiento",
        4,
        40,
        "subscale",
    ),
    (
        "sueno",
        "Caracterización del sueño",
        "Duración · Calidad · Hábitos · Riesgos",
        "sueno",
        5,
        40,
        "subscale",
    ),
    (
        "iac-adresd",
        "Índice de adherencia IAC-ADRESD",
        "Motivación · Autoeficacia · Compromiso",
        "adherencia",
        6,
        40,
        "weighted",
    ),
    (
        "orp",
        "Riesgo cardiometabólico ORP",
        "OMS · Obesidad · Complicaciones · Riesgo",
        "clinico",
        7,
        40,
        "weighted",
    ),
    (
        "ers",
        "Test de estrés relacional ERS",
        "Familia · Pareja · Trabajo · Entorno social",
        "psicologico",
        8,
        40,
        "subscale",
    ),
    (
        "bateria-antares",
        "Batería inicial completa ANTARES",
        "PHS · Propósito · Mentalidad · Perfil final",
        "integral",
        9,
        50,
        "subscale",
    ),
]

# code del instrumento → (sección, pregunta) por tipo de escala
# Escalas: 1-5 (temp/nutri/antares), 0-3 (mov/orp), 0-4 (sueno/iac/ers)
SCALE_QUESTIONS = {
    "temperamento": [
        (
            "🔴 Sanguíneo",
            "Me resulta fácil iniciar conversación con personas que no conozco.",
        ),
        (
            "🔴 Sanguíneo",
            "Disfruto estar rodeado de personas y participar en actividades sociales.",
        ),
        ("🔴 Sanguíneo", "Expreso fácilmente mis emociones."),
        (
            "🔴 Sanguíneo",
            "Suelo entusiasmarme rápidamente con nuevas ideas o proyectos.",
        ),
        ("🔴 Sanguíneo", "Me gusta contar historias y hacer reír a los demás."),
        ("🔴 Sanguíneo", "Me aburro cuando algo se vuelve demasiado rutinario."),
        ("🔴 Sanguíneo", "Suelo actuar espontáneamente."),
        ("🔴 Sanguíneo", "Cuando estoy motivado, contagio mi entusiasmo a otros."),
        (
            "🟠 Colérico",
            "Cuando tengo un objetivo, hago todo lo posible por alcanzarlo.",
        ),
        ("🟠 Colérico", "Me gusta tomar decisiones y asumir el liderazgo."),
        ("🟠 Colérico", "Me impaciento cuando las cosas avanzan muy lentamente."),
        ("🟠 Colérico", "Suelo decir directamente lo que pienso."),
        ("🟠 Colérico", "Los obstáculos me hacen esforzarme todavía más."),
        ("🟠 Colérico", "Me gusta competir y superar desafíos."),
        ("🟠 Colérico", "Prefiero actuar antes que quedarme analizando."),
        ("🟠 Colérico", "Cuando algo no funciona, busco rápidamente una solución."),
        (
            "🔵 Melancólico",
            "Antes de decidir, analizo cuidadosamente las posibilidades.",
        ),
        ("🔵 Melancólico", "Me fijo mucho en los detalles."),
        ("🔵 Melancólico", "Me preocupa hacer las cosas correctamente."),
        ("🔵 Melancólico", "Soy exigente conmigo mismo/a."),
        (
            "🔵 Melancólico",
            "Recuerdo con facilidad situaciones que me afectaron emocionalmente.",
        ),
        (
            "🔵 Melancólico",
            "Necesito comprender profundamente cómo y por qué funcionan las cosas.",
        ),
        ("🔵 Melancólico", "Me molesta cometer errores que podrían haberse evitado."),
        ("🔵 Melancólico", "Suelo pensar mucho antes de actuar."),
        ("🟢 Flemático", "Mantengo la calma incluso cuando otros están alterados."),
        ("🟢 Flemático", "Prefiero evitar discusiones y conflictos innecesarios."),
        ("🟢 Flemático", "Soy paciente con otras personas."),
        ("🟢 Flemático", "Me adapto bien a diferentes personalidades."),
        ("🟢 Flemático", "Valoro la estabilidad y la tranquilidad."),
        ("🟢 Flemático", "Prefiero escuchar antes que hablar."),
        (
            "🟢 Flemático",
            "Las situaciones de presión rara vez me hacen perder el control.",
        ),
        ("🟢 Flemático", "Las personas acuden a mí cuando necesitan ser escuchadas."),
    ],
    "nutricional": [
        (
            "🥦 Calidad alimentaria",
            "Incluyo verduras u hortalizas en mis comidas principales.",
        ),
        ("🥦 Calidad alimentaria", "Consumo frutas regularmente."),
        (
            "🥦 Calidad alimentaria",
            "Consumo fuentes de proteína de buena calidad diariamente.",
        ),
        (
            "🥦 Calidad alimentaria",
            "Consumo alimentos ricos en fibra (legumbres, cereales integrales).",
        ),
        (
            "🥦 Calidad alimentaria",
            "Prefiero alimentos mínimamente procesados frente a ultraprocesados.",
        ),
        (
            "⏰ Organización",
            "Mantengo horarios relativamente regulares para mis comidas.",
        ),
        ("⏰ Organización", "Planifico con anticipación lo que voy a comer."),
        (
            "⏰ Organización",
            "Como sentado/a y presto atención a lo que estoy comiendo.",
        ),
        ("⏰ Organización", "Puedo reconocer cuándo estoy satisfecho/a y detenerme."),
        (
            "⚠ Conductas de riesgo",
            "Consumo bebidas azucaradas o snacks ultraprocesados con frecuencia.",
        ),
        (
            "⚠ Conductas de riesgo",
            "Cuando estoy estresado/a o triste, aumento mi consumo de comida.",
        ),
        (
            "⚠ Conductas de riesgo",
            "Tengo dificultad para controlar porciones de ciertos alimentos.",
        ),
        ("💧 Hidratación", "Bebo agua regularmente durante el día."),
        (
            "🏠 Entorno",
            "En mi casa hay alimentos que facilitan una alimentación saludable.",
        ),
        (
            "🎯 Motivación",
            "Tengo una razón personal importante para mejorar mi alimentación.",
        ),
        (
            "🎯 Motivación",
            "Estoy dispuesto/a a modificar algunos hábitos alimentarios.",
        ),
        ("🎯 Motivación", "Cuando fracaso en un hábito, puedo volver a intentarlo."),
    ],
    "movimiento": [
        ("🟣 Movilidad", "Puedo cambiar de posición en la cama."),
        ("🟣 Movilidad", "Puedo sentarme en la cama con o sin ayuda."),
        ("🔵 Sedestación", "Puedo permanecer sentado/a durante 5 minutos."),
        ("🔵 Sedestación", "Puedo incorporarme de una silla con seguridad."),
        ("🟢 Bipedestación", "Puedo permanecer de pie durante 1 minuto."),
        ("🟢 Bipedestación", "Puedo mantener el equilibrio estando de pie."),
        ("🟡 Marcha", "Puedo caminar dentro de mi vivienda."),
        ("🟡 Marcha", "Puedo caminar durante 5 minutos sin detenerme."),
        ("🟡 Marcha", "Puedo caminar fuera de casa."),
        ("🟠 Capacidad funcional", "Puedo realizar actividades domésticas básicas."),
        ("🔴 Autopercepción", "Tengo confianza para moverme."),
        (
            "🔴 Autopercepción",
            "Estoy dispuesto/a a realizar actividad física diariamente.",
        ),
    ],
    "sueno": [
        (
            "⏰ Regularidad",
            "Me acuesto aproximadamente a la misma hora todos los días.",
        ),
        (
            "⏰ Regularidad",
            "Me levanto aproximadamente a la misma hora todos los días.",
        ),
        ("🛏 Duración", "Duermo el tiempo que necesito para sentirme descansado/a."),
        ("🛏 Duración", "Me duermo con relativa facilidad."),
        ("🌅 Funcionamiento diurno", "Me despierto sintiéndome descansado/a."),
        ("🌅 Funcionamiento diurno", "Tengo energía suficiente durante el día."),
        ("📱 Hábitos", "Evito el teléfono inmediatamente antes de dormir."),
        ("📱 Hábitos", "Evito la cafeína varias horas antes de acostarme."),
        (
            "🧠 Desconexión",
            "Puedo dejar de pensar en mis responsabilidades a la hora de dormir.",
        ),
    ],
    "iac-adresd": [
        ("🧠 Motivación", "Tengo una razón personal importante para mejorar mi salud."),
        ("🧠 Motivación", "Mejorar mi salud es actualmente una prioridad para mí."),
        (
            "💪 Autoeficacia",
            "Creo que soy capaz de cambiar hábitos que afectan mi salud.",
        ),
        (
            "💪 Autoeficacia",
            "Cuando tengo una recaída, soy capaz de volver a comenzar.",
        ),
        (
            "⏰ Organización",
            "Puedo reservar tiempo de manera regular para cuidar mi salud.",
        ),
        (
            "🥗 Hábitos",
            "Estoy dispuesto/a a realizar cambios progresivos en mi alimentación.",
        ),
        (
            "🥗 Hábitos",
            "Estoy dispuesto/a a aumentar progresivamente mi actividad física.",
        ),
        (
            "👥 Apoyo",
            "Tengo al menos una persona que puede apoyarme durante este proceso.",
        ),
        ("🔄 Resiliencia", "Entiendo que una recaída no significa que haya fracasado."),
        (
            "📲 Compromiso",
            "Estoy dispuesto/a a informar honestamente mis avances y dificultades.",
        ),
    ],
    "orp": [
        ("🫀 Cardiometabólico", "¿Tiene diagnóstico de hipertensión arterial?"),
        ("🫀 Cardiometabólico", "¿Tiene diagnóstico de diabetes tipo 2 o prediabetes?"),
        ("🫀 Cardiometabólico", "¿Tiene colesterol o triglicéridos elevados?"),
        ("🫀 Cardiometabólico", "¿Tiene enfermedad cardiovascular, coronaria o ACV?"),
        ("🫀 Cardiometabólico", "¿Le han informado que tiene hígado graso?"),
        ("🫁 Sueño", "¿Ronca habitualmente o muy fuerte?"),
        ("🫁 Sueño", "¿Se despierta cansado/a aunque haya dormido varias horas?"),
        (
            "🦴 Músculo-esquelético",
            "¿Tiene dolor frecuente en rodillas, caderas o columna?",
        ),
        ("🧠 Conductual", "¿Utiliza la comida frecuentemente para manejar estrés?"),
        ("🧠 Conductual", "¿Ha realizado múltiples dietas y recuperado el peso?"),
    ],
    "ers": [
        (
            "👨‍👩‍👧 Familia",
            "Siento que dentro de mi familia tengo conflictos que me generan estrés.",
        ),
        (
            "👨‍👩‍👧 Familia",
            "Los problemas familiares interfieren con mi descanso o sueño.",
        ),
        (
            "❤️ Pareja",
            "Mi relación de pareja genera situaciones que me producen estrés.",
        ),
        (
            "❤️ Pareja",
            "Los conflictos de pareja afectan mi sueño, alimentación o bienestar.",
        ),
        ("💼 Laboral", "Mi trabajo me genera un nivel importante de estrés."),
        (
            "💼 Laboral",
            "Tengo dificultades para desconectarme mentalmente del trabajo.",
        ),
        ("🧑‍🤝‍🧑 Social", 'Me cuesta decir "no" cuando alguien me pide algo.'),
        (
            "🧠 Impacto",
            "Cuando tengo conflictos, aumenta mi necesidad de comer o picar.",
        ),
        (
            "🧠 Impacto",
            "El estrés de mis relaciones está afectando mi calidad de vida.",
        ),
    ],
    "bateria-antares": [
        (
            "🧠 Mentalidad de salud",
            "Mi estado de salud depende principalmente de mis propias acciones.",
        ),
        (
            "🧠 Mentalidad de salud",
            "Cuando fracaso en un hábito, me recupero sin abandonar el proceso.",
        ),
        (
            "🧠 Mentalidad de salud",
            "Creo que cambios pequeños y constantes producen resultados importantes.",
        ),
        (
            "🧠 Mentalidad de salud",
            "Me veo capaz de construir una versión más saludable de mí mismo/a.",
        ),
    ],
}

# code → (min_scale, max_scale) para construir las opciones con score_value
SCALES = {
    "temperamento": (1, 5),
    "nutricional": (1, 5),
    "movimiento": (0, 3),
    "sueno": (0, 4),
    "iac-adresd": (0, 4),
    "orp": (0, 3),
    "ers": (0, 4),
    "bateria-antares": (1, 5),
}

# Items en negativo (reverse scoring) por instrumento: índices de pregunta (0-based).
REVERSE_QUESTIONS = {
    "nutricional": [9, 10, 11],  # Conductas de riesgo
}

# (code, nombre, condición jsonb, severity, message_template)
ALERT_RULES = [
    (
        "orp_riesgo_alto",
        "Riesgo cardiometabólico alto",
        '{"when":{"resultType":"score","code":"orp","severity":["high","critical"]}}',
        "high",
        "ORP {value} - {label}: riesgo cardiometabólico elevado. Revisar prioridad.",
    ),
    (
        "iapnea_sospecha",
        "Sospecha de apnea del sueño",
        '{"when":{"resultType":"indicator","code":"iapnea","severity":["high"]}}',
        "high",
        "Sospecha de apnea: {value} señales de riesgo. Evaluar estudio de sueño.",
    ),
    (
        "adherencia_baja",
        "Adherencia baja al programa",
        '{"when":{"resultType":"indicator","code":"iadherencia","severity":["high"]}}',
        "moderate",
        "Adherencia IAC-ADRESD baja ({value}%). Requiere acompañamiento.",
    ),
]

# (code, nombre, computation jsonb)
INDICATORS = [
    (
        "iadherencia",
        "Índice de adherencia IAC-ADRESD",
        '{"formula":"weighted","sources":[{"resultType":"subscale","code":"🧠 Motivación"},{"resultType":"subscale","code":"💪 Autoeficacia"},{"resultType":"subscale","code":"⏰ Organización"},{"resultType":"subscale","code":"🥗 Hábitos"},{"resultType":"subscale","code":"👥 Apoyo"},{"resultType":"subscale","code":"🔄 Resiliencia"},{"resultType":"subscale","code":"📲 Compromiso"}]}',
    ),
    (
        "iapnea",
        "Sospecha de apnea del sueño",
        '{"formula":"sum","sources":[{"resultType":"subscale","code":"🫁 Sueño"}]}',
    ),
]

# Rangos por instrumento: (label, min, max, severity)
RANGES = {
    "temperamento": [
        ("bajo", 32, 63, "low"),
        ("moderado", 64, 95, "moderate"),
        ("alto", 96, 128, "high"),
    ],
    "nutricional": [
        ("bajo", 17, 33, "low"),
        ("moderado", 34, 50, "moderate"),
        ("alto", 51, 68, "high"),
    ],
    "movimiento": [
        ("bajo", 0, 12, "low"),
        ("moderado", 13, 24, "moderate"),
        ("alto", 25, 36, "high"),
    ],
    "sueno": [
        ("bajo", 0, 12, "low"),
        ("moderado", 13, 24, "moderate"),
        ("alto", 25, 36, "high"),
    ],
    "iac-adresd": [
        ("alto", 0, 39, "high"),
        ("moderado", 40, 69, "moderate"),
        ("bajo", 70, 100, "low"),
    ],
    "orp": [
        ("bajo", 0, 9, "low"),
        ("moderado", 10, 17, "moderate"),
        ("alto", 18, 24, "high"),
        ("critico", 25, 30, "critical"),
    ],
    "ers": [
        ("bajo", 0, 9, "low"),
        ("moderado", 10, 18, "moderate"),
        ("alto", 19, 27, "high"),
        ("critico", 28, 36, "critical"),
    ],
    "bateria-antares": [
        ("bajo", 4, 9, "low"),
        ("moderado", 10, 14, "moderate"),
        ("alto", 15, 20, "high"),
    ],
    "historia-clinica": [
        ("bajo", 0, 2, "low"),
        ("moderado", 3, 5, "moderate"),
        ("alto", 6, 99, "high"),
    ],
}


def build() -> str:
    lines = []
    lines.append("-- Seed del módulo Tests de Salud: batería inicial ANTARES.")
    lines.append("-- Idempotente (ON CONFLICT DO NOTHING sobre claves únicas).")
    lines.append(
        "-- Generado por scripts/generate_health_tests_seed.py — editar el script, no este SQL."
    )
    lines.append("")

    # 1) Instrumentos
    lines.append(
        "INSERT INTO app.health_test_instruments (id, code, name, description, category, sort_order, is_active, created_at)"
    )
    lines.append("SELECT * FROM (VALUES")
    for i, (code, name, desc, cat, order_, _, _) in enumerate(INSTRUMENTS):
        suffix = "," if i < len(INSTRUMENTS) - 1 else ""
        lines.append(
            f"    ('{v5('inst:' + code)}', '{code}', {q(name)!r}, {q(desc)!r}, '{cat}', {order_}, true, now()){suffix}"
        )
    lines.append(
        ") AS t(id, code, name, description, category, sort_order, is_active, created_at)"
    )
    lines.append("ON CONFLICT (code) DO NOTHING;")
    lines.append("")

    # 2) Versiones (v1 active por cada instrumento)
    lines.append(
        "INSERT INTO app.health_test_versions (id, instrument_id, version_number, status, is_current, scoring_strategy, points, created_at, published_at)"
    )
    lines.append("SELECT v.id, i.id, v.vn, 'active', true, v.ss, v.pts, now(), now()")
    lines.append("FROM (VALUES")
    for i, (code, _, _, _, _, pts, ss) in enumerate(INSTRUMENTS):
        suffix = "," if i < len(INSTRUMENTS) - 1 else ""
        lines.append(
            f"    ('{v5('ver:' + code + ':1')}', '{code}', 1, '{ss}', {pts}){suffix}"
        )
    lines.append(") AS v(id, code, vn, ss, pts)")
    lines.append("JOIN app.health_test_instruments i ON i.code = v.code")
    lines.append("ON CONFLICT (instrument_id, version_number) DO NOTHING;")
    lines.append("")

    # 3) Preguntas (scale) con opciones por escala
    lines.append("-- Preguntas de escala con sus opciones.")
    for code, questions in SCALE_QUESTIONS.items():
        min_s, max_s = SCALES[code]
        for idx, (section, text) in enumerate(questions):
            qid = v5(f"q:{code}:{idx}")
            is_reverse = idx in REVERSE_QUESTIONS.get(code, [])
            direction = "reverse" if is_reverse else "positive"
            lines.append(
                f"INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)"
            )
            lines.append(
                f"SELECT '{qid}', v.id, '{code}_{idx:02d}', {q(section)!r}, {q(text)!r}, 'scale', '{direction}', {idx}, true"
            )
            lines.append(
                f"FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id"
            )
            lines.append(
                f"WHERE i.code = '{code}' AND v.version_number = 1 AND v.is_current = true"
            )
            lines.append(f"ON CONFLICT (version_id, code) DO NOTHING;")
            for opt_idx in range(min_s, max_s + 1):
                oid = v5(f"o:{code}:{idx}:{opt_idx}")
                lines.append(
                    f"INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)"
                )
                lines.append(
                    f"SELECT '{oid}', q.id, '{opt_idx}', {opt_idx}, {opt_idx - min_s}, true"
                )
                lines.append(
                    f"FROM app.health_test_questions q WHERE q.code = '{code}_{idx:02d}' AND q.is_active = true"
                )
                lines.append(f"ON CONFLICT DO NOTHING;")
            lines.append("")
    lines.append("")

    # 4) Historia clínica: bloques multi-select (antecedentes, familia, sistemas)
    HIST_MULTI = {
        "antecedentes": [
            "Diabetes tipo 2",
            "Prediabetes",
            "Hipertensión",
            "Enf. cardiovascular",
            "Colesterol alto",
            "Triglicéridos altos",
            "Hígado graso",
            "Asma / EPOC",
            "Artrosis",
            "Depresión / Ansiedad",
            "Tiroides",
            "Ninguno",
        ],
        "familia": [
            "Diabetes",
            "Enf. coronaria",
            "HTA",
            "ACV",
            "Cáncer",
            "Ninguno conocido",
        ],
        "cabeza_cuello": [
            "Cefalea frecuente",
            "Mareos o vértigo",
            "Visión borrosa",
            "Tinnitus",
            "Sinusitis",
            "Disfagia",
        ],
        "cardiorrespiratorio": [
            "Palpitaciones",
            "Dolor de pecho",
            "Disnea al esfuerzo",
            "Edema en piernas",
            "Tos persistente",
            "Apnea nocturna",
        ],
        "digestivo": [
            "Reflujo",
            "Náuseas",
            "Dolor abdominal",
            "Cambio de hábito intestinal",
            "Distensión",
            "Pérdida de peso",
        ],
        "osteomuscular": [
            "Dolor articular",
            "Dolor lumbar",
            "Rigidez matutina",
            "Limitación de movimiento",
        ],
        "psicologico": [
            "Tristeza persistente",
            "Ansiedad",
            "Insomnio",
            "Irritabilidad",
            "Baja motivación",
            "Estrés elevado",
        ],
    }
    lines.append("-- Historia clínica: bloques multi-select (inventario).")
    for block_idx, (block, options) in enumerate(HIST_MULTI.items()):
        section_label = {
            "antecedentes": "Antecedentes",
            "familia": "Familia",
            "cabeza_cuello": "Cabeza y cuello",
            "cardiorrespiratorio": "Cardiorrespiratorio",
            "digestivo": "Digestivo",
            "osteomuscular": "Osteomuscular",
            "psicologico": "Psicológico",
        }[block]
        qid = v5(f"q:historia-clinica:multi:{block}")
        lines.append(
            f"INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)"
        )
        lines.append(
            f"SELECT '{qid}', v.id, 'hist_{block}', {q(section_label)!r}, {q(section_label)!r}, 'multi', 'positive', {block_idx}, true"
        )
        lines.append(
            f"FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id"
        )
        lines.append(
            f"WHERE i.code = 'historia-clinica' AND v.version_number = 1 AND v.is_current = true"
        )
        lines.append(f"ON CONFLICT (version_id, code) DO NOTHING;")
        for opt_idx, opt_text in enumerate(options):
            oid = v5(f"o:hist:{block}:{opt_idx}")
            lines.append(
                f"INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)"
            )
            lines.append(f"SELECT '{oid}', q.id, {q(opt_text)!r}, 1, {opt_idx}, true")
            lines.append(
                f"FROM app.health_test_questions q WHERE q.code = 'hist_{block}' AND q.is_active = true"
            )
            lines.append(f"ON CONFLICT DO NOTHING;")
        lines.append("")
    lines.append("")

    # 5) Preguntas abiertas de la batería ANTARES (propósito)
    OPEN_QUESTIONS = [
        "¿Para qué quieres estar saludable?",
        "¿Qué quieres poder hacer dentro de 5 años que hoy se te dificulta?",
        "¿Qué perderías si tu salud empeorara?",
    ]
    lines.append("-- Batería ANTARES: preguntas abiertas de propósito.")
    for idx, text in enumerate(OPEN_QUESTIONS):
        qid = v5(f"q:bateria-antares:open:{idx}")
        lines.append(
            f"INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)"
        )
        lines.append(
            f"SELECT '{qid}', v.id, 'antares_open_{idx}', '🎯 Propósito', {q(text)!r}, 'open', 'positive', {100 + idx}, true"
        )
        lines.append(
            f"FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id"
        )
        lines.append(
            f"WHERE i.code = 'bateria-antares' AND v.version_number = 1 AND v.is_current = true"
        )
        lines.append(f"ON CONFLICT (version_id, code) DO NOTHING;")
    lines.append("")

    # 6) Rangos de interpretación
    lines.append("-- Rangos de interpretación por versión.")
    for code, ranges in RANGES.items():
        for idx, (label, mn, mx, sev) in enumerate(ranges):
            rid = v5(f"range:{code}:{idx}")
            lines.append(
                f"INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)"
            )
            lines.append(
                f"SELECT '{rid}', v.id, {mn}, {mx}, {q(label)!r}, '{sev}', true"
            )
            lines.append(
                f"FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id"
            )
            lines.append(
                f"WHERE i.code = '{code}' AND v.version_number = 1 AND v.is_current = true"
            )
            lines.append(f"ON CONFLICT DO NOTHING;")
    lines.append("")

    # 7) Batería inicial
    lines.append(
        "INSERT INTO app.health_test_batteries (id, code, name, description, auto_assign_on_patient_create, is_active, created_at)"
    )
    lines.append(
        f"VALUES ('{v5('battery:bateria-inicial')}', 'bateria-inicial', 'Batería de evaluación inicial ANTARES', 'Evaluación inicial del programa (9 tests del onboarding)', true, true, now())"
    )
    lines.append("ON CONFLICT (code) DO NOTHING;")
    lines.append("")
    lines.append(
        "INSERT INTO app.health_test_battery_items (id, battery_id, instrument_id, version_id, sort_order, is_required, frequency_days)"
    )
    lines.append("SELECT gen_random_uuid(), b.id, i.id, v.id, b_item.pos, true, NULL")
    lines.append("FROM (VALUES")
    for i, (code, _, _, _, order_, _, _) in enumerate(INSTRUMENTS):
        suffix = "," if i < len(INSTRUMENTS) - 1 else ""
        lines.append(f"    ('{code}', {order_}){suffix}")
    lines.append(") AS b_item(code, pos)")
    lines.append("JOIN app.health_test_batteries b ON b.code = 'bateria-inicial'")
    lines.append("JOIN app.health_test_instruments i ON i.code = b_item.code")
    lines.append(
        "JOIN app.health_test_versions v ON v.instrument_id = i.id AND v.is_current = true"
    )
    lines.append(
        "WHERE NOT EXISTS (SELECT 1 FROM app.health_test_battery_items bi WHERE bi.battery_id = b.id AND bi.instrument_id = i.id)"
    )
    lines.append("ON CONFLICT DO NOTHING;")
    lines.append("")

    # 8) Indicadores
    lines.append(
        "INSERT INTO app.health_test_indicator_defs (id, code, name, description, computation, is_active, created_at)"
    )
    lines.append("SELECT * FROM (VALUES")
    for i, (code, name, computation) in enumerate(INDICATORS):
        suffix = "," if i < len(INDICATORS) - 1 else ""
        desc = f"Indicador derivado: {name}"
        lines.append(
            f"    ('{v5('ind:' + code)}', '{code}', {q(name)!r}, {q(desc)!r}, {computation!r}, true, now()){suffix}"
        )
    lines.append(
        ") AS t(id, code, name, description, computation, is_active, created_at)"
    )
    lines.append("ON CONFLICT (code) DO NOTHING;")
    lines.append("")

    # 9) Reglas de alerta
    lines.append(
        "INSERT INTO app.health_test_alert_rules (id, code, name, condition, severity, message_template, is_active, created_at)"
    )
    lines.append("SELECT * FROM (VALUES")
    for i, (code, name, condition, sev, template) in enumerate(ALERT_RULES):
        suffix = "," if i < len(ALERT_RULES) - 1 else ""
        lines.append(
            f"    ('{v5('rule:' + code)}', '{code}', {q(name)!r}, {condition!r}, '{sev}', {q(template)!r}, true, now()){suffix}"
        )
    lines.append(
        ") AS t(id, code, name, condition, severity, message_template, is_active, created_at)"
    )
    lines.append("ON CONFLICT (code) DO NOTHING;")
    lines.append("")

    return "\n".join(lines)


def main() -> None:
    OUT.parent.mkdir(parents=True, exist_ok=True)
    content = build()
    # Castea los literales UUID a uuid (Postgres no infiere el tipo en VALUES/SELECT).
    content = re.sub(
        r"'([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})'",
        r"'\1'::uuid",
        content,
    )
    # Castea los literales JSON (computation/condition) a jsonb.
    content = re.sub(
        r"'(\{.*?\})'",
        r"'\1'::jsonb",
        content,
        flags=re.DOTALL,
    )
    OUT.write_text(content, encoding="utf-8")
    print(f"Seed escrito en {OUT} ({len(content)} bytes)")


if __name__ == "__main__":
    main()
