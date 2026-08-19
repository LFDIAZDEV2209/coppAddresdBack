"""Genera Migrations/Seed/AddProfessionalCatalogs.sql con el seed de catálogos
profesionales del ERP (profesiones, especialidades y su mapeo N:N).
Los INSERTs usan ON CONFLICT DO NOTHING: son idempotentes.

Ejecutar desde coppAddresdBack/scripts con:
  python generate_professional_catalogs_seed.py
"""

from pathlib import Path

OUT = (
    Path(__file__).resolve().parent.parent
    / "src"
    / "CoppAddresd.Infrastructure"
    / "Migrations"
    / "Seed"
    / "AddProfessionalCatalogs.sql"
)

# (código, nombre, descripción)
PROFESSIONAL_TYPES = [
    ("PHYSICIAN", "Physician (MD/DO)", "Médico/a con licencia estatal (Doctor of Medicine o Doctor of Osteopathic Medicine)."),
    ("NURSE_PRACTITIONER", "Nurse Practitioner (NP)", "Enfermero/a de práctica avanzada con prescripción."),
    ("PHYSICIAN_ASSISTANT", "Physician Assistant (PA)", "Asistente médico con licencia bajo supervisión de un médico."),
    ("REGISTERED_NURSE", "Registered Nurse (RN)", "Enfermero/a registrado/a."),
    ("LICENSED_PRACTICAL_NURSE", "Licensed Practical Nurse (LPN)", "Enfermero/a práctico/a licenciado/a."),
    ("CLINICAL_PSYCHOLOGIST", "Clinical Psychologist (PhD/PsyD)", "Psicólogo/a clínico/a con doctorado."),
    ("PSYCHIATRIST", "Psychiatrist (MD/DO)", "Médico/a especialista en psiquiatría."),
    ("CLINICAL_SOCIAL_WORKER", "Licensed Clinical Social Worker (LCSW)", "Trabajador/a social clínico/a licenciado/a."),
    ("PROFESSIONAL_COUNSELOR", "Licensed Professional Counselor (LPC)", "Consejero/a profesional licenciado/a."),
    ("REGISTERED_DIETITIAN", "Registered Dietitian (RD/RDN)", "Dietista registrado/a (Academy of Nutrition and Dietetics)."),
    ("PHYSICAL_THERAPIST", "Physical Therapist (DPT)", "Fisioterapeuta con doctorado profesional."),
    ("EXERCISE_PHYSIOLOGIST", "Exercise Physiologist", "Fisiólogo/a del ejercicio."),
    ("HEALTH_COACH", "Certified Health Coach (NBC-HWC)", "Coach de salud certificado/a (National Board)."),
    ("PHARMACIST", "Pharmacist (PharmD)", "Farmacéutico/a."),
    ("CARE_COORDINATOR", "Care Coordinator", "Coordinador/a de atención al paciente."),
    ("COMMUNITY_HEALTH_WORKER", "Community Health Worker", "Trabajador/a de salud comunitaria."),
    ("MEDICAL_ASSISTANT", "Medical Assistant", "Asistente médico/a de consulta."),
]

