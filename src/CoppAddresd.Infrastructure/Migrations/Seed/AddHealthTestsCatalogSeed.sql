-- Seed del módulo Tests de Salud: batería inicial Copp Adresd.
-- Contenido fiel a ANTARES_Tests_Perfil_Salud (1).html (9 tests, 44 preguntas).
-- Idempotente (ON CONFLICT DO NOTHING sobre claves únicas).
-- Generado por scripts/generate_health_tests_seed.py — editar el script, no este SQL.

INSERT INTO app.health_test_instruments (id, code, name, description, category, sort_order, is_active, created_at)
SELECT * FROM (VALUES
    ('9726aaac-f747-59cb-8daa-71b37cbdb057'::uuid, 'historia-clinica', 'Historia clínica', 'Antecedentes, medicamentos y cómo te sientes físicamente.', 'clinico', 1, true, now()),
    ('2c8e7171-ef3d-525f-8277-f83558009609'::uuid, 'temperamento', 'Temperamento', 'Tu personalidad determina cómo te acompañamos en el programa.', 'psicologico', 2, true, now()),
    ('924d0bae-5355-507e-a51d-152094d879a8'::uuid, 'nutricional', 'Nutrición', 'Tus hábitos alimentarios y tu relación con la comida.', 'nutricion', 3, true, now()),
    ('c624b36d-3880-572d-b084-fcdd2f67edf2'::uuid, 'movimiento', 'Movimiento · AMAF', 'Tu capacidad física actual determina el circuito que te asignamos.', 'movimiento', 4, true, now()),
    ('ab263023-fce0-5376-a648-9fe3bb101725'::uuid, 'sueno', 'Sueño', 'El sueño impacta directamente tu glucosa, tu peso y tu adherencia.', 'sueno', 5, true, now()),
    ('cd48e94c-c326-5312-8040-f44d2f4001d4'::uuid, 'iac-adresd', 'Adherencia · IAC', 'Tu motivación real determina cómo te acompañamos.', 'adherencia', 6, true, now()),
    ('b322c457-2853-5baf-a66f-0e255c7c725e'::uuid, 'orp', 'Riesgo cardiometabólico ORP', 'Información clínica confidencial — solo la ve tu equipo médico.', 'clinico', 7, true, now()),
    ('da092b10-061e-59e6-ad36-abb6bc71643e'::uuid, 'ers', 'Estrés relacional · ERS', 'El estrés en casa o en el trabajo es la barrera #1 de la adherencia.', 'psicologico', 8, true, now()),
    ('caa212ce-0b3e-5c5f-a5bb-8c796166e1c7'::uuid, 'bateria-antares', 'Propósito · Copp Adresd', 'Las respuestas más importantes del programa. Sé completamente honesto/a.', 'integral', 9, true, now())
) AS t(id, code, name, description, category, sort_order, is_active, created_at)
ON CONFLICT (code) DO NOTHING;

