"""Genera Migrations/Seed/AddServerCatalogs.sql con el seed de catálogos base
para el servidor: alérgenos, códigos ICD-10, medicamentos, unidades de medida,
métricas de medición, rangos de referencia, la organización raíz (Medicare) y
aseguradoras.

Reglas:
- Todos los INSERT usan ON CONFLICT DO NOTHING (o guard WHERE NOT EXISTS para
  las tablas sin clave única): son idempotentes para `dotnet ef database update`.
- NO re-siembra los catálogos que ya viven en migraciones anteriores
  (blood_types, document_types, ethnicities, document_categories,
  clinical_document_types, countries, states, cities, postal_codes,
  professional_types, specialties): esos ya los cubren
  AddPatientCatalogs / AddDocumentCatalogs / AddProfessionalCatalogs.
- Solo siembra lo que en un clon/servidor limpio quedaría vacío: lo que hoy se
  puebla únicamente desde datos legacy (alérgenos/ICD-10/medicamentos) o desde
  un seeder C# de arranque (unidades/métricas/rangos), más organizaciones.

Ejecutar desde coppAddresdBack/scripts con:
  python generate_server_catalogs_seed.py
"""

from pathlib import Path

OUT = (
    Path(__file__).resolve().parent.parent
    / "src"
    / "CoppAddresd.Infrastructure"
    / "Migrations"
    / "Seed"
    / "AddServerCatalogs.sql"
)

# Alérgenos (app.allergens, único por name). Los 10 del dato legacy ya existen;
# este es el catálogo canónico ampliado.
ALLERGENS = [
    # Alimentos
    "Milk",
    "Eggs",
    "Wheat",
    "Soy",
    "Tree nuts",
    "Fish",
    "Sesame",
    "Corn",
    "Strawberry",
    "Kiwi",
    "Banana",
    "Gluten",
    "Lactose",
    "Sulfites",
    "Monosodium glutamate (MSG)",
    "Food dye",
    "Soy lecithin",
    "Egg white",
    "Peanut butter",
    "Almonds",
    "Walnuts",
    "Cashews",
    "Brazil nuts",
    "Pistachios",
    "Hazelnuts",
    "Pecans",
    "Macadamia nuts",
    "Tuna",
    "Salmon",
    "Crab",
    "Lobster",
    "Shrimp",
    "Clams",
    "Mussels",
    "Oats",
    "Barley",
    "Rye",
    "Mango",
    "Avocado",
    # Medicamentos
    "Morphine",
    "Oxycodone",
    "Amoxicillin",
    "Cephalosporins",
    "Tetracyclines",
    "Macrolides",
    "Fluoroquinolones",
    "Local anesthetics",
    "Contrast dye (iodinated)",
    "Sulfonamides",
    "Quinine",
    "Anticonvulsants",
    "Muscle relaxants",
    # Ambientales
    "Dust mites",
    "Mold",
    "Pet dander",
    "Pollen",
    "Grass pollen",
    "Ragweed",
    "Tree pollen",
    "Cockroach",
    "House dust",
    "Feathers",
    "Wool",
    "Nickel",
    "Perfume / fragrance",
    "Cleaning products",
    "Latex gloves",
]

