"""Genera Migrations/Seed/AddHealthTestsCatalogSeed.sql con el seed del
módulo Tests de Salud: la batería inicial ANTARES (9 instrumentos) con el
contenido EXACTO de `ANTARES_Tests_Perfil_Salud (1).html` (preguntas,
opciones, secciones, hints, unidad/rango de biometría), tal como se
verificó con la app móvil.

Reglas:
- Idempotente: todos los INSERT usan ON CONFLICT DO NOTHING sobre la clave
  única de negocio (code de instrumento/versión/pregunta/indicador/regla).
- Los IDs de pregunta/opción/rango se derivan de manera determinista
  (UUID v5) para que el SQL sea estable entre ejecuciones y re-aplicable.
- El seed referencia versiones de instrumentos por (instrument_id, version_number)
  y asigna a la batería la versión activa al publicar.
- Tipos de pregunta: scale/single/multi/open/num (num = biometría con
  unidad/rango; no puntúa: su respuesta viaja como value_text).

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
        "Historia clínica",
        "Antecedentes, medicamentos y cómo te sientes físicamente.",
        "clinico",
        1,
        50,
        "inventory",
    ),
    (
        "temperamento",
        "Temperamento",
        "Tu personalidad determina cómo te acompañamos en el programa.",
        "psicologico",
        2,
        40,
        "subscale",
    ),
    (
        "nutricional",
        "Nutrición",
        "Tus hábitos alimentarios y tu relación con la comida.",
        "nutricion",
        3,
        40,
        "subscale",
    ),
    (
        "movimiento",
        "Movimiento · AMAF",
        "Tu capacidad física actual determina el circuito que te asignamos.",
        "movimiento",
        4,
        40,
        "subscale",
    ),
    (
        "sueno",
        "Sueño",
        "El sueño impacta directamente tu glucosa, tu peso y tu adherencia.",
        "sueno",
        5,
        40,
        "subscale",
    ),
    (
        "iac-adresd",
        "Adherencia · IAC",
        "Tu motivación real determina cómo te acompañamos.",
        "adherencia",
        6,
        40,
        "weighted",
    ),
    (
        "orp",
        "Riesgo cardiometabólico ORP",
        "Información clínica confidencial — solo la ve tu equipo médico.",
        "clinico",
        7,
        40,
        "weighted",
    ),
    (
        "ers",
        "Estrés relacional · ERS",
        "El estrés en casa o en el trabajo es la barrera #1 de la adherencia.",
        "psicologico",
        8,
        40,
        "subscale",
    ),
    (
        "bateria-antares",
        "Propósito · ANTARES",
        "Las respuestas más importantes del programa. Sé completamente honesto/a.",
        "integral",
        9,
        50,
        "subscale",
    ),
]

# Escalas Likert: el render siempre es 1..5 (score_value 0..4).
SCALE_OPTIONS = [(str(i), i - 1) for i in range(1, 6)]


def single_options(*labels: str):
    """Opciones de selección única: score_value 0..n-1 en el orden dado."""
    return [(lbl, i) for i, lbl in enumerate(labels)]


def multi_options(*labels: str):
    """Opciones multi (inventario): score 1 por señal; 0 para las de negación."""
    return [
        (lbl, 0 if any(k in lbl.lower() for k in ["ningun", "bien"]) else 1)
        for lbl in labels
    ]


# code del instrumento → preguntas:
# (code, section, text, type, direction, hint, unit, min, max, default,
#  min_label, max_label, options[(text, score)])
QUESTIONS = {
    "historia-clinica": [
        (
            "dx",
            "Diagnósticos actuales",
            "¿Cuál de estos diagnósticos tienes actualmente?",
            "multi",
            "positive",
            "Marca todos los que aplican.",
            None,
            None,
            None,
            None,
            None,
            None,
            multi_options(
                "Diabetes tipo 2",
                "Obesidad",
                "Hipertensión",
                "Prediabetes",
                "Colesterol alto",
                "Ninguno por ahora",
            ),
        ),
        (
            "med",
            "Medicamentos",
            "¿Tomas medicamentos actualmente?",
            "single",
            "positive",
            None,
            None,
            None,
            None,
            None,
            None,
            None,
            single_options("Sí, con receta médica", "Solo suplementos", "No tomo nada"),
        ),
        (
            "sexo",
            "Datos personales · Sexo biológico",
            "¿Cuál es tu sexo biológico?",
            "single",
            "positive",
            "Necesario para calcular tu % de grasa corporal con mayor precisión.",
            None,
            None,
            None,
            None,
            None,
            None,
            single_options("Masculino", "Femenino"),
        ),
        (
            "edad",
            "Datos personales · Edad",
            "¿Cuántos años tienes?",
            "num",
            "positive",
            None,
            "años",
            18,
            90,
            40,
            None,
            None,
            [],
        ),
        (
            "peso",
            "Biometría · Peso corporal",
            "¿Cuánto pesas? Si tienes báscula en casa, pésate ahora.",
            "num",
            "positive",
            "Párate en la báscula con ropa ligera, sin zapatos. Si no tienes, pon un aproximado.",
            "kg",
            40,
            220,
            75,
            None,
            None,
            [],
        ),
        (
            "talla",
            "Biometría · Estatura",
            "¿Cuánto mides de estatura?",
            "num",
            "positive",
            "Párate derecho/a contra una pared y marca con un libro. Mide hasta el suelo.",
            "cm",
            140,
            210,
            165,
            None,
            None,
            [],
        ),
        (
            "cintura",
            "Biometría · Cintura",
            "¿Cuánto mide tu cintura al nivel del ombligo?",
            "num",
            "positive",
            "Con una cinta métrica (o tira de papel que luego mides), rodea tu abdomen a la altura del ombligo al exhalar.",
            "cm",
            50,
            180,
            90,
            None,
            None,
            [],
        ),
        (
            "cadera",
            "Biometría · Cadera",
            "¿Cuánto mide la parte más ancha de tu cadera?",
            "num",
            "positive",
            "Mide alrededor de la parte más amplia de tus caderas y glúteos.",
            "cm",
            60,
            200,
            100,
            None,
            None,
            [],
        ),
        (
            "muneca",
            "Biometría · Muñeca",
            "¿Cuánto mide tu muñeca?",
            "num",
            "positive",
            "Rodea tu muñeca dominante con la cinta por debajo de los huesos del puño. Este dato ayuda a calcular tu % de grasa.",
            "cm",
            12,
            25,
            17,
            None,
            None,
            [],
        ),
        (
            "gluc_ayunas",
            "Glucosa · Última medición en ayunas",
            "¿Tienes glucómetro en casa o algún resultado reciente de glucosa en ayunas?",
            "single",
            "positive",
            "Ayunas = sin haber comido en las últimas 8 horas.",
            None,
            None,
            None,
            None,
            None,
            None,
            single_options(
                "Sí, está en rango (< 100 mg/dL)",
                "Sí, entre 100 y 125 mg/dL",
                "Sí, 126 mg/dL o más",
                "No sé o no me la he medido",
            ),
        ),
        (
            "gluc_sintomas",
            "Glucosa · Señales del cuerpo",
            "¿Cuáles de estas señales reconoces en tu cuerpo con frecuencia?",
            "multi",
            "positive",
            "Estas señales ayudan a estimar si tu glucosa puede estar elevada.",
            None,
            None,
            None,
            None,
            None,
            None,
            multi_options(
                "Sed intensa todo el día",
                "Orinas muchas veces al día",
                "Cansancio después de comer",
                "Antojos intensos de dulce",
                "Vista borrosa ocasional",
                "No tengo ninguna de estas",
            ),
        ),
        (
            "sintomas",
            "Síntomas frecuentes generales",
            "Además de lo anterior, ¿tienes alguno de estos síntomas con regularidad?",
            "multi",
            "positive",
            "Marca todos los que aplican.",
            None,
            None,
            None,
            None,
            None,
            None,
            multi_options(
                "Hormigueo en pies o manos",
                "Heridas que tardan en sanar",
                "Dificultad para respirar",
                "Me siento bien en general",
            ),
        ),
        (
            "antfam",
            "Antecedentes familiares",
            "¿Algún familiar directo tiene o tuvo alguna de estas condiciones?",
            "multi",
            "positive",
            "Padres, hermanos, abuelos.",
            None,
            None,
            None,
            None,
            None,
            None,
            multi_options(
                "Diabetes",
                "Hipertensión",
                "Infarto o ACV",
                "Obesidad",
                "Sin antecedentes",
            ),
        ),
    ],
    "temperamento": [
        (
            "temp_social",
            "Temperamento · Dimensión social",
            "Me siento bien en grupos grandes y hago amigos con facilidad.",
            "scale",
            "positive",
            None,
            None,
            None,
            None,
            None,
            "Nunca",
            "Siempre",
            SCALE_OPTIONS,
        ),
        (
            "temp_metas",
            "Temperamento · Metas y resultados",
            "Cuando me propongo algo, lo hago sin importar los obstáculos.",
            "scale",
            "positive",
            None,
            None,
            None,
            None,
            None,
            "Nunca",
            "Siempre",
            SCALE_OPTIONS,
        ),
        (
            "temp_analisis",
            "Temperamento · Análisis y detalle",
            "Antes de actuar, analizo bien todas las opciones disponibles.",
            "scale",
            "positive",
            None,
            None,
            None,
            None,
            None,
            "Nunca",
            "Siempre",
            SCALE_OPTIONS,
        ),
        (
            "temp_rutina",
            "Temperamento · Rutina y estabilidad",
            "Prefiero tener rutinas fijas y predecibles en mi vida diaria.",
            "scale",
            "positive",
            None,
            None,
            None,
            None,
            None,
            "Nunca",
            "Siempre",
            SCALE_OPTIONS,
        ),
        (
            "temp_emociones",
            "Temperamento · Reacción emocional",
            "Cuando algo no sale como esperaba, ¿cuál es tu reacción más común?",
            "single",
            "positive",
            None,
            None,
            None,
            None,
            None,
            None,
            None,
            single_options(
                "Me frustro pero sigo intentando",
                "Lo siento mucho y reflexiono",
                "Lo dejo ir y busco algo nuevo",
                "Lo acepto y espero el momento",
            ),
        ),
        (
            "temp_abandono",
            "Temperamento · Constancia",
            "¿Qué es lo más probable que te haga abandonar un programa de salud?",
            "single",
            "positive",
            None,
            None,
            None,
            None,
            None,
            None,
            None,
            single_options(
                "La monotonía y el aburrimiento",
                "No ver resultados rápidos",
                "Tener demasiada información",
                "Cambiar mi rutina habitual",
            ),
        ),
    ],
    "nutricional": [
        (
            "nut_comidas",
            "Hábitos · Frecuencia",
            "¿Cuántas veces al día comes normalmente (incluyendo meriendas)?",
            "single",
            "positive",
            None,
            None,
            None,
            None,
            None,
            None,
            None,
            single_options("1–2 veces", "3 veces", "4–5 veces", "Pico todo el día"),
        ),
        (
            "nut_emocional",
            "Conducta · Alimentación emocional",
            "Como más de lo planeado cuando estoy estresado/a, triste o ansioso/a.",
            "scale",
            "reverse",
            None,
            None,
            None,
            None,
            None,
            "Nunca",
            "Siempre",
            SCALE_OPTIONS,
        ),
        (
            "nut_control",
            "Conducta · Control de porciones",
            "Me cuesta controlar la cantidad de comida que sirvo en mi plato.",
            "scale",
            "reverse",
            None,
            None,
            None,
            None,
            None,
            "Nunca",
            "Siempre",
            SCALE_OPTIONS,
        ),
        (
            "nut_procesados",
            "Hábitos · Ultraprocesados",
            "Como alimentos ultraprocesados (snacks, comida rápida, gaseosas) más de 3 veces por semana.",
            "scale",
            "reverse",
            None,
            None,
            None,
            None,
            None,
            "Nunca",
            "Siempre",
            SCALE_OPTIONS,
        ),
        (
            "nut_agua",
            "Hidratación · Agua diaria",
            "¿Cuánta agua tomas al día aproximadamente?",
            "single",
            "positive",
            None,
            None,
            None,
            None,
            None,
            None,
            None,
            single_options(
                "Menos de 4 vasos", "4–6 vasos", "7–8 vasos", "Más de 8 vasos"
            ),
        ),
        (
            "nut_motivacion",
            "Motivación · Disposición al cambio",
            "Estoy dispuesto/a a cambiar mis hábitos alimenticios si tengo el apoyo adecuado.",
            "scale",
            "positive",
            None,
            None,
            None,
            None,
            None,
            "Para nada",
            "Totalmente",
            SCALE_OPTIONS,
        ),
    ],
    "movimiento": [
        (
            "mov_actual",
            "Actividad · Nivel actual",
            "¿Cómo describes tu nivel de actividad física actualmente?",
            "single",
            "positive",
            None,
            None,
            None,
            None,
            None,
            None,
            None,
            single_options(
                "Sedentario/a — casi no me muevo",
                "Camino un poco (< 30 min/día)",
                "Actividad moderada (30–60 min)",
                "Activo/a (ejercicio regular)",
            ),
        ),
        (
            "mov_fatiga",
            "Capacidad · Resistencia",
            "Me canso fácilmente al subir escaleras o caminar 10 minutos.",
            "scale",
            "reverse",
            None,
            None,
            None,
            None,
            None,
            "Nunca",
            "Siempre",
            SCALE_OPTIONS,
        ),
        (
            "mov_dolor",
            "Limitaciones · Dolor",
            "¿Tienes dolor articular o muscular que limita tu movimiento?",
            "single",
            "positive",
            None,
            None,
            None,
            None,
            None,
            None,
            None,
            single_options(
                "No, me muevo sin dolor",
                "Dolor leve ocasional",
                "Dolor frecuente que me limita",
            ),
        ),
        (
            "mov_tiempo",
            "Disponibilidad · Tiempo",
            "¿Cuánto tiempo puedes dedicar al ejercicio por día?",
            "single",
            "positive",
            None,
            None,
            None,
            None,
            None,
            None,
            None,
            single_options(
                "5–10 minutos", "15–20 minutos", "30–45 minutos", "Más de 45 minutos"
            ),
        ),
    ],
    "sueno": [
        (
            "sue_horas",
            "Sueño · Horas por noche",
            "¿Cuántas horas duermes normalmente por noche?",
            "single",
            "positive",
            None,
            None,
            None,
            None,
            None,
            None,
            None,
            single_options("Menos de 5 horas", "5–6 horas", "6–7 horas", "7–9 horas"),
        ),
        (
            "sue_calidad",
            "Sueño · Calidad",
            "Me despierto descansado/a y con energía para empezar el día.",
            "scale",
            "positive",
            None,
            None,
            None,
            None,
            None,
            "Nunca",
            "Siempre",
            SCALE_OPTIONS,
        ),
        (
            "sue_ronquidos",
            "Red flags · Ronquidos",
            "¿Te han dicho que roncas fuerte o que paras de respirar mientras duermes?",
            "single",
            "positive",
            None,
            None,
            None,
            None,
            None,
            None,
            None,
            single_options("No, nunca", "A veces", "Sí, frecuentemente"),
        ),
        (
            "sue_somnolencia",
            "Red flags · Somnolencia diurna",
            "Me da mucho sueño durante el día, aunque haya dormido mis horas.",
            "scale",
            "reverse",
            None,
            None,
            None,
            None,
            None,
            "Nunca",
            "Siempre",
            SCALE_OPTIONS,
        ),
    ],
    "iac-adresd": [
        (
            "adh_compromiso",
            "Compromiso · Motivación",
            "Estoy dispuesto/a a cambiar mis hábitos aunque sea difícil al principio.",
            "scale",
            "positive",
            None,
            None,
            None,
            None,
            None,
            "Para nada",
            "Totalmente",
            SCALE_OPTIONS,
        ),
        (
            "adh_constancia",
            "Compromiso · Constancia",
            "Cuando empiezo un programa o tratamiento, lo termino aunque sea duro.",
            "scale",
            "positive",
            None,
            None,
            None,
            None,
            None,
            "Nunca",
            "Siempre",
            SCALE_OPTIONS,
        ),
        (
            "adh_barreras",
            "Barreras · Obstáculos reales",
            "¿Qué podría impedirte seguir el programa con constancia?",
            "multi",
            "positive",
            "Sé honesto/a, esto nos ayuda a personalizar tu apoyo.",
            None,
            None,
            None,
            None,
            None,
            None,
            multi_options(
                "Falta de tiempo",
                "Costo económico",
                "Desmotivación",
                "Responsabilidades familiares",
                "Trabajo o estudio",
                "Ninguna — estoy listo/a",
            ),
        ),
        (
            "adh_proposito",
            "Propósito · Razón principal",
            "¿Cuál es tu razón más importante para unirte al programa?",
            "single",
            "positive",
            None,
            None,
            None,
            None,
            None,
            None,
            None,
            single_options(
                "Controlar mi enfermedad",
                "Bajar de peso",
                "Tener más energía",
                "Por mi familia",
                "Sentirme mejor conmigo mismo/a",
            ),
        ),
    ],
    "orp": [
        (
            "card_hba1c",
            "Laboratorios · HbA1c (si la tienes)",
            "¿Cuál fue tu último resultado de hemoglobina glicosilada (HbA1c)?",
            "single",
            "positive",
            'Si no la tienes, selecciona "No la sé".',
            None,
            None,
            None,
            None,
            None,
            None,
            single_options("Menos de 5.7%", "5.7% – 6.4%", "6.5% o más", "No la sé"),
        ),
        (
            "card_pa",
            "Tensión arterial · Última medición",
            "¿Cuál es tu presión arterial habitual?",
            "single",
            "positive",
            None,
            None,
            None,
            None,
            None,
            None,
            None,
            single_options(
                "Normal (< 130/80)",
                "Elevada (130–140 / 80–90)",
                "Alta (> 140/90)",
                "No la sé",
            ),
        ),
        (
            "card_tabaco",
            "Hábitos · Tabaquismo",
            "¿Fumas actualmente o has fumado en los últimos 5 años?",
            "single",
            "positive",
            None,
            None,
            None,
            None,
            None,
            None,
            None,
            single_options(
                "Nunca he fumado", "Fumé pero lo dejé", "Sí, fumo actualmente"
            ),
        ),
        (
            "card_colesterol",
            "Laboratorios · Colesterol",
            "¿Tu médico te ha dicho que tienes colesterol o triglicéridos altos?",
            "single",
            "positive",
            None,
            None,
            None,
            None,
            None,
            None,
            None,
            single_options(
                "No, están en rango normal",
                "Sí, pero controlado con dieta",
                "Sí, tomo medicamento",
                "No lo sé",
            ),
        ),
    ],
    "ers": [
        (
            "ers_familia",
            "Familia · Ambiente en casa",
            "El ambiente en mi hogar es tranquilo y me siento apoyado/a.",
            "scale",
            "positive",
            None,
            None,
            None,
            None,
            None,
            "Para nada",
            "Totalmente",
            SCALE_OPTIONS,
        ),
        (
            "ers_trabajo",
            "Trabajo · Carga laboral",
            "Mi trabajo o estudio me genera estrés frecuente o sobrecarga.",
            "scale",
            "reverse",
            None,
            None,
            None,
            None,
            None,
            "Nunca",
            "Siempre",
            SCALE_OPTIONS,
        ),
        (
            "ers_apoyo",
            "Red de apoyo · Apoyo externo",
            "¿Tienes alguien (familia, amigo, pareja) que te acompañe en este proceso?",
            "single",
            "positive",
            None,
            None,
            None,
            None,
            None,
            None,
            None,
            single_options(
                "Sí, tengo apoyo sólido",
                "Algo de apoyo",
                "Lo hago prácticamente solo/a",
            ),
        ),
        (
            "ers_tiempo",
            "Gestión · Tiempo personal",
            "Logro reservar tiempo para mí mismo/a en medio de mis responsabilidades.",
            "scale",
            "positive",
            None,
            None,
            None,
            None,
            None,
            "Nunca",
            "Siempre",
            SCALE_OPTIONS,
        ),
    ],
    "bateria-antares": [
        (
            "prop_urgencia",
            "Propósito · Urgencia del cambio",
            "Siento que necesito cambiar mis hábitos ahora, no más adelante.",
            "scale",
            "positive",
            None,
            None,
            None,
            None,
            None,
            "Para nada",
            "Totalmente",
            SCALE_OPTIONS,
        ),
        (
            "prop_creencia",
            "Mentalidad · Posibilidad de cambio",
            "Creo que puedo mejorar mi salud con el esfuerzo y el apoyo correctos.",
            "scale",
            "positive",
            None,
            None,
            None,
            None,
            None,
            "No lo creo",
            "Totalmente",
            SCALE_OPTIONS,
        ),
        (
            "prop_plazo",
            "Expectativas · Plazo esperado",
            "¿En cuánto tiempo esperas ver resultados concretos en tu salud?",
            "single",
            "positive",
            None,
            None,
            None,
            None,
            None,
            None,
            None,
            single_options(
                "Menos de 1 mes", "2–3 meses", "6 meses", "Es un proceso de vida"
            ),
        ),
        (
            "prop_meta",
            "Meta · Objetivo principal",
            "Si en 6 meses logras UN solo resultado, ¿cuál quieres que sea?",
            "single",
            "positive",
            None,
            None,
            None,
            None,
            None,
            None,
            None,
            single_options(
                "Glucosa o HbA1c en rango normal",
                "Bajar al menos 5 kg de peso",
                "Reducir o eliminar medicamentos",
                "Tener energía para disfrutar el día",
                "Sentirme mejor emocionalmente",
            ),
        ),
        (
            "prop_nota",
            "Nota personal · Para tu equipo médico",
            "Cuéntanos con tus propias palabras: ¿qué te trajo al programa ANTARES?",
            "open",
            "positive",
            "El Dr. Godoy Cruz leerá esto personalmente. Sé tan honesto/a como puedas.",
            None,
            None,
            None,
            None,
            None,
            None,
            [],
        ),
    ],
}

# Rangos por instrumento: (label, min, max, severity). Para los weighted
# (iac-adresd/orp) el score ya es 0-100.
RANGES = {
    "historia-clinica": [
        ("bajo", 0, 3, "low"),
        ("moderado", 4, 7, "moderate"),
        ("alto", 8, 11, "high"),
        ("critico", 12, 17, "critical"),
    ],
    "temperamento": [
        ("bajo", 0, 7, "low"),
        ("moderado", 8, 14, "moderate"),
        ("alto", 15, 22, "high"),
    ],
    "nutricional": [
        ("bajo", 0, 7, "low"),
        ("moderado", 8, 14, "moderate"),
        ("alto", 15, 22, "high"),
    ],
    "movimiento": [
        ("bajo", 0, 4, "low"),
        ("moderado", 5, 8, "moderate"),
        ("alto", 9, 12, "high"),
    ],
    "sueno": [
        ("bajo", 0, 4, "low"),
        ("moderado", 5, 9, "moderate"),
        ("alto", 10, 13, "high"),
    ],
    "iac-adresd": [
        ("alto", 0, 39, "high"),
        ("moderado", 40, 69, "moderate"),
        ("bajo", 70, 100, "low"),
    ],
    "orp": [
        ("bajo", 0, 24, "low"),
        ("moderado", 25, 49, "moderate"),
        ("alto", 50, 74, "high"),
        ("critico", 75, 100, "critical"),
    ],
    "ers": [
        ("bajo", 0, 4, "low"),
        ("moderado", 5, 9, "moderate"),
        ("alto", 10, 14, "high"),
    ],
    "bateria-antares": [
        ("bajo", 0, 5, "low"),
        ("moderado", 6, 10, "moderate"),
        ("alto", 11, 15, "high"),
    ],
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
        '{"formula":"weighted","sources":[{"resultType":"subscale","code":"Compromiso · Motivación"},{"resultType":"subscale","code":"Compromiso · Constancia"},{"resultType":"subscale","code":"Barreras · Obstáculos reales"},{"resultType":"subscale","code":"Propósito · Razón principal"}]}',
    ),
    (
        "iapnea",
        "Sospecha de apnea del sueño",
        '{"formula":"sum","sources":[{"resultType":"subscale","code":"Red flags · Ronquidos"}]}',
    ),
]


def build() -> str:
    lines = []
    lines.append("-- Seed del módulo Tests de Salud: batería inicial ANTARES.")
    lines.append(
        "-- Contenido fiel a ANTARES_Tests_Perfil_Salud (1).html (9 tests, 44 preguntas)."
    )
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

    # 3) Preguntas + opciones
    for code, questions in QUESTIONS.items():
        for idx, (
            qcode,
            section,
            text,
            qtype,
            direction,
            hint,
            unit,
            mn,
            mx,
            dft,
            min_lbl,
            max_lbl,
            options,
        ) in enumerate(questions):
            qid = v5(f"q:{code}:{idx}")
            meta = ""
            if hint is not None:
                meta += f", {q(hint)!r}"
            else:
                meta += ", NULL"
            for val in (unit, mn, mx, dft, min_lbl, max_lbl):
                if val is not None:
                    if isinstance(val, str):
                        meta += f", {q(val)!r}"
                    else:
                        meta += f", {val}"
                else:
                    meta += ", NULL"
            lines.append(
                "INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)"
            )
            lines.append(
                f"SELECT '{qid}', v.id, '{qcode}', {q(section)!r}, {q(text)!r}, '{qtype}', '{direction}', {idx}, true{meta}"
            )
            lines.append(
                "FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id"
            )
            lines.append(
                f"WHERE i.code = '{code}' AND v.version_number = 1 AND v.is_current = true"
            )
            lines.append(f"ON CONFLICT (version_id, code) DO NOTHING;")
            for opt_idx, (opt_text, opt_score) in enumerate(options):
                oid = v5(f"o:{code}:{idx}:{opt_idx}")
                lines.append(
                    "INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)"
                )
                lines.append(
                    f"SELECT '{oid}', q.id, {q(opt_text)!r}, {opt_score}, {opt_idx}, true"
                )
                lines.append(
                    f"FROM app.health_test_questions q WHERE q.code = '{qcode}' AND q.is_active = true"
                )
                lines.append("ON CONFLICT DO NOTHING;")
            lines.append("")

    # 4) Rangos de interpretación
    lines.append("-- Rangos de interpretación por versión.")
    for code, ranges in RANGES.items():
        for idx, (label, mn, mx, sev) in enumerate(ranges):
            rid = v5(f"range:{code}:{idx}")
            lines.append(
                "INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)"
            )
            lines.append(
                f"SELECT '{rid}', v.id, {mn}, {mx}, {q(label)!r}, '{sev}', true"
            )
            lines.append(
                "FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id"
            )
            lines.append(
                f"WHERE i.code = '{code}' AND v.version_number = 1 AND v.is_current = true"
            )
            lines.append("ON CONFLICT DO NOTHING;")
    lines.append("")

    # 5) Batería inicial
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

    # 6) Indicadores
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

    # 7) Reglas de alerta
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