# (código, nombre, categoría, descripción)
SPECIALTIES = [
    # Medicina
    ("OBESITY_MEDICINE", "Obesity Medicine", "Medicina", "Tratamiento integral de la obesidad (certificación ABOM)."),
    ("BARIATRIC_SURGERY", "Bariatric Surgery", "Medicina", "Cirugía bariátrica y metabólica."),
    ("ENDOCRINOLOGY", "Endocrinology & Diabetes", "Medicina", "Endocrinología y metabolismo (diabetes, tiroides)."),
    ("FAMILY_MEDICINE", "Family Medicine", "Medicina", "Medicina familiar y atención primaria."),
    ("INTERNAL_MEDICINE", "Internal Medicine", "Medicina", "Medicina interna del adulto."),
    ("PEDIATRICS", "Pediatrics", "Medicina", "Pediatría general."),
    ("ADOLESCENT_MEDICINE", "Adolescent Medicine", "Medicina", "Medicina del adolescente."),
    ("PREVENTIVE_MEDICINE", "Preventive Medicine", "Medicina", "Medicina preventiva y salud pública."),
    ("LIFESTYLE_MEDICINE", "Lifestyle Medicine", "Medicina", "Medicina del estilo de vida (certificación ABLM)."),
    ("SPORTS_MEDICINE", "Sports Medicine", "Medicina", "Medicina deportiva."),
    ("PHYSICAL_MEDICINE_REHAB", "Physical Medicine & Rehabilitation", "Medicina", "Medicina física y rehabilitación."),
    ("CARDIOLOGY", "Cardiology", "Medicina", "Cardiología clínica y preventiva."),
    ("GASTROENTEROLOGY", "Gastroenterology", "Medicina", "Gastroenterología y hepatología."),
    ("SLEEP_MEDICINE", "Sleep Medicine", "Medicina", "Medicina del sueño (apnea obstructiva)."),
    # Nutrición
    ("CLINICAL_NUTRITION", "Clinical Nutrition", "Nutrición", "Nutrición clínica y dietoterapia."),
    ("PEDIATRIC_NUTRITION", "Pediatric Nutrition", "Nutrición", "Nutrición pediátrica."),
    ("SPORTS_NUTRITION", "Sports Nutrition", "Nutrición", "Nutrición deportiva."),
    # Salud mental
    ("BEHAVIORAL_HEALTH", "Behavioral Health", "Salud mental", "Salud conductual y manejo de hábitos."),
    ("PSYCHIATRY", "Psychiatry", "Salud mental", "Psiquiatría general."),
    ("CLINICAL_PSYCHOLOGY", "Clinical Psychology", "Salud mental", "Psicología clínica."),
    ("BARIATRIC_PSYCHOLOGY", "Bariatric Psychology", "Salud mental", "Psicología pre/post cirugía bariátrica."),
    # Enfermería
    ("MEDSURG_NURSING", "Medical-Surgical Nursing", "Enfermería", "Enfermería médico-quirúrgica."),
    ("COMMUNITY_HEALTH_NURSING", "Community Health Nursing", "Enfermería", "Enfermería de salud comunitaria."),
    # Terapia
    ("PHYSICAL_THERAPY", "Physical Therapy", "Terapia", "Fisioterapia y rehabilitación funcional."),
    ("OCCUPATIONAL_THERAPY", "Occupational Therapy", "Terapia", "Terapia ocupacional."),
    # Coordinación
    ("CARE_COORDINATION", "Care Coordination", "Coordinación", "Coordinación de cuidados y navegación del paciente."),
    ("HEALTH_EDUCATION", "Health Education", "Coordinación", "Educación para la salud y promoción."),
    # Fitness
    ("EXERCISE_PHYSIOLOGY", "Exercise Physiology", "Fitness", "Fisiología del ejercicio y prescripción de actividad física."),
    ("WELLNESS_COACHING", "Wellness Coaching", "Fitness", "Coaching de bienestar y hábitos saludables."),
]