# Códigos ICD-10 (app.icd10_codes, único por code). Set inicial curado del
# programa (obesidad, cardiometabólico, salud mental, nutrición, crónicos).
ICD10 = [
    # Obesidad y metabolismo
    ("E66.01", "Obesity, morbid"),
    ("E66.09", "Obesity, other"),
    ("E66.9", "Obesity, unspecified"),
    ("E66.8", "Obesity, other"),
    ("R73.09", "Abnormal glucose, other"),
    ("R73.9", "Hyperglycemia, unspecified"),
    ("E78.00", "Pure hypercholesterolemia, unspecified"),
    ("E78.2", "Mixed hyperlipidemia"),
    ("E78.5", "Hyperlipidemia, unspecified"),
    ("E11.9", "Type 2 diabetes mellitus without complications"),
    ("E11.65", "Type 2 diabetes with hyperglycemia"),
    ("E11.42", "Type 2 diabetes with diabetic polyneuropathy"),
    ("E11.22", "Type 2 diabetes with diabetic chronic kidney disease"),
    ("E10.9", "Type 1 diabetes mellitus without complications"),
    ("E55.9", "Vitamin D deficiency, unspecified"),
    ("E53.8", "Deficiency of other specified B group vitamins"),
    ("D50.9", "Iron deficiency anemia, unspecified"),
    ("E03.9", "Hypothyroidism, unspecified"),
    ("E05.9", "Thyrotoxicosis, unspecified"),
    ("E44.0", "Protein-calorie malnutrition, moderate"),
    ("E44.1", "Protein-calorie malnutrition, mild"),
    # Cardiometabólico
    ("I10", "Essential (primary) hypertension"),
    ("I11.9", "Hypertensive heart disease without heart failure"),
    ("I25.10", "Atherosclerotic heart disease of native coronary artery"),
    ("I48.91", "Unspecified atrial fibrillation"),
    ("I50.9", "Heart failure, unspecified"),
    ("I63.9", "Cerebral infarction, unspecified"),
    ("I70.0", "Atherosclerosis of aorta"),
    # Salud mental
    ("F32.9", "Major depressive disorder, single episode, unspecified"),
    ("F33.9", "Major depressive disorder, recurrent, unspecified"),
    ("F41.1", "Generalized anxiety disorder"),
    ("F41.0", "Panic disorder without agoraphobia"),
    ("F43.10", "Post-traumatic stress disorder, unspecified"),
    ("F42", "Obsessive-compulsive disorder"),
    ("F50.02", "Anorexia nervosa, restrictive type"),
    ("F50.2", "Bulimia nervosa"),
    ("F50.81", "Binge eating disorder"),
    ("F51.01", "Primary insomnia"),
    ("F90.9", "Attention-deficit hyperactivity disorder, unspecified"),
    ("F31.9", "Bipolar disorder, unspecified"),
    ("F20.9", "Schizophrenia, unspecified"),
    # Nutrición y digestivo
    ("K21.0", "Gastro-esophageal reflux disease with esophagitis"),
    ("K59.00", "Constipation, unspecified"),
    ("K52.9", "Noninfective gastroenteritis and colitis, unspecified"),
    ("K90.0", "Celiac disease"),
    ("K63.9", "Disease of intestine, unspecified"),
    ("R63.0", "Anorexia"),
    ("R63.4", "Abnormal weight loss"),
    ("R63.5", "Abnormal weight gain"),
    # Sueño
    ("G47.33", "Obstructive sleep apnea (adult)"),
    ("G47.09", "Other insomnia"),
    ("G47.30", "Sleep apnea, unspecified"),
    # Musculoesquelético / crónicos
    ("M54.9", "Dorsalgia, unspecified"),
    ("M25.50", "Pain in unspecified joint"),
    ("M19.90", "Unspecified osteoarthritis, unspecified site"),
    ("M81.0", "Age-related osteoporosis without current pathological fracture"),
    ("M10.9", "Gout, unspecified"),
    ("J45.909", "Unspecified asthma, uncomplicated"),
    ("J44.1", "Chronic obstructive pulmonary disease with (acute) exacerbation"),
    # Exámenes / prevención
    ("Z00.00", "Encounter for general adult medical examination"),
    ("Z13.1", "Encounter for screening for diabetes mellitus"),
    ("Z68.25", "Body mass index (BMI) 25.0-25.9, adult"),
    ("Z68.30", "Body mass index (BMI) 30.0-30.9, adult"),
    ("Z71.3", "Dietary counseling and surveillance"),
    ("Z71.82", "Exercise counseling"),
    ("Z91.83", "Personal history of underweight"),
]

