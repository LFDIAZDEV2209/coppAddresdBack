-- Seed de catálogos del repositorio documental (Fase 5).
-- Idempotente: todos los INSERT usan ON CONFLICT DO NOTHING.
-- Generado por scripts/generate_document_catalogs_seed.py; no editar a mano.

-- Categorías de documentos.
INSERT INTO app.document_categories (code, name, sort_order) VALUES
    ('LABORATORIO', 'Laboratorio', 1),
    ('IMAGENES', 'Imágenes', 2),
    ('CONSENTIMIENTOS', 'Consentimientos', 3),
    ('SEGUROS', 'Seguros', 4),
    ('REFERENCIAS', 'Referencias', 5),
    ('NOTAS_CLINICAS', 'Notas clínicas', 6),
    ('LEGALES', 'Legales', 7),
    ('ADMINISTRATIVOS', 'Administrativos', 8),
    ('OTROS', 'Otros', 9)
ON CONFLICT (code) DO NOTHING;

-- Tipos de documento clínico (con extensiones permitidas).
INSERT INTO app.clinical_document_types (id, category_id, code, name, allowed_extensions, sort_order)
SELECT gen_random_uuid(), c.id, t.code, t.name, t.allowed_extensions, t.sort_order
FROM (VALUES
    ('LABORATORIO', 'LAB_CBC', 'Hemograma completo (CBC)', ARRAY['pdf', 'png', 'jpg', 'jpeg'], 1),
    ('LABORATORIO', 'LAB_LIPID_PANEL', 'Panel lipídico', ARRAY['pdf'], 2),
    ('LABORATORIO', 'LAB_HBA1C', 'Hemoglobina A1c', ARRAY['pdf'], 3),
    ('LABORATORIO', 'LAB_THYROID', 'Perfil tiroideo', ARRAY['pdf'], 4),
    ('LABORATORIO', 'LAB_GLUCOSE', 'Glucosa en ayunas', ARRAY['pdf', 'png', 'jpg'], 5),
    ('LABORATORIO', 'LAB_VITAMIN_D', 'Vitamina D', ARRAY['pdf'], 6),
    ('IMAGENES', 'IMG_XRAY', 'Radiografía', ARRAY['pdf', 'png', 'jpg', 'jpeg', 'dicom'], 7),
    ('IMAGENES', 'IMG_MRI', 'Resonancia magnética', ARRAY['pdf', 'png', 'jpg', 'dicom'], 8),
    ('IMAGENES', 'IMG_CT', 'Tomografía computarizada', ARRAY['pdf', 'png', 'jpg', 'dicom'], 9),
    ('IMAGENES', 'IMG_ULTRASOUND', 'Ultrasonido', ARRAY['pdf', 'png', 'jpg'], 10),
    ('IMAGENES', 'IMG_DXA', 'Densitometría ósea', ARRAY['pdf'], 11),
    ('CONSENTIMIENTOS', 'CONSENT_GENERAL', 'Consentimiento general', ARRAY['pdf'], 12),
    ('CONSENTIMIENTOS', 'CONSENT_SURGERY', 'Consentimiento quirúrgico', ARRAY['pdf'], 13),
    ('CONSENTIMIENTOS', 'CONSENT_RESEARCH', 'Consentimiento de investigación', ARRAY['pdf'], 14),
    ('SEGUROS', 'INSURANCE_CARD', 'Tarjeta de seguro', ARRAY['pdf', 'png', 'jpg', 'jpeg'], 15),
    ('SEGUROS', 'INSURANCE_AUTHORIZATION', 'Autorización de seguro', ARRAY['pdf'], 16),
    ('SEGUROS', 'INSURANCE_CLAIM', 'Reclamación al seguro', ARRAY['pdf'], 17),
    ('REFERENCIAS', 'REFERRAL_PRIMARY', 'Referencia de atención primaria', ARRAY['pdf'], 18),
    ('REFERENCIAS', 'REFERRAL_SPECIALIST', 'Referencia a especialista', ARRAY['pdf'], 19),
    ('NOTAS_CLINICAS', 'CLINICAL_NOTE', 'Nota de consulta', ARRAY['pdf', 'docx', 'doc'], 20),
    ('NOTAS_CLINICAS', 'DISCHARGE_SUMMARY', 'Resumen de egreso', ARRAY['pdf'], 21),
    ('NOTAS_CLINICAS', 'PROGRESS_NOTE', 'Nota de progreso', ARRAY['pdf'], 22),
    ('LEGALES', 'LEGAL_POA', 'Poder legal de salud', ARRAY['pdf'], 23),
    ('LEGALES', 'LEGAL_ADVANCE_DIRECTIVE', 'Directiva anticipada', ARRAY['pdf'], 24),
    ('LEGALES', 'LEGAL_DNR', 'Orden de no reanimar (DNR)', ARRAY['pdf'], 25),
    ('ADMINISTRATIVOS', 'ADMIN_IDENTIFICATION', 'Identificación', ARRAY['pdf', 'png', 'jpg', 'jpeg'], 26),
    ('ADMINISTRATIVOS', 'ADMIN_ADMISSION_FORM', 'Formulario de admisión', ARRAY['pdf', 'docx'], 27),
    ('ADMINISTRATIVOS', 'ADMIN_INVOICE', 'Factura', ARRAY['pdf'], 28),
    ('OTROS', 'OTHER', 'Otro documento', ARRAY['pdf', 'png', 'jpg', 'jpeg', 'docx', 'doc', 'xlsx'], 29)
) AS t(category_code, code, name, allowed_extensions, sort_order)
JOIN app.document_categories c ON c.code = t.category_code
ON CONFLICT (code) DO NOTHING;