INSERT INTO app.health_test_versions (id, instrument_id, version_number, status, is_current, scoring_strategy, points, created_at, published_at)
SELECT v.id, i.id, v.vn, 'active', true, v.ss, v.pts, now(), now()
FROM (VALUES
    ('55fa24c4-0eb2-5dfe-adc7-7fd0d15b467b'::uuid, 'historia-clinica', 1, 'inventory', 50),
    ('3ee0a5ba-6870-568e-8f22-85657b7172d5'::uuid, 'temperamento', 1, 'subscale', 40),
    ('c991b0af-a1d3-53c6-aa4c-536175b04ec6'::uuid, 'nutricional', 1, 'subscale', 40),
    ('d716b4b9-e939-5755-bd40-2f3d3d2441b2'::uuid, 'movimiento', 1, 'subscale', 40),
    ('d07d03fa-b286-5ea6-bdeb-7ee05c6c66b9'::uuid, 'sueno', 1, 'subscale', 40),
    ('c7b3cafc-0de1-5c44-b305-e1be945f0598'::uuid, 'iac-adresd', 1, 'weighted', 40),
    ('ebbc1034-880f-5319-8642-95fb8f96e19f'::uuid, 'orp', 1, 'weighted', 40),
    ('0f63b53a-8f42-5157-b155-19848545f44a'::uuid, 'ers', 1, 'subscale', 40),
    ('589a7c84-00b6-5098-b1fa-8ed32388c932'::uuid, 'bateria-antares', 1, 'subscale', 50)
) AS v(id, code, vn, ss, pts)
JOIN app.health_test_instruments i ON i.code = v.code
ON CONFLICT (instrument_id, version_number) DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT '5dcc3d7f-5ec4-59f5-b153-90f1982f1275'::uuid, v.id, 'dx', 'Diagnósticos actuales', '¿Cuál de estos diagnósticos tienes actualmente?', 'multi', 'positive', 0, true, 'Marca todos los que aplican.', NULL, NULL, NULL, NULL, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'historia-clinica' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '7c55a535-0149-557f-8c65-045958665cf6'::uuid, q.id, 'Diabetes tipo 2', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'dx' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '3c26ad48-a29d-5960-a359-d25d43d8ee8c'::uuid, q.id, 'Obesidad', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'dx' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'b445cfcf-8868-5cfd-9d75-5b3e53a4491e'::uuid, q.id, 'Hipertensión', 1, 2, true
FROM app.health_test_questions q WHERE q.code = 'dx' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '79171c68-dbf9-5749-afd0-96b91167f85a'::uuid, q.id, 'Prediabetes', 1, 3, true
FROM app.health_test_questions q WHERE q.code = 'dx' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '7e715032-5f20-5320-b6e9-64cf970b729c'::uuid, q.id, 'Colesterol alto', 1, 4, true
FROM app.health_test_questions q WHERE q.code = 'dx' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '2148cf5a-3f77-537e-bb13-3b11b9a1ad56'::uuid, q.id, 'Ninguno por ahora', 0, 5, true
FROM app.health_test_questions q WHERE q.code = 'dx' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT 'fae88cbf-c2f0-52a0-963a-e186f85ffc6d'::uuid, v.id, 'med', 'Medicamentos', '¿Tomas medicamentos actualmente?', 'single', 'positive', 1, true, NULL, NULL, NULL, NULL, NULL, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'historia-clinica' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '02edccea-aa7e-52f0-84a3-ba09e986259d'::uuid, q.id, 'Sí, con receta médica', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'med' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '9534010a-8ffe-5068-9b3d-597125d9bfa9'::uuid, q.id, 'Solo suplementos', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'med' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'a6848edc-be02-5e73-bba1-355628c0ab63'::uuid, q.id, 'No tomo nada', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'med' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT '1d9d0a7c-70ee-5e04-97b7-502e58831617'::uuid, v.id, 'sexo', 'Datos personales · Sexo biológico', '¿Cuál es tu sexo biológico?', 'single', 'positive', 2, true, 'Necesario para calcular tu % de grasa corporal con mayor precisión.', NULL, NULL, NULL, NULL, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'historia-clinica' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '15223ec9-3c31-59fd-8d7c-2b86ada7fe56'::uuid, q.id, 'Masculino', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'sexo' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '32a12ed7-036c-58ce-b807-31e12a9a3c88'::uuid, q.id, 'Femenino', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'sexo' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT '548279f1-3f68-5f48-9717-bfdb23d3c2cf'::uuid, v.id, 'edad', 'Datos personales · Edad', '¿Cuántos años tienes?', 'num', 'positive', 3, true, NULL, 'años', 18, 90, 40, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'historia-clinica' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT '817090ff-c4b4-5a50-ae87-bad44585e9f2'::uuid, v.id, 'peso', 'Biometría · Peso corporal', '¿Cuánto pesas? Si tienes báscula en casa, pésate ahora.', 'num', 'positive', 4, true, 'Párate en la báscula con ropa ligera, sin zapatos. Si no tienes, pon un aproximado.', 'kg', 40, 220, 75, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'historia-clinica' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT 'ca9f42a9-d070-5d45-955f-dea717b3f2c5'::uuid, v.id, 'talla', 'Biometría · Estatura', '¿Cuánto mides de estatura?', 'num', 'positive', 5, true, 'Párate derecho/a contra una pared y marca con un libro. Mide hasta el suelo.', 'cm', 140, 210, 165, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'historia-clinica' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT 'afdb4dc8-adf6-5a84-9053-d21728b0619c'::uuid, v.id, 'cintura', 'Biometría · Cintura', '¿Cuánto mide tu cintura al nivel del ombligo?', 'num', 'positive', 6, true, 'Con una cinta métrica (o tira de papel que luego mides), rodea tu abdomen a la altura del ombligo al exhalar.', 'cm', 50, 180, 90, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'historia-clinica' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT '9fe711ff-3a35-567c-aa08-70db5de1e468'::uuid, v.id, 'cadera', 'Biometría · Cadera', '¿Cuánto mide la parte más ancha de tu cadera?', 'num', 'positive', 7, true, 'Mide alrededor de la parte más amplia de tus caderas y glúteos.', 'cm', 60, 200, 100, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'historia-clinica' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT 'fa6f2e2b-1a79-5296-94dc-8e11f2f4ab76'::uuid, v.id, 'muneca', 'Biometría · Muñeca', '¿Cuánto mide tu muñeca?', 'num', 'positive', 8, true, 'Rodea tu muñeca dominante con la cinta por debajo de los huesos del puño. Este dato ayuda a calcular tu % de grasa.', 'cm', 12, 25, 17, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'historia-clinica' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT '0c93f205-fc80-5a22-a631-4997a2adacf5'::uuid, v.id, 'gluc_ayunas', 'Glucosa · Última medición en ayunas', '¿Tienes glucómetro en casa o algún resultado reciente de glucosa en ayunas?', 'single', 'positive', 9, true, 'Ayunas = sin haber comido en las últimas 8 horas.', NULL, NULL, NULL, NULL, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'historia-clinica' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'f678e070-b159-5247-b086-e0bacec756ea'::uuid, q.id, 'Sí, está en rango (< 100 mg/dL)', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'gluc_ayunas' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '7d7daf54-6414-5a21-b606-746763c8b434'::uuid, q.id, 'Sí, entre 100 y 125 mg/dL', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'gluc_ayunas' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'bc6de5f4-5d74-538b-a482-d69ec98e138d'::uuid, q.id, 'Sí, 126 mg/dL o más', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'gluc_ayunas' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '32fc8797-942b-5464-a1a4-add2f8836ade'::uuid, q.id, 'No sé o no me la he medido', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'gluc_ayunas' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT '08290da8-24fd-535b-b39e-e750d7c41d1a'::uuid, v.id, 'gluc_sintomas', 'Glucosa · Señales del cuerpo', '¿Cuáles de estas señales reconoces en tu cuerpo con frecuencia?', 'multi', 'positive', 10, true, 'Estas señales ayudan a estimar si tu glucosa puede estar elevada.', NULL, NULL, NULL, NULL, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'historia-clinica' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '92d341aa-6e40-5b13-a692-78b43a71601b'::uuid, q.id, 'Sed intensa todo el día', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'gluc_sintomas' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'cb926e4f-7a3b-5772-b083-b3ba88d20e12'::uuid, q.id, 'Orinas muchas veces al día', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'gluc_sintomas' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '76d35a3c-10ac-5b3f-ae0f-0ee7214e9dbb'::uuid, q.id, 'Cansancio después de comer', 1, 2, true
FROM app.health_test_questions q WHERE q.code = 'gluc_sintomas' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'fe10bdf4-ed2a-5950-8d43-23161ffc8205'::uuid, q.id, 'Antojos intensos de dulce', 1, 3, true
FROM app.health_test_questions q WHERE q.code = 'gluc_sintomas' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'd6c80713-de5b-54c6-b015-6ed79ce58b0c'::uuid, q.id, 'Vista borrosa ocasional', 1, 4, true
FROM app.health_test_questions q WHERE q.code = 'gluc_sintomas' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '081cea0a-61f2-5b38-9c79-d81b9869716e'::uuid, q.id, 'No tengo ninguna de estas', 0, 5, true
FROM app.health_test_questions q WHERE q.code = 'gluc_sintomas' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT 'a7853018-1571-5601-8f5f-75625d87f9ce'::uuid, v.id, 'sintomas', 'Síntomas frecuentes generales', 'Además de lo anterior, ¿tienes alguno de estos síntomas con regularidad?', 'multi', 'positive', 11, true, 'Marca todos los que aplican.', NULL, NULL, NULL, NULL, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'historia-clinica' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '7e49f9fc-a515-5da0-b480-82f2bf297f93'::uuid, q.id, 'Hormigueo en pies o manos', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'sintomas' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'ddc81324-a17b-56d3-9e97-d4ba2b6be05e'::uuid, q.id, 'Heridas que tardan en sanar', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'sintomas' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'd073ddc7-5d6c-51c3-8e52-85a0af02cfac'::uuid, q.id, 'Dificultad para respirar', 1, 2, true
FROM app.health_test_questions q WHERE q.code = 'sintomas' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '8a7203cd-aa4d-57d4-b88a-938bc190cb62'::uuid, q.id, 'Me siento bien en general', 0, 3, true
FROM app.health_test_questions q WHERE q.code = 'sintomas' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT '02969817-1fb3-58b4-8e02-2b6eea46c7a3'::uuid, v.id, 'antfam', 'Antecedentes familiares', '¿Algún familiar directo tiene o tuvo alguna de estas condiciones?', 'multi', 'positive', 12, true, 'Padres, hermanos, abuelos.', NULL, NULL, NULL, NULL, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'historia-clinica' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'a747897d-b057-5b61-bc76-834d3f59728a'::uuid, q.id, 'Diabetes', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'antfam' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '10a0bfca-9075-5941-b6e3-b5847b022498'::uuid, q.id, 'Hipertensión', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'antfam' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'db1598b7-a820-5174-8cd5-2282c932181b'::uuid, q.id, 'Infarto o ACV', 1, 2, true
FROM app.health_test_questions q WHERE q.code = 'antfam' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '8035c6b8-e67d-5cbe-be0b-4bfc4a92f316'::uuid, q.id, 'Obesidad', 1, 3, true
FROM app.health_test_questions q WHERE q.code = 'antfam' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '4a4c29b8-3db0-53f0-a83c-2aacc2ab4c0b'::uuid, q.id, 'Sin antecedentes', 1, 4, true
FROM app.health_test_questions q WHERE q.code = 'antfam' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT '98873855-117b-59b8-871e-526363740200'::uuid, v.id, 'temp_social', 'Temperamento · Dimensión social', 'Me siento bien en grupos grandes y hago amigos con facilidad.', 'scale', 'positive', 0, true, NULL, NULL, NULL, NULL, NULL, 'Nunca', 'Siempre'
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '72ba4c34-d0c3-58f2-8f17-35f1778ffd8d'::uuid, q.id, '1', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'temp_social' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'cddbe19a-e6d4-5279-b852-4982c5fc3004'::uuid, q.id, '2', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'temp_social' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '4a2981ec-5d90-517d-8a7d-f9914726140a'::uuid, q.id, '3', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'temp_social' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '25a5c2dc-41e5-5289-bc51-9ab0194671e0'::uuid, q.id, '4', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'temp_social' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '16aef632-cbcf-5809-8fd5-5b3eaf307ac0'::uuid, q.id, '5', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'temp_social' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT 'ec04dcb6-2250-5654-9ff1-e9b289a39533'::uuid, v.id, 'temp_metas', 'Temperamento · Metas y resultados', 'Cuando me propongo algo, lo hago sin importar los obstáculos.', 'scale', 'positive', 1, true, NULL, NULL, NULL, NULL, NULL, 'Nunca', 'Siempre'
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '33c0eb2d-daaa-5a0f-a165-ee23b02ca67e'::uuid, q.id, '1', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'temp_metas' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '878cfadb-c582-5379-b819-c28314c77067'::uuid, q.id, '2', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'temp_metas' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '2dbff712-ca56-58c7-99f5-a88f79c8ee36'::uuid, q.id, '3', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'temp_metas' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '8d06f4ba-06f3-581f-afde-be7cb8124302'::uuid, q.id, '4', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'temp_metas' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5aaaa766-a9da-577d-b45f-a1ff72006aa4'::uuid, q.id, '5', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'temp_metas' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT 'f448ee96-2939-558d-951d-29a1035e444b'::uuid, v.id, 'temp_analisis', 'Temperamento · Análisis y detalle', 'Antes de actuar, analizo bien todas las opciones disponibles.', 'scale', 'positive', 2, true, NULL, NULL, NULL, NULL, NULL, 'Nunca', 'Siempre'
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '56c631d4-afca-5e32-ad2d-2335a434fdf0'::uuid, q.id, '1', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'temp_analisis' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'c47e057b-78fe-5f50-afcd-95246d9972b5'::uuid, q.id, '2', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'temp_analisis' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '4f0edb0f-63bc-5716-baa6-2fffd289810b'::uuid, q.id, '3', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'temp_analisis' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '2a2e6ef2-d793-5730-a86d-ed5acbbb6566'::uuid, q.id, '4', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'temp_analisis' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '6fb8a69f-0258-5e04-81b7-ee4a56f5a345'::uuid, q.id, '5', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'temp_analisis' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT '9be35d7c-e805-5941-b277-2cf4dc172822'::uuid, v.id, 'temp_rutina', 'Temperamento · Rutina y estabilidad', 'Prefiero tener rutinas fijas y predecibles en mi vida diaria.', 'scale', 'positive', 3, true, NULL, NULL, NULL, NULL, NULL, 'Nunca', 'Siempre'
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'a785466b-14ea-5c23-8f72-5c7a949b6fb7'::uuid, q.id, '1', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'temp_rutina' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '7ebf87e2-efb6-585a-9530-44b1b949e7dc'::uuid, q.id, '2', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'temp_rutina' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e9b3dd0f-0cb2-5c79-ab01-698d71fa1f11'::uuid, q.id, '3', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'temp_rutina' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '7c95301f-6317-5a29-9704-f019a41acf4f'::uuid, q.id, '4', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'temp_rutina' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'd34eace1-3985-50eb-a9b8-72002aaddc71'::uuid, q.id, '5', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'temp_rutina' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT 'c4a6ba44-0adc-50e1-ab46-eb90a6817b60'::uuid, v.id, 'temp_emociones', 'Temperamento · Reacción emocional', 'Cuando algo no sale como esperaba, ¿cuál es tu reacción más común?', 'single', 'positive', 4, true, NULL, NULL, NULL, NULL, NULL, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '33229485-ec98-5164-9490-e7bf07a101c7'::uuid, q.id, 'Me frustro pero sigo intentando', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'temp_emociones' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5da818f0-9073-5fae-a010-cebd7f0539bb'::uuid, q.id, 'Lo siento mucho y reflexiono', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'temp_emociones' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'dfd13e7a-9abb-5985-afab-f977d5ff8bc4'::uuid, q.id, 'Lo dejo ir y busco algo nuevo', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'temp_emociones' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'bc3181f8-725d-596f-95ac-6b16d4367eb1'::uuid, q.id, 'Lo acepto y espero el momento', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'temp_emociones' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT '41591986-7dc9-5d1c-9c95-19d8af0054dc'::uuid, v.id, 'temp_abandono', 'Temperamento · Constancia', '¿Qué es lo más probable que te haga abandonar un programa de salud?', 'single', 'positive', 5, true, NULL, NULL, NULL, NULL, NULL, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'ce34e880-cbbf-5091-bc31-58e90ab19579'::uuid, q.id, 'La monotonía y el aburrimiento', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'temp_abandono' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '78b328ca-93e0-5b00-9e45-c6ee229f649d'::uuid, q.id, 'No ver resultados rápidos', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'temp_abandono' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '00a5dff7-477d-5488-a82e-35571efd27a9'::uuid, q.id, 'Tener demasiada información', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'temp_abandono' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'acff977d-bdae-5432-a8c5-de606c44d028'::uuid, q.id, 'Cambiar mi rutina habitual', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'temp_abandono' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT '34430cbe-0a58-509a-aea2-cc6dcef54030'::uuid, v.id, 'nut_comidas', 'Hábitos · Frecuencia', '¿Cuántas veces al día comes normalmente (incluyendo meriendas)?', 'single', 'positive', 0, true, NULL, NULL, NULL, NULL, NULL, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'nutricional' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '62061685-9c9e-5b88-8efb-78378f8935d7'::uuid, q.id, '1–2 veces', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'nut_comidas' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '8a0be2cb-aba5-539e-9554-83ec55d0575d'::uuid, q.id, '3 veces', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'nut_comidas' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e6120911-9366-55ed-b41d-29339e591bef'::uuid, q.id, '4–5 veces', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'nut_comidas' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'd81255f3-d02d-5ad4-82c9-9e96de03a31f'::uuid, q.id, 'Pico todo el día', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'nut_comidas' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT '473fe561-7d65-5b79-af03-def5d47b4203'::uuid, v.id, 'nut_emocional', 'Conducta · Alimentación emocional', 'Como más de lo planeado cuando estoy estresado/a, triste o ansioso/a.', 'scale', 'reverse', 1, true, NULL, NULL, NULL, NULL, NULL, 'Nunca', 'Siempre'
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'nutricional' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '882c1cb7-6c55-5f10-a90b-41e9f6f23ad5'::uuid, q.id, '1', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'nut_emocional' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '6e0b2ea8-3890-5be5-aed2-12715062166d'::uuid, q.id, '2', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'nut_emocional' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '8e9ed4c5-a7ce-53d0-859c-8dc3fbf37a96'::uuid, q.id, '3', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'nut_emocional' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '26094700-0c20-5dc5-a35a-13085af61af5'::uuid, q.id, '4', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'nut_emocional' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'b52f0cfb-19d7-5def-90e3-afce4a295023'::uuid, q.id, '5', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'nut_emocional' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT 'f0955311-28dc-50f3-b040-b6ab19d9b53b'::uuid, v.id, 'nut_control', 'Conducta · Control de porciones', 'Me cuesta controlar la cantidad de comida que sirvo en mi plato.', 'scale', 'reverse', 2, true, NULL, NULL, NULL, NULL, NULL, 'Nunca', 'Siempre'
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'nutricional' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '6ae72b69-4e23-556b-9072-8e24802d3a0f'::uuid, q.id, '1', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'nut_control' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '1d9faa35-f2f8-5296-a9c7-32bae28380a8'::uuid, q.id, '2', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'nut_control' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'b7846dc5-37a8-5815-8491-d766905df1de'::uuid, q.id, '3', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'nut_control' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'f1f57dde-8b33-510e-af47-c0aceeee26c1'::uuid, q.id, '4', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'nut_control' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '69c5b610-833d-5bca-9d7f-7208aa1da380'::uuid, q.id, '5', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'nut_control' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT 'dda7d016-41cd-544f-91b4-06deaac0e395'::uuid, v.id, 'nut_procesados', 'Hábitos · Ultraprocesados', 'Como alimentos ultraprocesados (snacks, comida rápida, gaseosas) más de 3 veces por semana.', 'scale', 'reverse', 3, true, NULL, NULL, NULL, NULL, NULL, 'Nunca', 'Siempre'
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'nutricional' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '6f8bcaba-1feb-58c9-b96a-2df966f46acd'::uuid, q.id, '1', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'nut_procesados' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'f53052cb-7c84-5c12-bf31-e393ef95d168'::uuid, q.id, '2', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'nut_procesados' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '698feb82-fb8a-5f69-a394-330e5fafc447'::uuid, q.id, '3', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'nut_procesados' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'a2883d58-8366-57e9-9a5a-1438a11c6dd4'::uuid, q.id, '4', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'nut_procesados' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'd2333e4b-cfbd-52e6-95dd-f1c0e1a80bb9'::uuid, q.id, '5', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'nut_procesados' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT 'cedeab2a-837a-5b8f-bfa8-1c29eb3b31e6'::uuid, v.id, 'nut_agua', 'Hidratación · Agua diaria', '¿Cuánta agua tomas al día aproximadamente?', 'single', 'positive', 4, true, NULL, NULL, NULL, NULL, NULL, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'nutricional' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '70d07624-aeea-57b2-ad28-51e42092eaab'::uuid, q.id, 'Menos de 4 vasos', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'nut_agua' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e2846485-7784-52ea-b11a-7d539c4107d1'::uuid, q.id, '4–6 vasos', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'nut_agua' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '028fc6c3-0b22-574a-b0ba-d9b0174087d7'::uuid, q.id, '7–8 vasos', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'nut_agua' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e9ca7fae-7c54-55c0-8b91-23264fc353ca'::uuid, q.id, 'Más de 8 vasos', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'nut_agua' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT 'a0e8b277-18d2-5e54-96eb-b1336029ea18'::uuid, v.id, 'nut_motivacion', 'Motivación · Disposición al cambio', 'Estoy dispuesto/a a cambiar mis hábitos alimenticios si tengo el apoyo adecuado.', 'scale', 'positive', 5, true, NULL, NULL, NULL, NULL, NULL, 'Para nada', 'Totalmente'
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'nutricional' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '4363fb91-1fa1-5fd6-be43-c640752d8b6b'::uuid, q.id, '1', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'nut_motivacion' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '7eb4f4d6-2360-5015-af2e-dbc8c49b43f9'::uuid, q.id, '2', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'nut_motivacion' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '7f0936b0-f65a-518d-8e5b-636fc6e002f7'::uuid, q.id, '3', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'nut_motivacion' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'afffe44d-14c7-580c-a7be-13a54d544cac'::uuid, q.id, '4', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'nut_motivacion' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '78cb419e-800a-5d86-9e18-ae5093966180'::uuid, q.id, '5', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'nut_motivacion' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT '0dada775-10e1-530f-9192-8e0a8bbd0974'::uuid, v.id, 'mov_actual', 'Actividad · Nivel actual', '¿Cómo describes tu nivel de actividad física actualmente?', 'single', 'positive', 0, true, NULL, NULL, NULL, NULL, NULL, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'movimiento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '908d5780-8cf1-5788-8fa7-83e52d3dc6b1'::uuid, q.id, 'Sedentario/a — casi no me muevo', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'mov_actual' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '9f831f62-173e-5854-8cc9-919f8e533396'::uuid, q.id, 'Camino un poco (< 30 min/día)', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'mov_actual' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '98b35f0b-42f6-5248-823c-cccb37a051e4'::uuid, q.id, 'Actividad moderada (30–60 min)', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'mov_actual' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '3bd2a3d6-610a-5f70-acff-608545a38e18'::uuid, q.id, 'Activo/a (ejercicio regular)', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'mov_actual' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT '8facd81f-bdd4-551f-8dce-6b14d0576290'::uuid, v.id, 'mov_fatiga', 'Capacidad · Resistencia', 'Me canso fácilmente al subir escaleras o caminar 10 minutos.', 'scale', 'reverse', 1, true, NULL, NULL, NULL, NULL, NULL, 'Nunca', 'Siempre'
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'movimiento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'a9f44c13-226f-5fea-afde-5007112b495e'::uuid, q.id, '1', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'mov_fatiga' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '82493856-1a1b-5608-8cb8-12299177a30a'::uuid, q.id, '2', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'mov_fatiga' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '4f936f19-2f41-5a19-bc5b-61e366b45ecc'::uuid, q.id, '3', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'mov_fatiga' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'f428bb06-d17e-54a6-b1e0-a00aa4652281'::uuid, q.id, '4', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'mov_fatiga' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '1e4e82b5-9c7b-5864-9b1f-c17b9b9dd5ca'::uuid, q.id, '5', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'mov_fatiga' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT 'd68ce262-08f2-5dc8-9133-3f6cb2243967'::uuid, v.id, 'mov_dolor', 'Limitaciones · Dolor', '¿Tienes dolor articular o muscular que limita tu movimiento?', 'single', 'positive', 2, true, NULL, NULL, NULL, NULL, NULL, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'movimiento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '6a9345e5-84bb-5b05-a6aa-8e86228b2201'::uuid, q.id, 'No, me muevo sin dolor', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'mov_dolor' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '8a4d570f-3717-51d0-a6cc-df544ba1c551'::uuid, q.id, 'Dolor leve ocasional', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'mov_dolor' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'f85beb9b-4529-5fae-8577-444b7ae46286'::uuid, q.id, 'Dolor frecuente que me limita', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'mov_dolor' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT '2950ff41-6950-53a5-a416-2dc4a88cfc89'::uuid, v.id, 'mov_tiempo', 'Disponibilidad · Tiempo', '¿Cuánto tiempo puedes dedicar al ejercicio por día?', 'single', 'positive', 3, true, NULL, NULL, NULL, NULL, NULL, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'movimiento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '6a4b0bf7-82d2-54a3-b822-ac1b60ddd512'::uuid, q.id, '5–10 minutos', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'mov_tiempo' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'a4d40acb-adc0-536e-8b21-3573c96d6737'::uuid, q.id, '15–20 minutos', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'mov_tiempo' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5b8927aa-6f39-59e0-a9c4-2dd83988acbc'::uuid, q.id, '30–45 minutos', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'mov_tiempo' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '617d87b9-d6f8-5a6a-bb37-883aeb4eb940'::uuid, q.id, 'Más de 45 minutos', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'mov_tiempo' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT 'a46e627b-b711-58c1-9ac5-a07724164dc5'::uuid, v.id, 'sue_horas', 'Sueño · Horas por noche', '¿Cuántas horas duermes normalmente por noche?', 'single', 'positive', 0, true, NULL, NULL, NULL, NULL, NULL, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'sueno' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '1d6ed65c-123c-5a18-8ea1-70487843d1ea'::uuid, q.id, 'Menos de 5 horas', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'sue_horas' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '25d8fd87-66f4-5298-a36f-726370a0dbd0'::uuid, q.id, '5–6 horas', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'sue_horas' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '4c987d90-a1f8-516b-af3a-1eb727549778'::uuid, q.id, '6–7 horas', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'sue_horas' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '7d0baba1-6b15-5296-9708-e10f091abf9c'::uuid, q.id, '7–9 horas', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'sue_horas' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT '2104046c-d691-500b-b6ec-4499deb318ac'::uuid, v.id, 'sue_calidad', 'Sueño · Calidad', 'Me despierto descansado/a y con energía para empezar el día.', 'scale', 'positive', 1, true, NULL, NULL, NULL, NULL, NULL, 'Nunca', 'Siempre'
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'sueno' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '6c9ddcda-2db4-5747-884c-b3108c65c6f7'::uuid, q.id, '1', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'sue_calidad' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '818b6726-166a-5faf-9d57-cfc4d5f55ff6'::uuid, q.id, '2', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'sue_calidad' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '43688e7e-484a-525d-a99f-eb5121503354'::uuid, q.id, '3', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'sue_calidad' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5701f8bb-0208-5b70-8db7-0ec33821a8f9'::uuid, q.id, '4', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'sue_calidad' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '9b8d449a-d209-53aa-a75d-ebf1ce750ac8'::uuid, q.id, '5', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'sue_calidad' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT '5e454e9a-5e06-5420-81af-5fe7951357a9'::uuid, v.id, 'sue_ronquidos', 'Red flags · Ronquidos', '¿Te han dicho que roncas fuerte o que paras de respirar mientras duermes?', 'single', 'positive', 2, true, NULL, NULL, NULL, NULL, NULL, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'sueno' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '677fb101-b1df-52e2-9e0d-74b1db71463f'::uuid, q.id, 'No, nunca', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'sue_ronquidos' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'd3344c29-82f2-5f4e-b51c-d068ae8943e6'::uuid, q.id, 'A veces', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'sue_ronquidos' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '81d855ac-8524-5248-b0ab-1e1bf40dbf3e'::uuid, q.id, 'Sí, frecuentemente', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'sue_ronquidos' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT 'faf173b5-7072-594c-bdef-44e0f67ee326'::uuid, v.id, 'sue_somnolencia', 'Red flags · Somnolencia diurna', 'Me da mucho sueño durante el día, aunque haya dormido mis horas.', 'scale', 'reverse', 3, true, NULL, NULL, NULL, NULL, NULL, 'Nunca', 'Siempre'
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'sueno' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '87524f13-e04e-5029-b5a3-1cff7b955a97'::uuid, q.id, '1', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'sue_somnolencia' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5a4fc726-3578-557e-b6ff-ca9d1bf19c4e'::uuid, q.id, '2', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'sue_somnolencia' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'f5fb1d2c-751c-5e4f-be89-8d76a3a607b6'::uuid, q.id, '3', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'sue_somnolencia' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '3c7223be-6051-5927-ac1a-e84d6ee62068'::uuid, q.id, '4', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'sue_somnolencia' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0725953c-560e-5e60-8701-1cc3ad7672c8'::uuid, q.id, '5', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'sue_somnolencia' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT '9c0de0f6-b70e-5cbe-9694-d3605d537c43'::uuid, v.id, 'adh_compromiso', 'Compromiso · Motivación', 'Estoy dispuesto/a a cambiar mis hábitos aunque sea difícil al principio.', 'scale', 'positive', 0, true, NULL, NULL, NULL, NULL, NULL, 'Para nada', 'Totalmente'
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'iac-adresd' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '10030c4a-c004-5ede-9b2e-47dec14b23b9'::uuid, q.id, '1', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'adh_compromiso' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '49437a13-44ba-56cf-a447-e053939184b6'::uuid, q.id, '2', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'adh_compromiso' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '4af37682-799c-5f51-a39d-49e11f865a1b'::uuid, q.id, '3', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'adh_compromiso' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0937a86f-d66a-56ba-a648-49ec5f950efc'::uuid, q.id, '4', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'adh_compromiso' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '9e4c9b4f-2a73-5331-b75e-70ff2f925ea0'::uuid, q.id, '5', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'adh_compromiso' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT '628c11bf-9b6c-52be-8747-d67b1339cc3c'::uuid, v.id, 'adh_constancia', 'Compromiso · Constancia', 'Cuando empiezo un programa o tratamiento, lo termino aunque sea duro.', 'scale', 'positive', 1, true, NULL, NULL, NULL, NULL, NULL, 'Nunca', 'Siempre'
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'iac-adresd' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'abfd9914-5a8d-5542-9517-6f42ac373627'::uuid, q.id, '1', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'adh_constancia' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '3128d372-b006-5f6b-b22b-9efa3a89e47a'::uuid, q.id, '2', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'adh_constancia' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0dcb043f-d348-5d89-9143-e3f2330918c8'::uuid, q.id, '3', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'adh_constancia' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '8a037954-942c-5e03-90ac-88e97d31f18a'::uuid, q.id, '4', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'adh_constancia' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'd59bfaad-61e7-508c-a330-48a8f5512a58'::uuid, q.id, '5', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'adh_constancia' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT '2bc6d57c-c00d-57ea-b84e-9ea8c58766a2'::uuid, v.id, 'adh_barreras', 'Barreras · Obstáculos reales', '¿Qué podría impedirte seguir el programa con constancia?', 'multi', 'positive', 2, true, 'Sé honesto/a, esto nos ayuda a personalizar tu apoyo.', NULL, NULL, NULL, NULL, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'iac-adresd' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5532845f-5c31-53af-99ff-131b69dbf535'::uuid, q.id, 'Falta de tiempo', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'adh_barreras' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'd69e0cf8-a6d2-5532-906a-35e013ca22f0'::uuid, q.id, 'Costo económico', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'adh_barreras' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '64d5a799-d68b-52c6-8efe-92c6a4f3a6d9'::uuid, q.id, 'Desmotivación', 1, 2, true
FROM app.health_test_questions q WHERE q.code = 'adh_barreras' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'f1b35b3b-2d84-5efc-8a1f-51b26212e014'::uuid, q.id, 'Responsabilidades familiares', 1, 3, true
FROM app.health_test_questions q WHERE q.code = 'adh_barreras' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'ce154f11-2b87-5119-b2e1-0375ce4f40c1'::uuid, q.id, 'Trabajo o estudio', 1, 4, true
FROM app.health_test_questions q WHERE q.code = 'adh_barreras' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'edfc59ee-a6dd-5e59-a7af-01f1c8a3d6c1'::uuid, q.id, 'Ninguna — estoy listo/a', 0, 5, true
FROM app.health_test_questions q WHERE q.code = 'adh_barreras' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT '22146fcc-319a-5250-88c1-bdd011990be7'::uuid, v.id, 'adh_proposito', 'Propósito · Razón principal', '¿Cuál es tu razón más importante para unirte al programa?', 'single', 'positive', 3, true, NULL, NULL, NULL, NULL, NULL, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'iac-adresd' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '75d6d60e-90ea-5211-8998-28b316929486'::uuid, q.id, 'Controlar mi enfermedad', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'adh_proposito' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '445c6d9c-1daa-5136-bd38-35420eec7e27'::uuid, q.id, 'Bajar de peso', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'adh_proposito' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '432c2b27-0bcc-5dea-be17-120c3083f212'::uuid, q.id, 'Tener más energía', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'adh_proposito' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '611ad304-8227-56b6-bdba-57cac5a274e4'::uuid, q.id, 'Por mi familia', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'adh_proposito' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '03216682-0f5e-5ab5-9db2-bac11ec2c505'::uuid, q.id, 'Sentirme mejor conmigo mismo/a', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'adh_proposito' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT '238179d5-f105-5b38-92a4-96436238ef10'::uuid, v.id, 'card_hba1c', 'Laboratorios · HbA1c (si la tienes)', '¿Cuál fue tu último resultado de hemoglobina glicosilada (HbA1c)?', 'single', 'positive', 0, true, 'Si no la tienes, selecciona "No la sé".', NULL, NULL, NULL, NULL, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'orp' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '9bde25e0-992e-557d-8f0c-ed5581060bd7'::uuid, q.id, 'Menos de 5.7%', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'card_hba1c' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '94f4dfb3-52f8-5932-8d1d-db17caeecd82'::uuid, q.id, '5.7% – 6.4%', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'card_hba1c' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '4d893e77-7858-54c5-9d62-7562ee6aa675'::uuid, q.id, '6.5% o más', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'card_hba1c' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '329d4f17-2411-58c9-b06c-49c5dcd6a0ca'::uuid, q.id, 'No la sé', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'card_hba1c' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT 'f4d24d95-ec00-51ed-a955-1ada616bd806'::uuid, v.id, 'card_pa', 'Tensión arterial · Última medición', '¿Cuál es tu presión arterial habitual?', 'single', 'positive', 1, true, NULL, NULL, NULL, NULL, NULL, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'orp' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'b80ecf77-cb62-5a58-8945-e3bd88725ae2'::uuid, q.id, 'Normal (< 130/80)', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'card_pa' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'c7e7cfa4-bdf8-5f22-a8d6-477cb2c6106b'::uuid, q.id, 'Elevada (130–140 / 80–90)', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'card_pa' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5ef01bce-4120-5a91-99e8-0cd7057cd60a'::uuid, q.id, 'Alta (> 140/90)', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'card_pa' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5fa6f282-8a69-584e-a53d-12dd5936cba0'::uuid, q.id, 'No la sé', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'card_pa' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT '83a7e1cc-0f98-5dd5-b0e3-22db44e38bf4'::uuid, v.id, 'card_tabaco', 'Hábitos · Tabaquismo', '¿Fumas actualmente o has fumado en los últimos 5 años?', 'single', 'positive', 2, true, NULL, NULL, NULL, NULL, NULL, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'orp' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e818e587-69a7-5ae7-9dcf-ed47987b0fb2'::uuid, q.id, 'Nunca he fumado', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'card_tabaco' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '026b7177-fbc4-5458-822d-7cff8539cb63'::uuid, q.id, 'Fumé pero lo dejé', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'card_tabaco' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'baa7281a-513b-5231-8b0b-56a67c1806ad'::uuid, q.id, 'Sí, fumo actualmente', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'card_tabaco' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT 'a45581dd-8d9d-50d4-b2cd-066efcb1ffec'::uuid, v.id, 'card_colesterol', 'Laboratorios · Colesterol', '¿Tu médico te ha dicho que tienes colesterol o triglicéridos altos?', 'single', 'positive', 3, true, NULL, NULL, NULL, NULL, NULL, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'orp' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '4e2e76ee-3a12-523b-af20-2045af89e017'::uuid, q.id, 'No, están en rango normal', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'card_colesterol' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'd539c13f-6501-55ea-a0ae-6922421813f7'::uuid, q.id, 'Sí, pero controlado con dieta', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'card_colesterol' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '47060b49-b8df-5b22-8ccb-44abd1bdde50'::uuid, q.id, 'Sí, tomo medicamento', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'card_colesterol' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '75b12ceb-3421-55f2-9182-28d46a911bb7'::uuid, q.id, 'No lo sé', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'card_colesterol' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT '67b31f9b-0745-50f9-85e2-ff982003f968'::uuid, v.id, 'ers_familia', 'Familia · Ambiente en casa', 'El ambiente en mi hogar es tranquilo y me siento apoyado/a.', 'scale', 'positive', 0, true, NULL, NULL, NULL, NULL, NULL, 'Para nada', 'Totalmente'
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'ers' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'c296581b-f2a2-5ce6-9c17-2db0d968c4a5'::uuid, q.id, '1', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'ers_familia' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'b3d9ea2c-99fd-594b-abed-0e0293a9d59e'::uuid, q.id, '2', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'ers_familia' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'db110b13-a473-5201-98b1-e90750a0cc68'::uuid, q.id, '3', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'ers_familia' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '1c41c114-9b13-5892-9a48-5512e683ccfd'::uuid, q.id, '4', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'ers_familia' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'ce72d7e0-6b6d-5a8d-8b2e-266221767e60'::uuid, q.id, '5', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'ers_familia' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT '1f09ad6c-586f-5c1b-9c03-5f6e8eac838b'::uuid, v.id, 'ers_trabajo', 'Trabajo · Carga laboral', 'Mi trabajo o estudio me genera estrés frecuente o sobrecarga.', 'scale', 'reverse', 1, true, NULL, NULL, NULL, NULL, NULL, 'Nunca', 'Siempre'
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'ers' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5d34930a-3060-54fd-829f-d88bf1e01c42'::uuid, q.id, '1', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'ers_trabajo' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '4869b224-9756-540f-b169-fb7a172a15c4'::uuid, q.id, '2', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'ers_trabajo' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '908fd705-14d0-5a7d-a8f6-98001c265137'::uuid, q.id, '3', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'ers_trabajo' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '459a843a-f2cf-5b69-ae4d-684aab348a1b'::uuid, q.id, '4', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'ers_trabajo' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'dcf524d6-c5b2-5dec-9d22-3824a0ed74b6'::uuid, q.id, '5', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'ers_trabajo' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT 'b6c1f753-53ec-52ed-a912-8aa7c6e6509b'::uuid, v.id, 'ers_apoyo', 'Red de apoyo · Apoyo externo', '¿Tienes alguien (familia, amigo, pareja) que te acompañe en este proceso?', 'single', 'positive', 2, true, NULL, NULL, NULL, NULL, NULL, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'ers' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '07a10313-0aa9-5eba-a6cb-3dd5092feb2c'::uuid, q.id, 'Sí, tengo apoyo sólido', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'ers_apoyo' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '544d2e9b-0de6-5f78-b17e-32aa2323f5c8'::uuid, q.id, 'Algo de apoyo', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'ers_apoyo' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'adc9eaa5-e5ce-5236-86f9-42de7dc82e92'::uuid, q.id, 'Lo hago prácticamente solo/a', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'ers_apoyo' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT '29c583e8-9153-5707-914f-5d7ee78c5ad8'::uuid, v.id, 'ers_tiempo', 'Gestión · Tiempo personal', 'Logro reservar tiempo para mí mismo/a en medio de mis responsabilidades.', 'scale', 'positive', 3, true, NULL, NULL, NULL, NULL, NULL, 'Nunca', 'Siempre'
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'ers' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'af3362d5-10f8-5a55-ae0a-4c764a8b02b3'::uuid, q.id, '1', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'ers_tiempo' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'f9a53608-b854-5f56-a464-2156adf84ca8'::uuid, q.id, '2', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'ers_tiempo' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'ae3ded8b-e477-5458-a2cc-a1fe2f1995c5'::uuid, q.id, '3', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'ers_tiempo' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5d1f262f-5308-5652-8ff8-b84213003dd7'::uuid, q.id, '4', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'ers_tiempo' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'b040ff5b-1394-58a8-bb9f-d3a621eb10b2'::uuid, q.id, '5', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'ers_tiempo' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT '8eae5632-1c9c-54f9-91cc-42356299af5d'::uuid, v.id, 'prop_urgencia', 'Propósito · Urgencia del cambio', 'Siento que necesito cambiar mis hábitos ahora, no más adelante.', 'scale', 'positive', 0, true, NULL, NULL, NULL, NULL, NULL, 'Para nada', 'Totalmente'
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'bateria-antares' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0b06b29e-692f-5fda-92ea-23305fca7d51'::uuid, q.id, '1', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'prop_urgencia' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '34a1f79c-1582-5a31-af6c-f9b1e2e25265'::uuid, q.id, '2', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'prop_urgencia' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0737576b-6dee-5eee-a7aa-ab96a02a348c'::uuid, q.id, '3', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'prop_urgencia' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'b295abba-689b-5f99-a2d3-1843ec1f2f8a'::uuid, q.id, '4', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'prop_urgencia' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'fe415de3-104c-5ac4-8336-ccc7cb87e680'::uuid, q.id, '5', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'prop_urgencia' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT '18b9d07b-6659-50da-ae41-34607c34cd03'::uuid, v.id, 'prop_creencia', 'Mentalidad · Posibilidad de cambio', 'Creo que puedo mejorar mi salud con el esfuerzo y el apoyo correctos.', 'scale', 'positive', 1, true, NULL, NULL, NULL, NULL, NULL, 'No lo creo', 'Totalmente'
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'bateria-antares' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '1386f9d7-f431-540f-8b0e-57b57a1d3727'::uuid, q.id, '1', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'prop_creencia' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '8f13a757-d2ea-5edc-9969-50a770b175aa'::uuid, q.id, '2', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'prop_creencia' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'b771d852-e54a-5bf8-9c2c-2d9490bbcac9'::uuid, q.id, '3', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'prop_creencia' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'ecafa648-dc8e-5b70-83df-7455991a0fc6'::uuid, q.id, '4', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'prop_creencia' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5fb40885-55d3-5769-9a4f-db5630620695'::uuid, q.id, '5', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'prop_creencia' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT '03e27e27-633a-5a76-8a3c-baa75371ef72'::uuid, v.id, 'prop_plazo', 'Expectativas · Plazo esperado', '¿En cuánto tiempo esperas ver resultados concretos en tu salud?', 'single', 'positive', 2, true, NULL, NULL, NULL, NULL, NULL, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'bateria-antares' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '71774b51-e111-5324-8fb2-f660456c3fd2'::uuid, q.id, 'Menos de 1 mes', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'prop_plazo' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0834cc4a-b1a5-5244-aaf1-650d25211c05'::uuid, q.id, '2–3 meses', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'prop_plazo' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '06fac8f1-01ab-560b-bd93-d84d4003e287'::uuid, q.id, '6 meses', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'prop_plazo' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '81a15d61-a98a-587c-be27-0a1508a14ca6'::uuid, q.id, 'Es un proceso de vida', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'prop_plazo' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT 'bf953f1a-d1c5-5309-9034-b32f29ccea1d'::uuid, v.id, 'prop_meta', 'Meta · Objetivo principal', 'Si en 6 meses logras UN solo resultado, ¿cuál quieres que sea?', 'single', 'positive', 3, true, NULL, NULL, NULL, NULL, NULL, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'bateria-antares' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '8775e280-d5eb-5859-afc8-c67f6e3627d4'::uuid, q.id, 'Glucosa o HbA1c en rango normal', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'prop_meta' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e0f19f89-732f-545a-9e6e-468152bf519b'::uuid, q.id, 'Bajar al menos 5 kg de peso', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'prop_meta' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'b7aa0331-a483-590f-834e-22b7ab761afd'::uuid, q.id, 'Reducir o eliminar medicamentos', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'prop_meta' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e50ad1a2-ba54-5058-8e22-8dc6235bde00'::uuid, q.id, 'Tener energía para disfrutar el día', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'prop_meta' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '2578086c-5aeb-5cce-8164-928cd84a6350'::uuid, q.id, 'Sentirme mejor emocionalmente', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'prop_meta' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active, hint, unit, min_value, max_value, default_value, min_label, max_label)
SELECT '5284d3e6-e1cf-5b15-8787-a38e138dbd1a'::uuid, v.id, 'prop_nota', 'Nota personal · Para tu equipo médico', 'Cuéntanos con tus propias palabras: ¿qué te trajo al programa Copp Adresd?', 'open', 'positive', 4, true, 'El Dr. Godoy Cruz leerá esto personalmente. Sé tan honesto/a como puedas.', NULL, NULL, NULL, NULL, NULL, NULL
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'bateria-antares' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;