# Medicamentos (app.medications, único por name). Set inicial curado: manejo de
# peso, diabetes, cardiovascular, salud mental y suplementación común.
MEDICATIONS = [
    # Manejo de peso / GLP-1
    ("Semaglutide 0.25mg", "GLP-1 Agonist"),
    ("Semaglutide 0.5mg", "GLP-1 Agonist"),
    ("Semaglutide 1.0mg", "GLP-1 Agonist"),
    ("Semaglutide 2.4mg", "GLP-1 Agonist"),
    ("Tirzepatide 2.5mg", "GLP-1/GIP Agonist"),
    ("Tirzepatide 5mg", "GLP-1/GIP Agonist"),
    ("Tirzepatide 10mg", "GLP-1/GIP Agonist"),
    ("Tirzepatide 15mg", "GLP-1/GIP Agonist"),
    ("Liraglutide 3.0mg", "GLP-1 Agonist"),
    ("Naltrexone/Bupropion 8/90mg", "Weight Management Combo"),
    ("Phentermine/Topiramate 7.5/46mg", "Weight Management Combo"),
    ("Orlistat 120mg", "Lipase Inhibitor"),
    # Diabetes
    ("Metformin 500mg", "Biguanide"),
    ("Metformin 1000mg", "Biguanide"),
    ("Metformin ER 750mg", "Biguanide"),
    ("Glipizide 5mg", "Sulfonylurea"),
    ("Dapagliflozin 10mg", "SGLT2 Inhibitor"),
    ("Sitagliptin 100mg", "DPP-4 Inhibitor"),
    ("Insulin Glargine 100 u/mL", "Long-Acting Insulin"),
    ("Insulin Lispro 100 u/mL", "Rapid-Acting Insulin"),
    # Cardiovascular
    ("Lisinopril 10mg", "ACE Inhibitor"),
    ("Losartan 50mg", "ARB"),
    ("Amlodipine 5mg", "Calcium Channel Blocker"),
    ("Hydrochlorothiazide 25mg", "Thiazide Diuretic"),
    ("Atorvastatin 20mg", "Statin"),
    ("Rosuvastatin 10mg", "Statin"),
    ("Metoprolol Succinate 50mg", "Beta Blocker"),
    ("Carvedilol 6.25mg", "Beta Blocker"),
    ("Clopidogrel 75mg", "Antiplatelet"),
    ("Aspirin 81mg", "Antiplatelet"),
    # Salud mental
    ("Sertraline 50mg", "SSRI"),
    ("Sertraline 100mg", "SSRI"),
    ("Escitalopram 10mg", "SSRI"),
    ("Fluoxetine 20mg", "SSRI"),
    ("Bupropion XL 150mg", "NDRI"),
    ("Venlafaxine ER 75mg", "SNRI"),
    ("Duloxetine 30mg", "SNRI"),
    ("Quetiapine 25mg", "Atypical Antipsychotic"),
    ("Trazodone 50mg", "SARI"),
    ("Mirtazapine 15mg", "NaSSA"),
    # Suplementación / nutrición
    ("Vitamin D3 2000 IU", "Vitamin D Supplement"),
    ("Vitamin D3 5000 IU", "Vitamin D Supplement"),
    ("Vitamin B12 1000mcg", "B12 Supplement"),
    ("Ferrous Sulfate 325mg", "Iron Supplement"),
    ("Magnesium Citrate 400mg", "Magnesium Supplement"),
    ("Omega-3 Fish Oil 1000mg", "Omega-3 Supplement"),
    ("Probiotic 10 billion CFU", "Probiotic"),
    ("Multivitamin with Minerals", "Multivitamin"),
    ("Whey Protein Powder", "Nutritional Supplement"),
    ("Fiber Supplement (Psyllium)", "Fiber Supplement"),
]

# Unidades de medida (app.unit_of_measures, único por code). Mismas que
# ClinicalMeasurementsSeeder: el seed en migración las garantiza sin arrancar
# la API.
UNITS = [
    ("mg_dl", "Miligramos por decilitro", "mg/dL"),
    ("kg", "Kilogramos", "kg"),
    ("cm", "Centímetros", "cm"),
    ("mmhg", "Milímetros de mercurio", "mmHg"),
    ("bpm", "Latidos por minuto", "bpm"),
    ("pct", "Porcentaje", "%"),
    ("kg_m2", "Kilogramos por metro cuadrado", "kg/m²"),
    # vital-signs-tracking: unidad para temperatura corporal.
    ("celsius", "Grados Celsius", "°C"),
    # device-metrics-tracking: métricas diarias del wearable.
    ("count", "Conteos", "u"),
    ("meters", "Metros", "m"),
    ("kcal", "Kilocalorías", "kcal"),
    ("minutes", "Minutos", "min"),
]

# Métricas de medición (app.measurement_metrics, único por code; FK a la unidad
# por defecto). Mismas que ClinicalMeasurementsSeeder.
METRICS = [
    ("glucose_fasting", "Glucosa en ayunas", "mg_dl", "metabolic"),
    ("weight", "Peso", "kg", "body_comp"),
    ("height", "Talla", "cm", "body_comp"),
    ("systolic_bp", "Presión arterial sistólica", "mmhg", "vital"),
    ("diastolic_bp", "Presión arterial diastólica", "mmhg", "vital"),
    ("heart_rate", "Frecuencia cardíaca", "bpm", "vital"),
    ("bmi", "Índice de masa corporal", "kg_m2", "body_comp"),
    ("body_fat", "Porcentaje de grasa corporal", "pct", "body_comp"),
    # vital-signs-tracking: métricas nuevas para el payload de signos vitales.
    ("o2_saturation", "Saturación de oxígeno", "pct", "vital"),
    ("temperature_c", "Temperatura corporal", "celsius", "vital"),
    # device-metrics-tracking: métricas diarias capturadas por el wearable.
    ("step_count", "Pasos", "count", "activity"),
    ("distance_m", "Distancia recorrida", "meters", "activity"),
    ("activity_kcal", "Calorías activas", "kcal", "activity"),
    ("sleep_minutes", "Sueño", "minutes", "lifestyle"),
]