# Mapeo N:N profesión → especialidades que puede ejercer.
TYPE_SPECIALTIES = {
    "PHYSICIAN": [
        "OBESITY_MEDICINE", "BARIATRIC_SURGERY", "ENDOCRINOLOGY", "FAMILY_MEDICINE",
        "INTERNAL_MEDICINE", "PEDIATRICS", "ADOLESCENT_MEDICINE", "PREVENTIVE_MEDICINE",
        "LIFESTYLE_MEDICINE", "SPORTS_MEDICINE", "PHYSICAL_MEDICINE_REHAB", "CARDIOLOGY",
        "GASTROENTEROLOGY", "SLEEP_MEDICINE", "CLINICAL_NUTRITION", "BEHAVIORAL_HEALTH",
    ],
    "NURSE_PRACTITIONER": [
        "FAMILY_MEDICINE", "INTERNAL_MEDICINE", "PEDIATRICS", "ADOLESCENT_MEDICINE",
        "PREVENTIVE_MEDICINE", "LIFESTYLE_MEDICINE", "OBESITY_MEDICINE", "ENDOCRINOLOGY",
        "BEHAVIORAL_HEALTH", "COMMUNITY_HEALTH_NURSING",
    ],
    "PHYSICIAN_ASSISTANT": [
        "FAMILY_MEDICINE", "INTERNAL_MEDICINE", "PEDIATRICS", "OBESITY_MEDICINE",
        "ENDOCRINOLOGY", "SPORTS_MEDICINE", "PREVENTIVE_MEDICINE",
    ],
    "REGISTERED_NURSE": [
        "MEDSURG_NURSING", "COMMUNITY_HEALTH_NURSING", "CARE_COORDINATION",
        "HEALTH_EDUCATION", "OBESITY_MEDICINE", "ENDOCRINOLOGY",
    ],
    "LICENSED_PRACTICAL_NURSE": [
        "MEDSURG_NURSING", "COMMUNITY_HEALTH_NURSING", "CARE_COORDINATION",
    ],
    "CLINICAL_PSYCHOLOGIST": [
        "CLINICAL_PSYCHOLOGY", "BEHAVIORAL_HEALTH", "BARIATRIC_PSYCHOLOGY",
    ],
    "PSYCHIATRIST": ["PSYCHIATRY", "BEHAVIORAL_HEALTH"],
    "CLINICAL_SOCIAL_WORKER": ["BEHAVIORAL_HEALTH", "CARE_COORDINATION", "HEALTH_EDUCATION"],
    "PROFESSIONAL_COUNSELOR": ["BEHAVIORAL_HEALTH", "HEALTH_EDUCATION"],
    "REGISTERED_DIETITIAN": [
        "CLINICAL_NUTRITION", "PEDIATRIC_NUTRITION", "SPORTS_NUTRITION",
        "HEALTH_EDUCATION", "OBESITY_MEDICINE",
    ],
    "PHYSICAL_THERAPIST": ["PHYSICAL_THERAPY"],
    "EXERCISE_PHYSIOLOGIST": ["EXERCISE_PHYSIOLOGY", "WELLNESS_COACHING"],
    "HEALTH_COACH": ["WELLNESS_COACHING", "HEALTH_EDUCATION", "BEHAVIORAL_HEALTH"],
    "PHARMACIST": [],
    "CARE_COORDINATOR": ["CARE_COORDINATION", "HEALTH_EDUCATION"],
    "COMMUNITY_HEALTH_WORKER": ["HEALTH_EDUCATION", "COMMUNITY_HEALTH_NURSING", "CARE_COORDINATION"],
    "MEDICAL_ASSISTANT": ["CARE_COORDINATION", "HEALTH_EDUCATION"],
}


def esc(value: str) -> str:
    return value.replace("'", "''")


def build_sql() -> str:
    lines = [
        "-- Seed de catálogos profesionales del ERP (schema erp).",
        "-- Idempotente: todos los INSERT usan ON CONFLICT DO NOTHING.",
        "-- Generado por scripts/generate_professional_catalogs_seed.py; no editar a mano.",
        "",
        "-- Profesiones (professional_types).",
        "INSERT INTO erp.professional_types (code, name, description, sort_order) VALUES",
    ]
    rows = [
        f"    ('{code}', '{esc(name)}', '{esc(desc)}', {i})"
        for i, (code, name, desc) in enumerate(PROFESSIONAL_TYPES, start=1)
    ]
    lines.append(",\n".join(rows))
    lines.append("ON CONFLICT (code) DO NOTHING;")
    lines.append("")

    lines.append("-- Especialidades (specialties).")
    lines.append(
        "INSERT INTO erp.specialties (code, name, category, description, sort_order) VALUES"
    )
    rows = [
        f"    ('{code}', '{esc(name)}', '{esc(cat)}', '{esc(desc)}', {i})"
        for i, (code, name, cat, desc) in enumerate(SPECIALTIES, start=1)
    ]
    lines.append(",\n".join(rows))
    lines.append("ON CONFLICT (code) DO NOTHING;")
    lines.append("")

    # Mapeo N:N con join por código: no requiere UUIDs fijos.
    lines.append("-- Mapeo profesión → especialidades válidas (professional_type_specialties).")
    lines.append(
        "INSERT INTO erp.professional_type_specialties (professional_type_id, specialty_id)"
    )
    lines.append("SELECT pt.id, s.id")
    lines.append("FROM (VALUES")
    pairs = []
    for pt_code, spec_codes in TYPE_SPECIALTIES.items():
        for spec_code in spec_codes:
            pairs.append(f"    ('{pt_code}', '{spec_code}')")
    lines.append(",\n".join(pairs))
    lines.append(f") AS m(pt_code, s_code)")
    lines.append("JOIN erp.professional_types pt ON pt.code = m.pt_code")
    lines.append("JOIN erp.specialties s ON s.code = m.s_code")
    lines.append("ON CONFLICT (professional_type_id, specialty_id) DO NOTHING;")
    lines.append("")

    return "\n".join(lines)


def main() -> None:
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(build_sql(), encoding="utf-8")
    print(f"Generado {OUT}")


if __name__ == "__main__":
    main()