-- Rangos de interpretación por versión.
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT 'f1007249-b462-5536-b5c7-aac2a0ba2b4f'::uuid, v.id, 0, 3, 'bajo', 'low', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'historia-clinica' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT 'b19436f9-adb2-5d5a-96ff-9aeaa711937e'::uuid, v.id, 4, 7, 'moderado', 'moderate', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'historia-clinica' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '74487812-48aa-5d78-8581-3d4cf04b453a'::uuid, v.id, 8, 11, 'alto', 'high', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'historia-clinica' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT 'e8870f59-74b7-5c1f-af14-916cba9f3676'::uuid, v.id, 12, 17, 'critico', 'critical', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'historia-clinica' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '868a8bee-c70f-548d-bbed-0038e84790e0'::uuid, v.id, 0, 7, 'bajo', 'low', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '7773cd9a-b16e-53cb-af69-38e42438df13'::uuid, v.id, 8, 14, 'moderado', 'moderate', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT 'aa77594c-3bdd-57e1-b525-f83d05bbd644'::uuid, v.id, 15, 22, 'alto', 'high', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '2f299022-e9fc-5fa9-a883-1bd3f0569cf4'::uuid, v.id, 0, 7, 'bajo', 'low', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'nutricional' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '79b5576e-c279-50a5-bb10-fb2b62f7bc98'::uuid, v.id, 8, 14, 'moderado', 'moderate', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'nutricional' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT 'be5cf079-c807-5a2c-aa2a-3c7efba783ca'::uuid, v.id, 15, 22, 'alto', 'high', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'nutricional' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '4069210e-e5d3-5e66-80ee-d957949bcde8'::uuid, v.id, 0, 4, 'bajo', 'low', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'movimiento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '30789bbb-09dd-5673-9e92-f3bf1201d8a2'::uuid, v.id, 5, 8, 'moderado', 'moderate', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'movimiento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '460e02c0-0484-578c-85f1-6a5d4a5cc91b'::uuid, v.id, 9, 12, 'alto', 'high', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'movimiento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '5f1b2dd0-9c12-51a4-bd45-76255c9fd4da'::uuid, v.id, 0, 4, 'bajo', 'low', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'sueno' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '4463de42-ce41-5556-86eb-161e0c4f631c'::uuid, v.id, 5, 9, 'moderado', 'moderate', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'sueno' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT 'b6462553-d190-539b-8f52-791510eafb08'::uuid, v.id, 10, 13, 'alto', 'high', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'sueno' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '23ff382e-276a-5272-9eea-eb9df43648b1'::uuid, v.id, 0, 39, 'alto', 'high', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'iac-adresd' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT 'dec92ed1-05d3-53ca-94a2-783a014cb8aa'::uuid, v.id, 40, 69, 'moderado', 'moderate', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'iac-adresd' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT 'e6dec258-4770-54ea-ba74-91b565e2b3e4'::uuid, v.id, 70, 100, 'bajo', 'low', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'iac-adresd' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '8098b67c-e360-5b61-98e9-2783ce535604'::uuid, v.id, 0, 24, 'bajo', 'low', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'orp' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '3ccafa00-1cfe-50f8-a8c8-0ed083f1ffe7'::uuid, v.id, 25, 49, 'moderado', 'moderate', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'orp' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '8c972277-e0bc-562e-9de3-8438b773c33f'::uuid, v.id, 50, 74, 'alto', 'high', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'orp' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '06aa9388-1392-57a4-9331-768ef2b360cf'::uuid, v.id, 75, 100, 'critico', 'critical', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'orp' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '9786a828-1501-5dfc-8a1b-4cf009054dba'::uuid, v.id, 0, 4, 'bajo', 'low', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'ers' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT 'b73d3dee-8a40-5f24-9420-74239d0a8b4a'::uuid, v.id, 5, 9, 'moderado', 'moderate', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'ers' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '0c83a942-7ed6-5e54-9787-a642edbe4f95'::uuid, v.id, 10, 14, 'alto', 'high', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'ers' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT 'e8aec319-a938-5d26-9c83-fb67ad27b568'::uuid, v.id, 0, 5, 'bajo', 'low', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'bateria-antares' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT 'fab3ce78-765d-5ed1-9800-1afbf0142d0d'::uuid, v.id, 6, 10, 'moderado', 'moderate', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'bateria-antares' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '01d5d163-8d02-5081-8bfd-3a2979ab0641'::uuid, v.id, 11, 15, 'alto', 'high', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'bateria-antares' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_batteries (id, code, name, description, auto_assign_on_patient_create, is_active, created_at)
VALUES ('0d0198a4-ade2-50af-a6d4-e5fe29096d41'::uuid, 'bateria-inicial', 'Batería de evaluación inicial Copp Adresd', 'Evaluación inicial del programa (9 tests del onboarding)', true, true, now())
ON CONFLICT (code) DO NOTHING;