# Rangos de referencia (app.measurement_reference_ranges). Sin clave única:
# el INSERT usa guard WHERE NOT EXISTS por (métrica, min, max, unidad).
REFERENCE_RANGES = [
    ("glucose_fasting", None, None, None, 70.0, 99.0, "mg_dl", 0),
    ("systolic_bp", None, None, None, 90.0, 120.0, "mmhg", 0),
    ("diastolic_bp", None, None, None, 60.0, 80.0, "mmhg", 0),
    ("bmi", None, None, None, 18.5, 24.9, "kg_m2", 0),
    ("heart_rate", None, None, None, 60.0, 100.0, "bpm", 0),
    # vital-signs-tracking: rango de referencia clínica para SpO2 (pendiente de
    # validación del comité; no altera las reglas de debilidad/seguridad).
    ("o2_saturation", None, None, None, 94.0, 100.0, "pct", 0),
]

# Organización raíz (erp.organizations, único por code).
ORGANIZATIONS = [
    ("medicare", "Medicare"),
]

# Aseguradoras (app.insurers, único por name). Set inicial común en USA.
INSURERS = [
    "Medicare",
    "Medicaid",
    "Blue Cross Blue Shield",
    "Aetna",
    "Cigna",
    "UnitedHealthcare",
    "Humana",
    "Kaiser Permanente",
    "Centene",
    "Molina Healthcare",
    "Oscar Health",
    "Ambetter",
    "No Insurance",
    "Self-Pay",
]


def sql_quote(value: str) -> str:
    return "'" + value.replace("'", "''") + "'"


def sql_value(value, pg_type: str | None = None) -> str:
    if value is None:
        # NULL con tipo explícito: una columna todo-NULL en VALUES se infiere
        # como text y rompe el INSERT contra columnas integer/numeric.
        return f"NULL::{pg_type}" if pg_type else "NULL"
    if isinstance(value, bool):
        return "true" if value else "false"
    if isinstance(value, (int, float)):
        return str(value)
    return sql_quote(str(value))


