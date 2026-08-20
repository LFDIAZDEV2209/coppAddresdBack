"""Genera Migrations/Seed/AddDocumentCatalogs.sql con el seed de catálogos del
repositorio documental (Fase 5): categorías de documentos y tipos de documento
clínico con sus extensiones permitidas. Los INSERTs usan ON CONFLICT DO NOTHING:
son idempotentes.

Ejecutar desde coppAddresdBack/scripts con:
  python generate_document_catalogs_seed.py
"""

from pathlib import Path

OUT = Path(__file__).resolve().parent.parent / "src" / "CoppAddresd.Infrastructure" / "Migrations" / "Seed" / "AddDocumentCatalogs.sql"

# (código categoría, nombre categoría, [(código tipo, nombre tipo, [extensiones])])
CATALOG = [
    ("LABORATORIO", "Laboratorio", [
        ("LAB_CBC", "Hemograma completo (CBC)", ["pdf", "png", "jpg", "jpeg"]),
        ("LAB_LIPID_PANEL", "Panel lipídico", ["pdf"]),
        ("LAB_HBA1C", "Hemoglobina A1c", ["pdf"]),
        ("LAB_THYROID", "Perfil tiroideo", ["pdf"]),
        ("LAB_GLUCOSE", "Glucosa en ayunas", ["pdf", "png", "jpg"]),
        ("LAB_VITAMIN_D", "Vitamina D", ["pdf"]),
    ]),
    ("IMAGENES", "Imágenes", [
        ("IMG_XRAY", "Radiografía", ["pdf", "png", "jpg", "jpeg", "dicom"]),
        ("IMG_MRI", "Resonancia magnética", ["pdf", "png", "jpg", "dicom"]),
        ("IMG_CT", "Tomografía computarizada", ["pdf", "png", "jpg", "dicom"]),
        ("IMG_ULTRASOUND", "Ultrasonido", ["pdf", "png", "jpg"]),
        ("IMG_DXA", "Densitometría ósea", ["pdf"]),
    ]),
    ("CONSENTIMIENTOS", "Consentimientos", [
        ("CONSENT_GENERAL", "Consentimiento general", ["pdf"]),
        ("CONSENT_SURGERY", "Consentimiento quirúrgico", ["pdf"]),
        ("CONSENT_RESEARCH", "Consentimiento de investigación", ["pdf"]),
    ]),
    ("SEGUROS", "Seguros", [
        ("INSURANCE_CARD", "Tarjeta de seguro", ["pdf", "png", "jpg", "jpeg"]),
        ("INSURANCE_AUTHORIZATION", "Autorización de seguro", ["pdf"]),
        ("INSURANCE_CLAIM", "Reclamación al seguro", ["pdf"]),
    ]),
    ("REFERENCIAS", "Referencias", [
        ("REFERRAL_PRIMARY", "Referencia de atención primaria", ["pdf"]),
        ("REFERRAL_SPECIALIST", "Referencia a especialista", ["pdf"]),
    ]),
    ("NOTAS_CLINICAS", "Notas clínicas", [
        ("CLINICAL_NOTE", "Nota de consulta", ["pdf", "docx", "doc"]),
        ("DISCHARGE_SUMMARY", "Resumen de egreso", ["pdf"]),
        ("PROGRESS_NOTE", "Nota de progreso", ["pdf"]),
    ]),
    ("LEGALES", "Legales", [
        ("LEGAL_POA", "Poder legal de salud", ["pdf"]),
        ("LEGAL_ADVANCE_DIRECTIVE", "Directiva anticipada", ["pdf"]),
        ("LEGAL_DNR", "Orden de no reanimar (DNR)", ["pdf"]),
    ]),
    ("ADMINISTRATIVOS", "Administrativos", [
        ("ADMIN_IDENTIFICATION", "Identificación", ["pdf", "png", "jpg", "jpeg"]),
        ("ADMIN_ADMISSION_FORM", "Formulario de admisión", ["pdf", "docx"]),
        ("ADMIN_INVOICE", "Factura", ["pdf"]),
    ]),
    ("OTROS", "Otros", [
        ("OTHER", "Otro documento", ["pdf", "png", "jpg", "jpeg", "docx", "doc", "xlsx"]),
    ]),
]


def sql_quote(value: str) -> str:
    return "'" + value.replace("'", "''") + "'"


def build() -> str:
    lines = [
        "-- Seed de catálogos del repositorio documental (Fase 5).",
        "-- Idempotente: todos los INSERT usan ON CONFLICT DO NOTHING.",
        "-- Generado por scripts/generate_document_catalogs_seed.py; no editar a mano.",
        "",
        "-- Categorías de documentos.",
        "INSERT INTO app.document_categories (code, name, sort_order) VALUES",
    ]
    total_categories = len(CATALOG)
    for i, (code, name, _) in enumerate(CATALOG):
        suffix = "," if i < total_categories - 1 else ""
        lines.append(f"    ({sql_quote(code)}, {sql_quote(name)}, {i + 1}){suffix}")
    lines.append("ON CONFLICT (code) DO NOTHING;")
    lines.append("")

    # Tipos por categoría: id generado + FK por código de categoría.
    lines += [
        "-- Tipos de documento clínico (con extensiones permitidas).",
        "INSERT INTO app.clinical_document_types (id, category_id, code, name, allowed_extensions, sort_order)",
        "SELECT gen_random_uuid(), c.id, t.code, t.name, t.allowed_extensions, t.sort_order",
        "FROM (VALUES",
    ]
    type_rows = [
        (category_code, type_code, name, extensions)
        for category_code, _, types in CATALOG
        for type_code, name, extensions in types
    ]
    for i, (category_code, type_code, name, extensions) in enumerate(type_rows):
        array = ", ".join(sql_quote(ext) for ext in extensions)
        suffix = "," if i < len(type_rows) - 1 else ""
        lines.append(
            f"    ({sql_quote(category_code)}, {sql_quote(type_code)}, {sql_quote(name)}, ARRAY[{array}], {i + 1}){suffix}"
        )
    lines.extend([
        ") AS t(category_code, code, name, allowed_extensions, sort_order)",
        "JOIN app.document_categories c ON c.code = t.category_code",
        "ON CONFLICT (code) DO NOTHING;",
        "",
    ])
    return "\n".join(lines)


def main() -> None:
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(build(), encoding="utf-8")
    print(f"Seed escrito en {OUT} ({len(build())} bytes)")


if __name__ == "__main__":
    main()