INSERT INTO app.health_test_battery_items (id, battery_id, instrument_id, version_id, sort_order, is_required, frequency_days)
SELECT gen_random_uuid(), b.id, i.id, v.id, b_item.pos, true, NULL
FROM (VALUES
    ('historia-clinica', 1),
    ('temperamento', 2),
    ('nutricional', 3),
    ('movimiento', 4),
    ('sueno', 5),
    ('iac-adresd', 6),
    ('orp', 7),
    ('ers', 8),
    ('bateria-antares', 9)
) AS b_item(code, pos)
JOIN app.health_test_batteries b ON b.code = 'bateria-inicial'
JOIN app.health_test_instruments i ON i.code = b_item.code
JOIN app.health_test_versions v ON v.instrument_id = i.id AND v.is_current = true
WHERE NOT EXISTS (SELECT 1 FROM app.health_test_battery_items bi WHERE bi.battery_id = b.id AND bi.instrument_id = i.id)
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_indicator_defs (id, code, name, description, computation, is_active, created_at)
SELECT * FROM (VALUES
    ('db15ba04-66b6-5a9d-9550-195c02dfccaa'::uuid, 'iadherencia', 'Índice de adherencia IAC-ADRESD', 'Indicador derivado: Índice de adherencia IAC-ADRESD', '{"formula":"weighted","sources":[{"resultType":"subscale","code":"Compromiso · Motivación"},{"resultType":"subscale","code":"Compromiso · Constancia"},{"resultType":"subscale","code":"Barreras · Obstáculos reales"},{"resultType":"subscale","code":"Propósito · Razón principal"}]}'::jsonb, true, now()),
    ('ba1c2658-464c-595f-9947-76ea9e52f0f2'::uuid, 'iapnea', 'Sospecha de apnea del sueño', 'Indicador derivado: Sospecha de apnea del sueño', '{"formula":"sum","sources":[{"resultType":"subscale","code":"Red flags · Ronquidos"}]}'::jsonb, true, now())
) AS t(id, code, name, description, computation, is_active, created_at)
ON CONFLICT (code) DO NOTHING;