def build() -> str:
    lines = [
        "-- Seed de catálogos base para el servidor (schema app + erp).",
        "-- Idempotente: todos los INSERT usan ON CONFLICT DO NOTHING; los rangos",
        "-- de referencia (sin clave única) usan un guard WHERE NOT EXISTS.",
        "-- Generado por scripts/generate_server_catalogs_seed.py; no editar a mano.",
        "",
        "-- Alérgenos.",
        "INSERT INTO app.allergens (id, name, created_at)",
        "SELECT gen_random_uuid(), a.name, now()",
        "FROM (VALUES",
    ]
    for i, name in enumerate(ALLERGENS):
        suffix = "," if i < len(ALLERGENS) - 1 else ""
        lines.append(f"    ({sql_quote(name)}){suffix}")
    lines.append(") AS a(name)")
    lines.append("ON CONFLICT (name) DO NOTHING;")
    lines.append("")

    lines += [
        "-- Códigos ICD-10 (set inicial curado).",
        "INSERT INTO app.icd10_codes (id, code, description, created_at)",
        "SELECT gen_random_uuid(), c.code, c.description, now()",
        "FROM (VALUES",
    ]
    for i, (code, desc) in enumerate(ICD10):
        suffix = "," if i < len(ICD10) - 1 else ""
        lines.append(f"    ({sql_quote(code)}, {sql_quote(desc)}){suffix}")
    lines.append(") AS c(code, description)")
    lines.append("ON CONFLICT (code) DO NOTHING;")
    lines.append("")

    lines += [
        "-- Medicamentos (set inicial curado).",
        "INSERT INTO app.medications (id, name, ndc, rx_norm, drug_class, created_at)",
        "SELECT gen_random_uuid(), m.name, NULL, NULL, m.drug_class, now()",
        "FROM (VALUES",
    ]
    for i, (name, drug_class) in enumerate(MEDICATIONS):
        suffix = "," if i < len(MEDICATIONS) - 1 else ""
        lines.append(f"    ({sql_quote(name)}, {sql_quote(drug_class)}){suffix}")
    lines.append(") AS m(name, drug_class)")
    lines.append("ON CONFLICT (name) DO NOTHING;")
    lines.append("")

    lines += [
        "-- Unidades de medida.",
        "INSERT INTO app.unit_of_measures (id, code, name, symbol, is_active)",
        "SELECT gen_random_uuid(), u.code, u.name, u.symbol, true",
        "FROM (VALUES",
    ]
    for i, (code, name, symbol) in enumerate(UNITS):
        suffix = "," if i < len(UNITS) - 1 else ""
        lines.append(
            f"    ({sql_quote(code)}, {sql_quote(name)}, {sql_quote(symbol)}){suffix}"
        )
    lines.append(") AS u(code, name, symbol)")
    lines.append("ON CONFLICT (code) DO NOTHING;")
    lines.append("")

    lines += [
        "-- Métricas de medición (FK a la unidad por defecto).",
        "INSERT INTO app.measurement_metrics (id, code, name, description, default_unit_id, category, is_active)",
        "SELECT gen_random_uuid(), m.code, m.name, NULL, u.id, m.category, true",
        "FROM (VALUES",
    ]
    for i, (code, name, unit_code, category) in enumerate(METRICS):
        suffix = "," if i < len(METRICS) - 1 else ""
        lines.append(
            f"    ({sql_quote(code)}, {sql_quote(name)}, {sql_quote(unit_code)}, {sql_quote(category)}){suffix}"
        )
    lines.append(") AS m(code, name, unit_code, category)")
    lines.append("JOIN app.unit_of_measures u ON u.code = m.unit_code")
    lines.append("ON CONFLICT (code) DO NOTHING;")
    lines.append("")

    lines += [
        "-- Rangos de referencia (guard WHERE NOT EXISTS: la tabla no tiene clave única).",
        "INSERT INTO app.measurement_reference_ranges",
        "    (id, metric_id, age_min, age_max, gender, min_value, max_value, unit_id, priority, notes, is_active)",
        "SELECT gen_random_uuid(), met.id, r.age_min, r.age_max, r.gender,",
        "       r.min_value, r.max_value, u.id, r.priority, NULL, true",
        "FROM (VALUES",
    ]
    for i, (
        metric_code,
        age_min,
        age_max,
        gender,
        min_val,
        max_val,
        unit_code,
        priority,
    ) in enumerate(REFERENCE_RANGES):
        suffix = "," if i < len(REFERENCE_RANGES) - 1 else ""
        vals = ", ".join(
            [
                sql_value(metric_code),
                sql_value(age_min, "int"),
                sql_value(age_max, "int"),
                sql_value(gender, "text"),
                sql_value(min_val, "numeric"),
                sql_value(max_val, "numeric"),
                sql_value(unit_code),
                str(priority),
            ]
        )
        lines.append(f"    ({vals}){suffix}")
    lines.append(
        ") AS r(metric_code, age_min, age_max, gender, min_value, max_value, unit_code, priority)"
    )
    lines.append("JOIN app.measurement_metrics met ON met.code = r.metric_code")
    lines.append("JOIN app.unit_of_measures u ON u.code = r.unit_code")
    lines.append("WHERE NOT EXISTS (")
    lines.append("    SELECT 1 FROM app.measurement_reference_ranges rr")
    lines.append("    WHERE rr.metric_id = met.id")
    lines.append("      AND rr.min_value = r.min_value")
    lines.append("      AND rr.max_value = r.max_value")
    lines.append("      AND rr.unit_id = u.id")
    lines.append(");")
    lines.append("")

    lines += [
        "-- Organización raíz del tenant.",
        "INSERT INTO erp.organizations (id, code, name, is_active, created_at)",
        "SELECT gen_random_uuid(), o.code, o.name, true, now()",
        "FROM (VALUES",
    ]
    for i, (code, name) in enumerate(ORGANIZATIONS):
        suffix = "," if i < len(ORGANIZATIONS) - 1 else ""
        lines.append(f"    ({sql_quote(code)}, {sql_quote(name)}){suffix}")
    lines.append(") AS o(code, name)")
    lines.append("ON CONFLICT (code) DO NOTHING;")
    lines.append("")

    lines += [
        "-- Aseguradoras (set inicial).",
        "INSERT INTO app.insurers (id, name, created_at)",
        "SELECT gen_random_uuid(), i.name, now()",
        "FROM (VALUES",
    ]
    for i, name in enumerate(INSURERS):
        suffix = "," if i < len(INSURERS) - 1 else ""
        lines.append(f"    ({sql_quote(name)}){suffix}")
    lines.append(") AS i(name)")
    lines.append("ON CONFLICT (name) DO NOTHING;")
    lines.append("")

    return "\n".join(lines)


def main() -> None:
    OUT.parent.mkdir(parents=True, exist_ok=True)
    content = build()
    OUT.write_text(content, encoding="utf-8")
    print(f"Seed escrito en {OUT} ({len(content)} bytes)")


if __name__ == "__main__":
    main()