INSERT INTO app.health_test_alert_rules (id, code, name, condition, severity, message_template, is_active, created_at)
SELECT * FROM (VALUES
    ('7a8213b2-9487-53fb-bc0b-2f95ad706f71'::uuid, 'orp_riesgo_alto', 'Riesgo cardiometabólico alto', '{"when":{"resultType":"score","code":"orp","severity":["high","critical"]}}'::jsonb, 'high', 'ORP {value} - {label}: riesgo cardiometabólico elevado. Revisar prioridad.', true, now()),
    ('19632f6a-a4f1-5f9e-8a59-9726e302595d'::uuid, 'iapnea_sospecha', 'Sospecha de apnea del sueño', '{"when":{"resultType":"indicator","code":"iapnea","severity":["high"]}}'::jsonb, 'high', 'Sospecha de apnea: {value} señales de riesgo. Evaluar estudio de sueño.', true, now()),
    ('a7fe02c1-49d5-5d7a-b8ba-1271fd43c38f'::uuid, 'adherencia_baja', 'Adherencia baja al programa', '{"when":{"resultType":"indicator","code":"iadherencia","severity":["high"]}}'::jsonb, 'moderate', 'Adherencia IAC-ADRESD baja ({value}%). Requiere acompañamiento.', true, now())
) AS t(id, code, name, condition, severity, message_template, is_active, created_at)
ON CONFLICT (code) DO NOTHING;
