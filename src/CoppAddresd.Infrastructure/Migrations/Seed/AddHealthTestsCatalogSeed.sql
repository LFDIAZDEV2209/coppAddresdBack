-- Seed del módulo Tests de Salud: batería inicial ANTARES.
-- Idempotente (ON CONFLICT DO NOTHING sobre claves únicas).
-- Generado por scripts/generate_health_tests_seed.py — editar el script, no este SQL.

INSERT INTO app.health_test_instruments (id, code, name, description, category, sort_order, is_active, created_at)
SELECT * FROM (VALUES
    ('9726aaac-f747-59cb-8daa-71b37cbdb057'::uuid, 'historia-clinica', 'Historia clínica biológica', 'Antecedentes · Examen físico · Sistemas', 'clinico', 1, true, now()),
    ('2c8e7171-ef3d-525f-8277-f83558009609'::uuid, 'temperamento', 'Test de temperamento', 'Sanguíneo · Colérico · Melancólico · Flemático', 'psicologico', 2, true, now()),
    ('924d0bae-5355-507e-a51d-152094d879a8'::uuid, 'nutricional', 'Test nutricional y hábitos', 'Alimentación · Conducta · Motivación', 'nutricion', 3, true, now()),
    ('c624b36d-3880-572d-b084-fcdd2f67edf2'::uuid, 'movimiento', 'Movimiento y actividad física', 'AMAF · Nivel funcional · Capacidad', 'movimiento', 4, true, now()),
    ('ab263023-fce0-5376-a648-9fe3bb101725'::uuid, 'sueno', 'Caracterización del sueño', 'Duración · Calidad · Hábitos · Riesgos', 'sueno', 5, true, now()),
    ('cd48e94c-c326-5312-8040-f44d2f4001d4'::uuid, 'iac-adresd', 'Índice de adherencia IAC-ADRESD', 'Motivación · Autoeficacia · Compromiso', 'adherencia', 6, true, now()),
    ('b322c457-2853-5baf-a66f-0e255c7c725e'::uuid, 'orp', 'Riesgo cardiometabólico ORP', 'OMS · Obesidad · Complicaciones · Riesgo', 'clinico', 7, true, now()),
    ('da092b10-061e-59e6-ad36-abb6bc71643e'::uuid, 'ers', 'Test de estrés relacional ERS', 'Familia · Pareja · Trabajo · Entorno social', 'psicologico', 8, true, now()),
    ('caa212ce-0b3e-5c5f-a5bb-8c796166e1c7'::uuid, 'bateria-antares', 'Batería inicial completa ANTARES', 'PHS · Propósito · Mentalidad · Perfil final', 'integral', 9, true, now())
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

-- Preguntas de escala con sus opciones.
INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '98873855-117b-59b8-871e-526363740200'::uuid, v.id, 'temperamento_00', '🔴 Sanguíneo', 'Me resulta fácil iniciar conversación con personas que no conozco.', 'scale', 'positive', 0, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'cddbe19a-e6d4-5279-b852-4982c5fc3004'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_00' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '4a2981ec-5d90-517d-8a7d-f9914726140a'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_00' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '25a5c2dc-41e5-5289-bc51-9ab0194671e0'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_00' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '16aef632-cbcf-5809-8fd5-5b3eaf307ac0'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_00' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'c5359607-1148-5d76-ad06-463807fb38a5'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_00' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'ec04dcb6-2250-5654-9ff1-e9b289a39533'::uuid, v.id, 'temperamento_01', '🔴 Sanguíneo', 'Disfruto estar rodeado de personas y participar en actividades sociales.', 'scale', 'positive', 1, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '878cfadb-c582-5379-b819-c28314c77067'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_01' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '2dbff712-ca56-58c7-99f5-a88f79c8ee36'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_01' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '8d06f4ba-06f3-581f-afde-be7cb8124302'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_01' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5aaaa766-a9da-577d-b45f-a1ff72006aa4'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_01' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '21f4ed0c-94fd-5593-a5ac-81914ab08131'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_01' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'f448ee96-2939-558d-951d-29a1035e444b'::uuid, v.id, 'temperamento_02', '🔴 Sanguíneo', 'Expreso fácilmente mis emociones.', 'scale', 'positive', 2, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'c47e057b-78fe-5f50-afcd-95246d9972b5'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_02' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '4f0edb0f-63bc-5716-baa6-2fffd289810b'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_02' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '2a2e6ef2-d793-5730-a86d-ed5acbbb6566'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_02' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '6fb8a69f-0258-5e04-81b7-ee4a56f5a345'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_02' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '40b5b606-53ad-521b-aac0-0c73d81bc62f'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_02' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '9be35d7c-e805-5941-b277-2cf4dc172822'::uuid, v.id, 'temperamento_03', '🔴 Sanguíneo', 'Suelo entusiasmarme rápidamente con nuevas ideas o proyectos.', 'scale', 'positive', 3, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '7ebf87e2-efb6-585a-9530-44b1b949e7dc'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_03' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e9b3dd0f-0cb2-5c79-ab01-698d71fa1f11'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_03' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '7c95301f-6317-5a29-9704-f019a41acf4f'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_03' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'd34eace1-3985-50eb-a9b8-72002aaddc71'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_03' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '847b3445-7e64-589e-ae2e-2d4c8f16c264'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_03' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'c4a6ba44-0adc-50e1-ab46-eb90a6817b60'::uuid, v.id, 'temperamento_04', '🔴 Sanguíneo', 'Me gusta contar historias y hacer reír a los demás.', 'scale', 'positive', 4, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5da818f0-9073-5fae-a010-cebd7f0539bb'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_04' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'dfd13e7a-9abb-5985-afab-f977d5ff8bc4'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_04' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'bc3181f8-725d-596f-95ac-6b16d4367eb1'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_04' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '2a720cc7-f82f-546f-bcb1-a6bbb060b347'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_04' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '3e9299b3-85d1-502b-b571-8db8fa5a1249'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_04' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '41591986-7dc9-5d1c-9c95-19d8af0054dc'::uuid, v.id, 'temperamento_05', '🔴 Sanguíneo', 'Me aburro cuando algo se vuelve demasiado rutinario.', 'scale', 'positive', 5, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '78b328ca-93e0-5b00-9e45-c6ee229f649d'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_05' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '00a5dff7-477d-5488-a82e-35571efd27a9'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_05' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'acff977d-bdae-5432-a8c5-de606c44d028'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_05' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '53d37685-eac5-595d-89e8-fd9494c50c20'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_05' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5347fc22-b2df-5af1-9bf1-074324efe8c7'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_05' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'dd7e003a-677f-5b28-9421-9a5ec6b565de'::uuid, v.id, 'temperamento_06', '🔴 Sanguíneo', 'Suelo actuar espontáneamente.', 'scale', 'positive', 6, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'd5d24644-91eb-5bb9-97b1-337dc9db4ba5'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_06' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'beef3e6f-999d-5861-9e46-0a0b49ee64ca'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_06' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '1ce4f404-160b-590d-b71c-090115c4df34'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_06' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '094910ed-07d8-5071-ac1c-b5c6b8308f19'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_06' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'ca73d310-d0d1-55c4-9821-52001038fdf2'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_06' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '69833362-6f34-5a84-8a3b-fd238d5bac7a'::uuid, v.id, 'temperamento_07', '🔴 Sanguíneo', 'Cuando estoy motivado, contagio mi entusiasmo a otros.', 'scale', 'positive', 7, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '246c6941-f280-5d03-b283-eb1af142f62b'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_07' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '7d8193de-3246-5f1a-b0b6-92d39dffc9ff'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_07' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '7d1ef85a-9864-587b-b517-1dd56585b69b'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_07' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'a6534c15-7764-5030-9980-933b92898198'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_07' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '97f3bcd0-dc2f-5102-b9da-8eb7f7ade2a9'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_07' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '5f674e23-b476-5aef-a695-43f9276e379c'::uuid, v.id, 'temperamento_08', '🟠 Colérico', 'Cuando tengo un objetivo, hago todo lo posible por alcanzarlo.', 'scale', 'positive', 8, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '81bd7ec2-5511-520c-9603-79a5ba93cc88'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_08' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0b70dea9-c920-5f5e-b85c-f7082a0bddef'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_08' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'c7d3fe01-970d-53e3-b76c-89590c55bb86'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_08' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '8cdf40a2-d092-51ed-8f0f-92c02feffc5f'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_08' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '96f6fc6a-ef5c-56fb-af3a-60d2e74f5ed3'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_08' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'e7f86c10-292d-54cc-a916-0543e6fccde6'::uuid, v.id, 'temperamento_09', '🟠 Colérico', 'Me gusta tomar decisiones y asumir el liderazgo.', 'scale', 'positive', 9, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5c7e8fb2-9b23-59b0-b545-56c8573c7b17'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_09' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '1c705d38-0143-5dc4-be43-dda55ca98089'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_09' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '7963d86a-bedc-5925-bddd-a6d1f53d3c6a'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_09' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e515cac7-0fae-5ac2-8d1e-a70a64d211a2'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_09' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '59eddf82-ff2b-5727-a62e-9fe668d9d93b'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_09' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '5d51aeac-01d8-5d62-a7a1-cb9ebd5b00b7'::uuid, v.id, 'temperamento_10', '🟠 Colérico', 'Me impaciento cuando las cosas avanzan muy lentamente.', 'scale', 'positive', 10, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'de26e25d-3dab-552c-9fdf-9f6d1626d2d4'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_10' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '09759e66-d955-56a9-a84f-8e7ace6e81ff'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_10' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '37b0a08b-0bc4-5252-950a-ac9722c1e6a8'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_10' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '1b9e93a9-6778-5072-b78e-972dcc186c4d'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_10' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '1fb96676-debf-5d10-a0e7-bd25263ede5f'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_10' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '0b06998f-8f86-5b13-a534-2d8dc2e8e696'::uuid, v.id, 'temperamento_11', '🟠 Colérico', 'Suelo decir directamente lo que pienso.', 'scale', 'positive', 11, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '72369155-dd87-54c4-bdd5-bb9967a621c4'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_11' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '4158d479-3c9c-5b81-bf6b-de1eb770713e'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_11' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '705a7333-133b-5fa6-a98f-819669a44959'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_11' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'de4c3fe2-f898-5063-a645-9a9afdd946de'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_11' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'a31df409-3ebd-54e9-9556-873f3615afef'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_11' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'c1d8c48c-75fe-5a39-8f5c-ab2ce0c4199f'::uuid, v.id, 'temperamento_12', '🟠 Colérico', 'Los obstáculos me hacen esforzarme todavía más.', 'scale', 'positive', 12, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '1d158216-909b-569f-9589-f65413a8a377'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_12' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '7b02547e-e509-5062-ba23-5f7c895ebc8e'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_12' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '1efcdbc1-70b7-52bc-b3dd-79624241ed40'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_12' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '6d730c61-2bd2-5043-9f23-0f55c342de63'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_12' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'f68cda00-9cdb-5e87-b26e-90a23b3ce1d5'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_12' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'e0c11536-12c0-53ae-be1a-a339c873e7a9'::uuid, v.id, 'temperamento_13', '🟠 Colérico', 'Me gusta competir y superar desafíos.', 'scale', 'positive', 13, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'cf0c2663-649b-561e-a122-86d6d62fdf14'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_13' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '8e8faf84-10c4-50c1-8f62-eaa69085a5de'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_13' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'a3fae402-2ec0-5bac-98cb-38a4616e2db4'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_13' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'ec9fdb40-9a78-514c-8c00-aefdb02f053a'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_13' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'a44b3483-2bc4-5174-a2be-a9ce765c4dce'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_13' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'f21ec350-6baa-565e-b9f5-5828884bbef4'::uuid, v.id, 'temperamento_14', '🟠 Colérico', 'Prefiero actuar antes que quedarme analizando.', 'scale', 'positive', 14, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'c31ca61a-98bb-517d-82bc-c16585dd12a5'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_14' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'fe908cb5-9791-5ea6-ba11-dc45c1e15aa1'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_14' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '95e8b93a-596a-53f6-8a65-e7cb5ff4e0e1'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_14' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'f592da89-65a4-51c7-8ee4-ccb84907b8e7'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_14' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5f1128ce-d75e-5187-802a-7ed085aa44d6'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_14' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '45bb9d0a-b02a-50a4-bc89-b683e6b6cbcf'::uuid, v.id, 'temperamento_15', '🟠 Colérico', 'Cuando algo no funciona, busco rápidamente una solución.', 'scale', 'positive', 15, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '1b71c8af-02b1-5da2-84b9-c701804e6a36'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_15' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0e31f2cd-d281-559e-8063-ba3880560997'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_15' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0c2f97da-21d5-57fa-8822-837e9a0dd018'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_15' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'a0e98f34-e4ec-5a4b-a94e-e7784612c537'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_15' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'bf32f93d-0394-5d85-8989-e67451c79a1b'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_15' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '13add193-69e0-5840-945c-442f8b045877'::uuid, v.id, 'temperamento_16', '🔵 Melancólico', 'Antes de decidir, analizo cuidadosamente las posibilidades.', 'scale', 'positive', 16, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0c0c943e-8757-5d85-b2c0-a48b4736adb1'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_16' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e68159df-f918-5a29-9409-257f7520d558'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_16' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '699e376e-6224-555d-bcfa-03fa955a1962'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_16' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'c195bb7b-092f-5da7-91f5-cdb06550cdb5'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_16' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '6fdcc03b-db88-5e3b-b9b9-318d09c003a2'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_16' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'c3d75b3f-87da-5d2b-8b93-ea9a85a7d80f'::uuid, v.id, 'temperamento_17', '🔵 Melancólico', 'Me fijo mucho en los detalles.', 'scale', 'positive', 17, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '6b360afe-7a11-58e1-a162-790925ed8893'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_17' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e874873f-ac15-5142-b40d-be6a8ce3ad18'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_17' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'b8017b6c-6371-5183-a02d-ed55a7cd8113'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_17' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'f2d4dc39-a3d6-5306-8c61-ef0155d36fa8'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_17' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '34181966-cf91-5f6a-a5b7-bc55eb3c4746'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_17' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '21260dc2-a607-5f66-8f82-234f2757dd48'::uuid, v.id, 'temperamento_18', '🔵 Melancólico', 'Me preocupa hacer las cosas correctamente.', 'scale', 'positive', 18, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e4e61571-e355-5d53-8e5b-0dc3b2a379c2'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_18' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'f81dbc4d-ddcc-5f5d-bcf8-2e4d290d54e1'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_18' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'f07d14c6-fd97-5eb5-9ac1-486d1ff45cd7'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_18' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '7f5aca3a-e2b0-5eb6-9ed3-d3c280ff5f20'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_18' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '80d946da-4663-5952-959b-afa61ebaa1f6'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_18' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '55525c26-dd64-5467-9ff2-2581a363f8e3'::uuid, v.id, 'temperamento_19', '🔵 Melancólico', 'Soy exigente conmigo mismo/a.', 'scale', 'positive', 19, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'ad1e400d-ece4-502d-87aa-19056d3b712f'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_19' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '09df785c-654d-56a8-a3d3-8919ae8139d6'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_19' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '480127b4-0e62-5240-85eb-6a635509c798'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_19' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '7924ff54-77ee-5125-9012-f60ac6558b51'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_19' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'fdbd8f75-44b7-583f-8ea9-78e6c40f5cae'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_19' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '0dc64ec6-6acc-5342-ac15-e677c40ae54c'::uuid, v.id, 'temperamento_20', '🔵 Melancólico', 'Recuerdo con facilidad situaciones que me afectaron emocionalmente.', 'scale', 'positive', 20, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '7adb22da-51aa-5c9f-9762-4d024c17926f'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_20' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '45df45db-0026-55cc-b1e9-bbe68c959018'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_20' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e3c0bd3b-dcfc-5b11-b337-05658b8efceb'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_20' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '289393a9-c8ac-50c9-839a-7cad793ede69'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_20' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '77cd0c3e-c7c4-5c88-8281-229478c25b1a'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_20' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '25ae3f74-0b70-5640-a530-2cce30656783'::uuid, v.id, 'temperamento_21', '🔵 Melancólico', 'Necesito comprender profundamente cómo y por qué funcionan las cosas.', 'scale', 'positive', 21, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '837bcb3b-0ae8-55e1-8ee6-4b972e8d8ca0'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_21' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'bc565f5c-66ef-59c1-a57d-8266d633c3ff'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_21' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e0a91428-f181-5423-9d7f-0345e8a61ecc'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_21' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '7fb8d34c-c13a-5d3a-9be7-69e0ac3ba0c4'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_21' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '8c1f3df3-8ca0-5846-8d8f-6f3607091f84'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_21' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '5df13686-df56-5fed-bd6f-37f927a4ba6e'::uuid, v.id, 'temperamento_22', '🔵 Melancólico', 'Me molesta cometer errores que podrían haberse evitado.', 'scale', 'positive', 22, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '003d125e-bd40-5347-a885-63877de6faa5'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_22' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '6a642722-ef09-566f-90f1-7e88c5436f2e'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_22' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '98193514-ab25-564d-9922-8c7c426f8570'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_22' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '666453d0-528c-59eb-9884-c773051487a5'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_22' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'cd58f941-5c52-5113-a214-88273b1dfd54'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_22' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '80ad868b-8e5b-530d-a494-09bcdcabffa3'::uuid, v.id, 'temperamento_23', '🔵 Melancólico', 'Suelo pensar mucho antes de actuar.', 'scale', 'positive', 23, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '420ba04f-deaa-5016-b9e8-95dfedc14200'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_23' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'df922344-253a-55d1-b6a8-873ca5487cd8'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_23' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '8d62a01c-f15d-5381-8242-f9529b10d59d'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_23' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '2cf5eea6-8ebb-51ec-b8db-e0759e18c628'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_23' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '6b242c97-2e92-592c-a7b2-0c69dc8a469a'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_23' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '237fefa5-c502-58e4-a528-a60ee88d3ed7'::uuid, v.id, 'temperamento_24', '🟢 Flemático', 'Mantengo la calma incluso cuando otros están alterados.', 'scale', 'positive', 24, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '6dcd6088-2a09-5849-ac3c-dd99c4d71e49'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_24' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '09969187-42f4-57fa-a517-c4eadf99c624'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_24' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '8ebb6d45-70c3-5b66-80c7-69126c0375c9'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_24' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '058c39ea-d6b4-5052-9792-76635a11b5b6'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_24' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '32dbc5f9-6804-5546-ac7f-9926650ee002'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_24' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '62442303-fb7d-5294-9940-1b68b314a476'::uuid, v.id, 'temperamento_25', '🟢 Flemático', 'Prefiero evitar discusiones y conflictos innecesarios.', 'scale', 'positive', 25, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '18080b8e-1557-58b9-86fc-1d08ecae71e2'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_25' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '7383d568-6c37-5149-9664-d9c5bf53cfcc'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_25' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '88acb851-4a70-5c21-bd39-57bd9b4061be'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_25' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '3242626a-8b54-5546-aa6d-caa1a7bc772e'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_25' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '376e9e1c-0dd2-5f40-b3cd-5b9bcb18b834'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_25' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'eb120b8d-7016-58ce-8eff-e32c89236606'::uuid, v.id, 'temperamento_26', '🟢 Flemático', 'Soy paciente con otras personas.', 'scale', 'positive', 26, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0f94fb01-7c9f-5399-8b6b-0442d405f9fe'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_26' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'f44d2346-0b0d-55bf-acc1-edf4e539c690'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_26' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '01ffc98b-55ad-5ef2-bf8b-de51d8054b77'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_26' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'ce5b73a9-07d8-55fa-8378-2be90d30c601'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_26' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '6b25651a-2824-5ad0-aab3-ac91a4d54a94'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_26' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '5dbeb436-aa19-56ac-97b3-9cf089b81fb5'::uuid, v.id, 'temperamento_27', '🟢 Flemático', 'Me adapto bien a diferentes personalidades.', 'scale', 'positive', 27, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'ef2ba100-e1bd-5292-9e4d-37eebd7ac52d'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_27' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'd72e94ad-cdea-58f5-adc7-9b19667ad1fb'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_27' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5c85cf64-a15c-5e4b-8f27-20dea0ba6fdb'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_27' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'fcde8ddb-8d64-5970-bfd1-c9f3f91d7617'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_27' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '858b0013-9dca-55ae-84e6-c6d33bd488f8'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_27' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '22127daf-d5fa-5e87-b646-699a531d17ac'::uuid, v.id, 'temperamento_28', '🟢 Flemático', 'Valoro la estabilidad y la tranquilidad.', 'scale', 'positive', 28, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'b7d18cc5-edfa-50d7-8940-f1fb55434e47'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_28' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '2802372f-63e1-5da5-b552-8d4a49703340'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_28' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '071f86b5-55ea-5809-8f32-4e5255f2e3f7'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_28' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e55cdbd6-5e33-543f-8d58-544c044ff758'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_28' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '8d3aebb1-73ea-56e5-81ba-77f4a689b2d7'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_28' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'd247d494-8257-599a-8925-b446ce0cf220'::uuid, v.id, 'temperamento_29', '🟢 Flemático', 'Prefiero escuchar antes que hablar.', 'scale', 'positive', 29, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'fc971e95-8dc6-5fce-bc53-5d42c99e7286'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_29' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '9674112d-d95d-540b-ab2e-a9c15061ea4b'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_29' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '6d3dbfdb-e571-58fb-8f52-badd959a21ca'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_29' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '552da5fd-fc9f-5dbe-a277-e47e306714f6'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_29' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '4e1a81fa-481b-5d00-946c-0eb4e5238a6d'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_29' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '93e99d98-5477-5f1c-ba51-d9ff177617bc'::uuid, v.id, 'temperamento_30', '🟢 Flemático', 'Las situaciones de presión rara vez me hacen perder el control.', 'scale', 'positive', 30, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '4a3ce6d3-6079-5f4a-b334-b10b98933b00'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_30' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e46e4ed7-5ad6-59e5-b355-0aeef48cdeb9'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_30' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '1bbaa530-c769-5559-9039-ae8b4d0d92d6'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_30' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0193594f-b4e7-5b76-8efd-c4aecaeadabe'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_30' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '52c665c3-7633-5c78-af6f-2127a75eee10'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_30' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '1bc88700-504e-5913-817d-4c1e712ddcdf'::uuid, v.id, 'temperamento_31', '🟢 Flemático', 'Las personas acuden a mí cuando necesitan ser escuchadas.', 'scale', 'positive', 31, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '328b6309-9520-53b5-8e4f-483c438241ac'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_31' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '9f352530-fb7f-5f6a-8b31-ce960e3cc720'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_31' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0512c3cc-cf65-51bd-81dc-90b702571b8b'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_31' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '001ce2ab-cddf-5f2e-985e-f9d2d7a4d154'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_31' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '562f876c-720a-5af8-a18a-d1aa9d468dfb'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'temperamento_31' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '34430cbe-0a58-509a-aea2-cc6dcef54030'::uuid, v.id, 'nutricional_00', '🥦 Calidad alimentaria', 'Incluyo verduras u hortalizas en mis comidas principales.', 'scale', 'positive', 0, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'nutricional' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '8a0be2cb-aba5-539e-9554-83ec55d0575d'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_00' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e6120911-9366-55ed-b41d-29339e591bef'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_00' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'd81255f3-d02d-5ad4-82c9-9e96de03a31f'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_00' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '253a1ef6-a6bb-554d-b150-733da48f5364'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_00' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5cbe8d0b-58f2-5391-b06e-1edbc10460fd'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_00' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '473fe561-7d65-5b79-af03-def5d47b4203'::uuid, v.id, 'nutricional_01', '🥦 Calidad alimentaria', 'Consumo frutas regularmente.', 'scale', 'positive', 1, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'nutricional' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '6e0b2ea8-3890-5be5-aed2-12715062166d'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_01' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '8e9ed4c5-a7ce-53d0-859c-8dc3fbf37a96'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_01' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '26094700-0c20-5dc5-a35a-13085af61af5'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_01' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'b52f0cfb-19d7-5def-90e3-afce4a295023'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_01' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'b18436e2-d275-55bb-a51a-b3b219ff8c1f'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_01' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'f0955311-28dc-50f3-b040-b6ab19d9b53b'::uuid, v.id, 'nutricional_02', '🥦 Calidad alimentaria', 'Consumo fuentes de proteína de buena calidad diariamente.', 'scale', 'positive', 2, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'nutricional' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '1d9faa35-f2f8-5296-a9c7-32bae28380a8'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_02' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'b7846dc5-37a8-5815-8491-d766905df1de'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_02' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'f1f57dde-8b33-510e-af47-c0aceeee26c1'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_02' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '69c5b610-833d-5bca-9d7f-7208aa1da380'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_02' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'b3ac4836-3462-5610-b4c1-bfdfc5a2c0d7'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_02' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'dda7d016-41cd-544f-91b4-06deaac0e395'::uuid, v.id, 'nutricional_03', '🥦 Calidad alimentaria', 'Consumo alimentos ricos en fibra (legumbres, cereales integrales).', 'scale', 'positive', 3, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'nutricional' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'f53052cb-7c84-5c12-bf31-e393ef95d168'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_03' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '698feb82-fb8a-5f69-a394-330e5fafc447'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_03' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'a2883d58-8366-57e9-9a5a-1438a11c6dd4'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_03' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'd2333e4b-cfbd-52e6-95dd-f1c0e1a80bb9'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_03' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'c5ad9233-91cf-5aed-9d52-b0bb6f311d89'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_03' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'cedeab2a-837a-5b8f-bfa8-1c29eb3b31e6'::uuid, v.id, 'nutricional_04', '🥦 Calidad alimentaria', 'Prefiero alimentos mínimamente procesados frente a ultraprocesados.', 'scale', 'positive', 4, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'nutricional' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e2846485-7784-52ea-b11a-7d539c4107d1'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_04' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '028fc6c3-0b22-574a-b0ba-d9b0174087d7'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_04' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e9ca7fae-7c54-55c0-8b91-23264fc353ca'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_04' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'f30a7c10-f1f1-5b06-9c92-d5602aaad12b'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_04' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '1197fd81-d4ee-575b-8447-217edd51df9f'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_04' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'a0e8b277-18d2-5e54-96eb-b1336029ea18'::uuid, v.id, 'nutricional_05', '⏰ Organización', 'Mantengo horarios relativamente regulares para mis comidas.', 'scale', 'positive', 5, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'nutricional' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '7eb4f4d6-2360-5015-af2e-dbc8c49b43f9'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_05' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '7f0936b0-f65a-518d-8e5b-636fc6e002f7'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_05' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'afffe44d-14c7-580c-a7be-13a54d544cac'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_05' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '78cb419e-800a-5d86-9e18-ae5093966180'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_05' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '43fe5993-3cb5-5ccb-aefb-2b4db249c303'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_05' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '55e2b1d6-d209-5cba-bfce-d48d2cb28d61'::uuid, v.id, 'nutricional_06', '⏰ Organización', 'Planifico con anticipación lo que voy a comer.', 'scale', 'positive', 6, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'nutricional' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e56840c7-15c3-5a23-b092-53ca7687ab40'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_06' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '3826fcce-0b18-5f85-b7f2-a6927d9c0fb3'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_06' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'ccaa9d65-2af0-5dc1-92be-b1fbc1acfd76'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_06' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '49336cce-21d9-5921-92dd-f7f64823b1b8'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_06' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '7a46ddfb-0981-5ce4-b859-afb3daec8823'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_06' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '5f1a1835-6a16-5a57-8169-265cbc22c289'::uuid, v.id, 'nutricional_07', '⏰ Organización', 'Como sentado/a y presto atención a lo que estoy comiendo.', 'scale', 'positive', 7, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'nutricional' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'fc12a4c4-3322-5e78-901e-ac07f51e1cda'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_07' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '15ecb42e-3192-5d78-96ad-d7bbfb3125a1'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_07' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '050ed17d-7799-59f7-aaad-c173786a5031'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_07' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'cea717a1-2bb5-53be-a358-446cdfb437c3'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_07' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '653df720-9332-518c-b6a5-b30158ac5a82'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_07' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '05e550ac-c550-59f9-8635-5dea9b6f8676'::uuid, v.id, 'nutricional_08', '⏰ Organización', 'Puedo reconocer cuándo estoy satisfecho/a y detenerme.', 'scale', 'positive', 8, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'nutricional' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5823db13-707e-5150-a70f-ed27b3c76589'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_08' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '2f983eb5-a43e-5671-80a1-30de9350a8d3'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_08' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'dcf6b4ec-9a08-55ff-8847-17dd753219b0'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_08' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'a71a09cf-fd91-54f5-ab0e-646ea9a02f5f'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_08' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '4c71c646-c23b-5f0c-ace5-e678b6862082'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_08' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '1cc588d9-d4a9-509b-bfd1-48d0b3553e90'::uuid, v.id, 'nutricional_09', '⚠ Conductas de riesgo', 'Consumo bebidas azucaradas o snacks ultraprocesados con frecuencia.', 'scale', 'reverse', 9, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'nutricional' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'a88fb728-7f67-5f2b-8c69-731f46a021ca'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_09' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'a640033a-8838-5621-b0ed-5a306e9ca8af'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_09' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e6bbca05-e3ba-5385-9fff-34539c9b2fec'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_09' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '1d034e0c-b229-52db-826f-ddef4aed50c3'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_09' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0599ffdb-1ed4-5d7f-b5fd-e1fb70887653'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_09' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '2eca6854-41e9-57a1-971f-c0bd9f3f0a26'::uuid, v.id, 'nutricional_10', '⚠ Conductas de riesgo', 'Cuando estoy estresado/a o triste, aumento mi consumo de comida.', 'scale', 'reverse', 10, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'nutricional' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'd1dcc28f-b1c0-5ecb-931f-dd1e947290ef'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_10' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '85cbc00d-cd03-57e2-961c-7c7bc516ef6d'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_10' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'c51576d4-82de-528e-b701-54227a6b239a'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_10' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'cc815ec1-c243-5231-95c6-54a21e820224'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_10' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '9ca881e8-0e67-5219-89bd-12ee2367f164'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_10' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '2628c563-eda8-5d26-88f1-af1ece973b5b'::uuid, v.id, 'nutricional_11', '⚠ Conductas de riesgo', 'Tengo dificultad para controlar porciones de ciertos alimentos.', 'scale', 'reverse', 11, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'nutricional' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '35d7433a-c0ad-5925-a167-4e5a7ee58f7b'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_11' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '09fbc658-38b1-5813-a723-778faeccd4a8'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_11' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'd1aa7341-944b-5c44-beb3-ef8248b2d49c'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_11' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '03e00278-4412-590f-9b89-d10c72a483a4'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_11' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e701f4e6-e2fd-5b99-8ade-14439f27e199'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_11' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '49bac430-df19-517a-a4e7-93714f55f4dd'::uuid, v.id, 'nutricional_12', '💧 Hidratación', 'Bebo agua regularmente durante el día.', 'scale', 'positive', 12, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'nutricional' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e920113b-fcd7-5639-abbe-796ac1552d02'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_12' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'a767b64a-2ed7-581a-8f25-13d60137709d'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_12' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'bc816872-0325-54d1-9a5a-73834d081101'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_12' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e7cb8c0b-2a1d-530e-9e1e-f88a1a2c65bf'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_12' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'b76aa3b2-d10b-5d9a-89d1-d7f7486747a5'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_12' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'bdabb562-5a92-5b19-b4f8-402cabf545a9'::uuid, v.id, 'nutricional_13', '🏠 Entorno', 'En mi casa hay alimentos que facilitan una alimentación saludable.', 'scale', 'positive', 13, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'nutricional' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '1e1427a0-6352-52ee-9a1d-cb10de647ff9'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_13' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'b9e45361-117f-5b79-a9ad-d1d14de695ef'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_13' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '81d93299-9ffc-59c6-b643-a92155308c53'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_13' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '40643939-4742-5195-bf20-58b7148f9ab0'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_13' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'c9aebb80-5c03-5393-bc56-ab7fe32556e6'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_13' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'b154a277-fc2f-58fd-92a7-a77e239fb85a'::uuid, v.id, 'nutricional_14', '🎯 Motivación', 'Tengo una razón personal importante para mejorar mi alimentación.', 'scale', 'positive', 14, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'nutricional' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'f92b774b-476f-5142-b072-988184d0abff'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_14' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '211f8b4b-4951-526e-aebf-9e2654ca438c'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_14' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '869394e2-39ec-528c-8c74-6a5668b4995e'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_14' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'd047eece-075a-5320-842d-b6924ae278e9'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_14' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'f9664b45-94ca-5254-825a-b57614dbc5eb'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_14' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'ed9eb7db-ea51-59f1-acaa-db5cce50558e'::uuid, v.id, 'nutricional_15', '🎯 Motivación', 'Estoy dispuesto/a a modificar algunos hábitos alimentarios.', 'scale', 'positive', 15, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'nutricional' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0cfb620d-b1b2-50c2-a8f8-6c2b6856f339'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_15' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '8001a0b0-93f0-5d81-8a42-c05f64bc8fea'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_15' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'cbac1995-7180-5d2a-b3da-b6fa3f23e084'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_15' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '6993afeb-dddb-5a4c-8f62-8dfb4ee184b5'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_15' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '6ba23928-5676-5c59-b334-d622934d453c'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_15' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '95ea1b36-03ca-51de-94c9-b589e84a79a1'::uuid, v.id, 'nutricional_16', '🎯 Motivación', 'Cuando fracaso en un hábito, puedo volver a intentarlo.', 'scale', 'positive', 16, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'nutricional' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'f42412bc-2184-5a26-9f2c-15bb0351fd6c'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_16' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '82c88029-6356-56c5-b03f-b9a5be33b52b'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_16' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '04e40d30-37d5-52ba-bc6b-5ba45606cbde'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_16' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '01c00bbc-e2d9-53ec-a9cd-064392375a5e'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_16' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '6d85964f-b52b-5c5a-83f3-3657f86b817f'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'nutricional_16' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '0dada775-10e1-530f-9192-8e0a8bbd0974'::uuid, v.id, 'movimiento_00', '🟣 Movilidad', 'Puedo cambiar de posición en la cama.', 'scale', 'positive', 0, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'movimiento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '908d5780-8cf1-5788-8fa7-83e52d3dc6b1'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_00' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '9f831f62-173e-5854-8cc9-919f8e533396'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_00' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '98b35f0b-42f6-5248-823c-cccb37a051e4'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_00' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '3bd2a3d6-610a-5f70-acff-608545a38e18'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_00' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '8facd81f-bdd4-551f-8dce-6b14d0576290'::uuid, v.id, 'movimiento_01', '🟣 Movilidad', 'Puedo sentarme en la cama con o sin ayuda.', 'scale', 'positive', 1, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'movimiento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'a9f44c13-226f-5fea-afde-5007112b495e'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_01' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '82493856-1a1b-5608-8cb8-12299177a30a'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_01' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '4f936f19-2f41-5a19-bc5b-61e366b45ecc'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_01' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'f428bb06-d17e-54a6-b1e0-a00aa4652281'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_01' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'd68ce262-08f2-5dc8-9133-3f6cb2243967'::uuid, v.id, 'movimiento_02', '🔵 Sedestación', 'Puedo permanecer sentado/a durante 5 minutos.', 'scale', 'positive', 2, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'movimiento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '6a9345e5-84bb-5b05-a6aa-8e86228b2201'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_02' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '8a4d570f-3717-51d0-a6cc-df544ba1c551'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_02' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'f85beb9b-4529-5fae-8577-444b7ae46286'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_02' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0c56edc4-7d61-5117-a731-dc5449966125'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_02' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '2950ff41-6950-53a5-a416-2dc4a88cfc89'::uuid, v.id, 'movimiento_03', '🔵 Sedestación', 'Puedo incorporarme de una silla con seguridad.', 'scale', 'positive', 3, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'movimiento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '6a4b0bf7-82d2-54a3-b822-ac1b60ddd512'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_03' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'a4d40acb-adc0-536e-8b21-3573c96d6737'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_03' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5b8927aa-6f39-59e0-a9c4-2dd83988acbc'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_03' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '617d87b9-d6f8-5a6a-bb37-883aeb4eb940'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_03' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '84d4c4b6-af46-5683-bea3-76360e9966d5'::uuid, v.id, 'movimiento_04', '🟢 Bipedestación', 'Puedo permanecer de pie durante 1 minuto.', 'scale', 'positive', 4, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'movimiento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5e11c32f-d775-58aa-bf81-3cc4705d9a0e'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_04' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0a723f91-e90a-5f3b-98b2-ee765ff7ecb4'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_04' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'c91d248d-aa64-5d5d-8505-257de0149d28'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_04' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '31a90fa3-19a6-57bc-8e66-ea6281078e45'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_04' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '537281af-6c30-5b21-9697-5c55d43a0788'::uuid, v.id, 'movimiento_05', '🟢 Bipedestación', 'Puedo mantener el equilibrio estando de pie.', 'scale', 'positive', 5, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'movimiento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'a5966966-5026-5b4f-aea8-c684cb61dff5'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_05' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '4d5f143c-5507-5809-a12b-8ac42cb478c7'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_05' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'f2c79c69-cbe8-5114-98b2-48b6a3106194'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_05' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '252bb145-933a-587f-b526-aacc5caefaaa'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_05' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '7fcba89a-39c2-5b44-9eb6-7e4cfe3980ce'::uuid, v.id, 'movimiento_06', '🟡 Marcha', 'Puedo caminar dentro de mi vivienda.', 'scale', 'positive', 6, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'movimiento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '69007bf8-1d66-541b-bd53-3fb88fa89f97'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_06' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '8b804b8f-2ca4-564e-b0ba-e5c2866f03ef'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_06' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '24f908d2-cbd0-54d8-8983-ca96b06a359e'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_06' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '61459f63-3a41-5e69-b579-6d8bbbda418f'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_06' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '17ef8c8f-6ba3-5f34-a107-384295be182c'::uuid, v.id, 'movimiento_07', '🟡 Marcha', 'Puedo caminar durante 5 minutos sin detenerme.', 'scale', 'positive', 7, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'movimiento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '15bb4ccc-4e66-599b-a822-9b72f7bcf371'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_07' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '31c721e5-f3f8-5121-bac5-d09d7dc8614b'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_07' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e0aefb96-db9e-5670-adef-48aded09122c'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_07' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '3b877c10-5e63-5a73-8d5c-7a47820a3585'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_07' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '8a0517eb-61f5-526c-94b0-7fceeb825e80'::uuid, v.id, 'movimiento_08', '🟡 Marcha', 'Puedo caminar fuera de casa.', 'scale', 'positive', 8, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'movimiento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '2d329c88-3092-582c-bcfc-ea0614c66f06'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_08' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '65312ebf-512d-5b93-aa45-f6f4a82de206'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_08' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'ccff2836-706f-5326-a599-13eab36d77f6'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_08' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'eed48139-9cea-5fbb-a799-0269a500cff5'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_08' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'd83f033c-249a-59b7-ab66-d6489519cd2f'::uuid, v.id, 'movimiento_09', '🟠 Capacidad funcional', 'Puedo realizar actividades domésticas básicas.', 'scale', 'positive', 9, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'movimiento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '7f76add4-210b-5f6d-b7a4-8f6ca03dded6'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_09' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'ea8dbd9c-70e5-5ebe-8f7e-fb1a3b662338'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_09' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'c8d169f1-eb48-543f-9fdf-38a22376f434'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_09' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '009556d0-5d58-543c-bd63-33e3774a54df'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_09' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'e18e3e55-c137-525b-906b-c155c196403c'::uuid, v.id, 'movimiento_10', '🔴 Autopercepción', 'Tengo confianza para moverme.', 'scale', 'positive', 10, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'movimiento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '2de73b5a-4038-5568-b066-e65a6dbaf45a'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_10' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '9d8dda47-f1e4-540b-b955-e620613325f0'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_10' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '8da9cc45-419a-5996-a61f-3c5dc658b169'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_10' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'd2e9f99c-68a6-5d25-8bf4-455e1c44a6ba'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_10' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'eeeee08e-eb8e-5167-baa0-d27e4ad95d45'::uuid, v.id, 'movimiento_11', '🔴 Autopercepción', 'Estoy dispuesto/a a realizar actividad física diariamente.', 'scale', 'positive', 11, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'movimiento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '45c854c2-4652-58a6-9519-8e5dcd70eb34'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_11' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0c52490e-034b-54bd-a63d-438a43cd9fdb'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_11' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '2e6ee5ce-6d78-566d-a9ae-a75bfb965804'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_11' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'fe903f51-ed22-5fdf-9ae9-e13cd1ff142d'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'movimiento_11' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'a46e627b-b711-58c1-9ac5-a07724164dc5'::uuid, v.id, 'sueno_00', '⏰ Regularidad', 'Me acuesto aproximadamente a la misma hora todos los días.', 'scale', 'positive', 0, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'sueno' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '1d6ed65c-123c-5a18-8ea1-70487843d1ea'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'sueno_00' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '25d8fd87-66f4-5298-a36f-726370a0dbd0'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'sueno_00' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '4c987d90-a1f8-516b-af3a-1eb727549778'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'sueno_00' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '7d0baba1-6b15-5296-9708-e10f091abf9c'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'sueno_00' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '04eeae9f-2006-5063-8e2c-cad38ecca32f'::uuid, q.id, '4', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'sueno_00' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '2104046c-d691-500b-b6ec-4499deb318ac'::uuid, v.id, 'sueno_01', '⏰ Regularidad', 'Me levanto aproximadamente a la misma hora todos los días.', 'scale', 'positive', 1, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'sueno' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '6c9ddcda-2db4-5747-884c-b3108c65c6f7'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'sueno_01' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '818b6726-166a-5faf-9d57-cfc4d5f55ff6'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'sueno_01' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '43688e7e-484a-525d-a99f-eb5121503354'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'sueno_01' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5701f8bb-0208-5b70-8db7-0ec33821a8f9'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'sueno_01' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '9b8d449a-d209-53aa-a75d-ebf1ce750ac8'::uuid, q.id, '4', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'sueno_01' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '5e454e9a-5e06-5420-81af-5fe7951357a9'::uuid, v.id, 'sueno_02', '🛏 Duración', 'Duermo el tiempo que necesito para sentirme descansado/a.', 'scale', 'positive', 2, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'sueno' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '677fb101-b1df-52e2-9e0d-74b1db71463f'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'sueno_02' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'd3344c29-82f2-5f4e-b51c-d068ae8943e6'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'sueno_02' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '81d855ac-8524-5248-b0ab-1e1bf40dbf3e'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'sueno_02' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '9adf2748-6086-51c7-b3e6-6c776189166f'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'sueno_02' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '651d920a-e232-5e7f-80ca-2a54609cd1a1'::uuid, q.id, '4', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'sueno_02' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'faf173b5-7072-594c-bdef-44e0f67ee326'::uuid, v.id, 'sueno_03', '🛏 Duración', 'Me duermo con relativa facilidad.', 'scale', 'positive', 3, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'sueno' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '87524f13-e04e-5029-b5a3-1cff7b955a97'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'sueno_03' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5a4fc726-3578-557e-b6ff-ca9d1bf19c4e'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'sueno_03' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'f5fb1d2c-751c-5e4f-be89-8d76a3a607b6'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'sueno_03' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '3c7223be-6051-5927-ac1a-e84d6ee62068'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'sueno_03' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0725953c-560e-5e60-8701-1cc3ad7672c8'::uuid, q.id, '4', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'sueno_03' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'fc8f5369-6345-5e93-ad58-0f1bc25e830a'::uuid, v.id, 'sueno_04', '🌅 Funcionamiento diurno', 'Me despierto sintiéndome descansado/a.', 'scale', 'positive', 4, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'sueno' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '43e6a4d8-9d8d-5a52-a55c-d960a74d0883'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'sueno_04' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '4976f782-1b53-5078-a1e6-8bd1b2931db9'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'sueno_04' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '6d97abf1-11c0-5d30-b65f-f50d8c9ee2ea'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'sueno_04' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e864d947-5e0c-5948-8397-4880146ca60a'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'sueno_04' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'bb7987a6-5569-563b-9d28-68c5907fc883'::uuid, q.id, '4', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'sueno_04' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '4bc06809-881e-551e-9e42-9b92b2e5e33c'::uuid, v.id, 'sueno_05', '🌅 Funcionamiento diurno', 'Tengo energía suficiente durante el día.', 'scale', 'positive', 5, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'sueno' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'd9522748-4f81-519e-b8ea-cd82b5a1bb76'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'sueno_05' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '3a212d1a-3635-5f63-b754-f09296f119dd'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'sueno_05' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '87fc88a0-344c-5a6e-b50f-dd5f7837d4c0'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'sueno_05' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0317c604-8764-5506-abd5-b75a8533a73c'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'sueno_05' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'f1974bd6-3eb4-548f-b0e6-dc23080535c0'::uuid, q.id, '4', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'sueno_05' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'ec1a926e-ff66-5616-851b-f0ee501b29b5'::uuid, v.id, 'sueno_06', '📱 Hábitos', 'Evito el teléfono inmediatamente antes de dormir.', 'scale', 'positive', 6, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'sueno' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '6d4472c4-0f1d-5d53-b686-ddf743b9645a'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'sueno_06' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '3236a2f0-5fde-5c18-b505-5b9363bb579f'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'sueno_06' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '32dc5d5f-8714-52a7-bca7-ae719ba19769'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'sueno_06' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '93b88ae6-919b-5c51-952a-c229dfd9c0dd'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'sueno_06' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '8166e268-bbc5-59c8-8dec-16964adccd80'::uuid, q.id, '4', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'sueno_06' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '10cfdbf1-ae2e-5628-8a8a-5c0ff6a335f1'::uuid, v.id, 'sueno_07', '📱 Hábitos', 'Evito la cafeína varias horas antes de acostarme.', 'scale', 'positive', 7, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'sueno' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'df4ba444-3e8c-5ec7-b92a-946722f08512'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'sueno_07' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'f0d08c54-ee84-5cea-aa53-83f4a5dc5577'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'sueno_07' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '603bc140-1951-5bac-b5ca-f20f29f93af7'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'sueno_07' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'd61fbe15-11da-5ade-be51-d0530ed2a217'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'sueno_07' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '4f823464-d34c-5b7d-a12b-77d27334c4a5'::uuid, q.id, '4', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'sueno_07' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '9f570a42-eb01-56b6-ae8c-cafe096dfd30'::uuid, v.id, 'sueno_08', '🧠 Desconexión', 'Puedo dejar de pensar en mis responsabilidades a la hora de dormir.', 'scale', 'positive', 8, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'sueno' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '694b1144-d21f-5c22-a54e-ea0c356d2a98'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'sueno_08' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'cff00a3e-4b16-5011-b8af-52cafb52af91'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'sueno_08' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '7e30b9d3-65de-5367-b3c9-c189937ae0c0'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'sueno_08' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5c679ab1-2a33-5308-ab0e-c2a45a8416ae'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'sueno_08' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '8ec47435-2a95-5da4-bdb7-67edfe2c66a5'::uuid, q.id, '4', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'sueno_08' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '9c0de0f6-b70e-5cbe-9694-d3605d537c43'::uuid, v.id, 'iac-adresd_00', '🧠 Motivación', 'Tengo una razón personal importante para mejorar mi salud.', 'scale', 'positive', 0, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'iac-adresd' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '10030c4a-c004-5ede-9b2e-47dec14b23b9'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_00' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '49437a13-44ba-56cf-a447-e053939184b6'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_00' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '4af37682-799c-5f51-a39d-49e11f865a1b'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_00' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0937a86f-d66a-56ba-a648-49ec5f950efc'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_00' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '9e4c9b4f-2a73-5331-b75e-70ff2f925ea0'::uuid, q.id, '4', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_00' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '628c11bf-9b6c-52be-8747-d67b1339cc3c'::uuid, v.id, 'iac-adresd_01', '🧠 Motivación', 'Mejorar mi salud es actualmente una prioridad para mí.', 'scale', 'positive', 1, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'iac-adresd' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'abfd9914-5a8d-5542-9517-6f42ac373627'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_01' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '3128d372-b006-5f6b-b22b-9efa3a89e47a'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_01' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0dcb043f-d348-5d89-9143-e3f2330918c8'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_01' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '8a037954-942c-5e03-90ac-88e97d31f18a'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_01' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'd59bfaad-61e7-508c-a330-48a8f5512a58'::uuid, q.id, '4', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_01' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '2bc6d57c-c00d-57ea-b84e-9ea8c58766a2'::uuid, v.id, 'iac-adresd_02', '💪 Autoeficacia', 'Creo que soy capaz de cambiar hábitos que afectan mi salud.', 'scale', 'positive', 2, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'iac-adresd' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5532845f-5c31-53af-99ff-131b69dbf535'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_02' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'd69e0cf8-a6d2-5532-906a-35e013ca22f0'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_02' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '64d5a799-d68b-52c6-8efe-92c6a4f3a6d9'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_02' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'f1b35b3b-2d84-5efc-8a1f-51b26212e014'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_02' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'ce154f11-2b87-5119-b2e1-0375ce4f40c1'::uuid, q.id, '4', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_02' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '22146fcc-319a-5250-88c1-bdd011990be7'::uuid, v.id, 'iac-adresd_03', '💪 Autoeficacia', 'Cuando tengo una recaída, soy capaz de volver a comenzar.', 'scale', 'positive', 3, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'iac-adresd' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '75d6d60e-90ea-5211-8998-28b316929486'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_03' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '445c6d9c-1daa-5136-bd38-35420eec7e27'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_03' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '432c2b27-0bcc-5dea-be17-120c3083f212'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_03' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '611ad304-8227-56b6-bdba-57cac5a274e4'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_03' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '03216682-0f5e-5ab5-9db2-bac11ec2c505'::uuid, q.id, '4', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_03' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '0c6b2903-dcd4-5c04-9f03-6769cec7dc64'::uuid, v.id, 'iac-adresd_04', '⏰ Organización', 'Puedo reservar tiempo de manera regular para cuidar mi salud.', 'scale', 'positive', 4, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'iac-adresd' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '2d4a056e-8133-5238-a54a-8f4880a53fdf'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_04' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '34a8b1a0-caf1-58c6-a83f-e5538a8dd220'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_04' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '9042ba72-cb1f-5e83-b515-a33a50aeabee'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_04' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '81d4c0c2-fc44-5c5c-a2e3-1777cf49ea40'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_04' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '8d817952-763b-5333-8818-278f35c2ce9b'::uuid, q.id, '4', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_04' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'd8d7d031-b100-561d-a76c-665ce819814d'::uuid, v.id, 'iac-adresd_05', '🥗 Hábitos', 'Estoy dispuesto/a a realizar cambios progresivos en mi alimentación.', 'scale', 'positive', 5, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'iac-adresd' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'b4e0030d-2398-5d98-b5e6-2d9d6c454137'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_05' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '11ab900d-75aa-527c-9d52-8437b36328ef'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_05' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '7d0dff2e-64be-539a-9448-21276ff05e72'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_05' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'eff028f2-3d1b-5c05-8067-220c09dde524'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_05' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'b97cd808-665a-504b-90b0-2e8ebe9000ea'::uuid, q.id, '4', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_05' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '0bae08cc-427a-587a-baa9-96ff1abb939d'::uuid, v.id, 'iac-adresd_06', '🥗 Hábitos', 'Estoy dispuesto/a a aumentar progresivamente mi actividad física.', 'scale', 'positive', 6, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'iac-adresd' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'bcf5359c-5757-576b-8977-0195b7b4fa38'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_06' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '6a6bf968-8723-54ad-a989-cd892168adcf'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_06' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '3544ceae-72bc-5275-9bfd-a8c7b9d9c92e'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_06' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'fdd3869d-18bf-53ee-9608-980bc5358f17'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_06' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '09d01b69-d3dd-519d-a19b-ae2fabf6307d'::uuid, q.id, '4', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_06' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'be9a264c-7835-5f84-8a68-cf68ec001839'::uuid, v.id, 'iac-adresd_07', '👥 Apoyo', 'Tengo al menos una persona que puede apoyarme durante este proceso.', 'scale', 'positive', 7, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'iac-adresd' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '78fdbe3a-5345-5832-8b1e-de751d30f172'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_07' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0fe19551-f8d9-52ad-9f02-772870b2ed36'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_07' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'cb9e6ece-0ca1-59d7-8afc-c161ca971b59'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_07' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'cf7d258b-25d7-53bb-bb03-a5f331a95be6'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_07' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '80574f87-d55c-5c04-b508-b6f12b266443'::uuid, q.id, '4', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_07' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '31806f06-0c69-5b23-8ea1-daec526bf031'::uuid, v.id, 'iac-adresd_08', '🔄 Resiliencia', 'Entiendo que una recaída no significa que haya fracasado.', 'scale', 'positive', 8, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'iac-adresd' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'c1fa01da-3584-5ff8-aa05-8d1f3cc69072'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_08' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'ad2fc63d-c5c8-5393-ba5a-645ed0f54875'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_08' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '700f97c0-58ce-5df2-a5f5-9d0f0662e711'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_08' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0b8f25a8-5b10-5320-8e7d-eed835067164'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_08' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0ea96d6e-4aa8-5e53-a33e-7f51dc57301a'::uuid, q.id, '4', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_08' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '79af5ee6-3cc1-5b48-84e5-7e8c2a585642'::uuid, v.id, 'iac-adresd_09', '📲 Compromiso', 'Estoy dispuesto/a a informar honestamente mis avances y dificultades.', 'scale', 'positive', 9, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'iac-adresd' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'a68eb716-4fe1-5409-b5b5-9849fca72bed'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_09' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '17922e6c-140e-573f-b30b-2683d1e58d2a'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_09' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'feb1ff90-f444-55b5-9883-b55e91c68434'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_09' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '3208941c-4c01-5038-9711-ad1f9a76e67e'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_09' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'b6979339-beef-5fcf-804f-5af375ad5612'::uuid, q.id, '4', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'iac-adresd_09' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '238179d5-f105-5b38-92a4-96436238ef10'::uuid, v.id, 'orp_00', '🫀 Cardiometabólico', '¿Tiene diagnóstico de hipertensión arterial?', 'scale', 'positive', 0, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'orp' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '9bde25e0-992e-557d-8f0c-ed5581060bd7'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'orp_00' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '94f4dfb3-52f8-5932-8d1d-db17caeecd82'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'orp_00' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '4d893e77-7858-54c5-9d62-7562ee6aa675'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'orp_00' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '329d4f17-2411-58c9-b06c-49c5dcd6a0ca'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'orp_00' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'f4d24d95-ec00-51ed-a955-1ada616bd806'::uuid, v.id, 'orp_01', '🫀 Cardiometabólico', '¿Tiene diagnóstico de diabetes tipo 2 o prediabetes?', 'scale', 'positive', 1, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'orp' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'b80ecf77-cb62-5a58-8945-e3bd88725ae2'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'orp_01' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'c7e7cfa4-bdf8-5f22-a8d6-477cb2c6106b'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'orp_01' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5ef01bce-4120-5a91-99e8-0cd7057cd60a'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'orp_01' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5fa6f282-8a69-584e-a53d-12dd5936cba0'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'orp_01' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '83a7e1cc-0f98-5dd5-b0e3-22db44e38bf4'::uuid, v.id, 'orp_02', '🫀 Cardiometabólico', '¿Tiene colesterol o triglicéridos elevados?', 'scale', 'positive', 2, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'orp' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e818e587-69a7-5ae7-9dcf-ed47987b0fb2'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'orp_02' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '026b7177-fbc4-5458-822d-7cff8539cb63'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'orp_02' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'baa7281a-513b-5231-8b0b-56a67c1806ad'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'orp_02' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '54ffe801-dec7-5585-a6e2-e3d132ce1f96'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'orp_02' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'a45581dd-8d9d-50d4-b2cd-066efcb1ffec'::uuid, v.id, 'orp_03', '🫀 Cardiometabólico', '¿Tiene enfermedad cardiovascular, coronaria o ACV?', 'scale', 'positive', 3, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'orp' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '4e2e76ee-3a12-523b-af20-2045af89e017'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'orp_03' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'd539c13f-6501-55ea-a0ae-6922421813f7'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'orp_03' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '47060b49-b8df-5b22-8ccb-44abd1bdde50'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'orp_03' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '75b12ceb-3421-55f2-9182-28d46a911bb7'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'orp_03' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '30fcb5b4-23b5-50e6-8b54-c47a9a0c402c'::uuid, v.id, 'orp_04', '🫀 Cardiometabólico', '¿Le han informado que tiene hígado graso?', 'scale', 'positive', 4, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'orp' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '6d4d7e30-6303-53c7-95a3-73fca6bafd57'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'orp_04' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'fe6f0096-cdf5-5d32-9af5-cfe758a97f09'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'orp_04' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '20ef480c-75d5-506f-8c4b-c933117b2020'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'orp_04' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '393aa834-4fff-515d-9da9-e319eabe276e'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'orp_04' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '9673a498-6fe1-58eb-9e34-5ca65e566688'::uuid, v.id, 'orp_05', '🫁 Sueño', '¿Ronca habitualmente o muy fuerte?', 'scale', 'positive', 5, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'orp' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e41a618e-01d0-54f6-80e3-3c891fefd92f'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'orp_05' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '572b6aaa-efce-5ce8-aaa1-9d8fac52dfb2'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'orp_05' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '3470b51c-d10e-5191-a810-b2e937070817'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'orp_05' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '44323d54-7245-5858-a963-7dfe8e733073'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'orp_05' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '19aef696-20fb-5380-ae9c-219d1df2ae2b'::uuid, v.id, 'orp_06', '🫁 Sueño', '¿Se despierta cansado/a aunque haya dormido varias horas?', 'scale', 'positive', 6, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'orp' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '56ef5dcf-594b-5aaf-a06f-37e654ba1e67'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'orp_06' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '6b70d1c7-3036-5b5b-99c5-bb6919b0751d'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'orp_06' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'af2295af-78be-5033-9b37-aca8b8f34e29'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'orp_06' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'd0ec955d-c9be-5e3e-a03f-68a19c720cdb'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'orp_06' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'f0a870d8-9156-5ddf-b380-410e885d4bc6'::uuid, v.id, 'orp_07', '🦴 Músculo-esquelético', '¿Tiene dolor frecuente en rodillas, caderas o columna?', 'scale', 'positive', 7, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'orp' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '2d851cc7-90b4-51e6-a716-502e55358a5b'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'orp_07' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '837e3a95-b409-5b6f-9d29-41af07ef91c4'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'orp_07' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '50230e7d-b907-57a5-bae4-f9d73524d432'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'orp_07' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '943502e2-55b9-50c2-b866-8eefa25adc20'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'orp_07' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'f98e3ff4-73ab-5e62-9a57-204ec1bec121'::uuid, v.id, 'orp_08', '🧠 Conductual', '¿Utiliza la comida frecuentemente para manejar estrés?', 'scale', 'positive', 8, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'orp' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0691273e-782c-53ab-9614-80b120c379d3'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'orp_08' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'b0b97a42-d058-548e-87c2-549402de131c'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'orp_08' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '4dba6fef-fd0c-50dd-881f-ea95d20c4123'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'orp_08' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'c7244253-eee6-5090-8e3a-207d88f2a958'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'orp_08' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '60d9d5f7-bfc6-54b4-9f7c-0558876fe2e2'::uuid, v.id, 'orp_09', '🧠 Conductual', '¿Ha realizado múltiples dietas y recuperado el peso?', 'scale', 'positive', 9, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'orp' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'ec34a124-72eb-5a1f-9d85-3a4e496f7878'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'orp_09' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '857300d9-a4bc-523f-83cc-825ae49d8791'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'orp_09' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '79bbbf17-afa6-552d-981f-da7a1dc042af'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'orp_09' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'a1434d9c-70f0-5057-a50f-25357d203ce2'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'orp_09' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '67b31f9b-0745-50f9-85e2-ff982003f968'::uuid, v.id, 'ers_00', '👨\u200d👩\u200d👧 Familia', 'Siento que dentro de mi familia tengo conflictos que me generan estrés.', 'scale', 'positive', 0, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'ers' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'c296581b-f2a2-5ce6-9c17-2db0d968c4a5'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'ers_00' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'b3d9ea2c-99fd-594b-abed-0e0293a9d59e'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'ers_00' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'db110b13-a473-5201-98b1-e90750a0cc68'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'ers_00' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '1c41c114-9b13-5892-9a48-5512e683ccfd'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'ers_00' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'ce72d7e0-6b6d-5a8d-8b2e-266221767e60'::uuid, q.id, '4', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'ers_00' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '1f09ad6c-586f-5c1b-9c03-5f6e8eac838b'::uuid, v.id, 'ers_01', '👨\u200d👩\u200d👧 Familia', 'Los problemas familiares interfieren con mi descanso o sueño.', 'scale', 'positive', 1, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'ers' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5d34930a-3060-54fd-829f-d88bf1e01c42'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'ers_01' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '4869b224-9756-540f-b169-fb7a172a15c4'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'ers_01' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '908fd705-14d0-5a7d-a8f6-98001c265137'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'ers_01' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '459a843a-f2cf-5b69-ae4d-684aab348a1b'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'ers_01' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'dcf524d6-c5b2-5dec-9d22-3824a0ed74b6'::uuid, q.id, '4', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'ers_01' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'b6c1f753-53ec-52ed-a912-8aa7c6e6509b'::uuid, v.id, 'ers_02', '❤️ Pareja', 'Mi relación de pareja genera situaciones que me producen estrés.', 'scale', 'positive', 2, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'ers' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '07a10313-0aa9-5eba-a6cb-3dd5092feb2c'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'ers_02' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '544d2e9b-0de6-5f78-b17e-32aa2323f5c8'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'ers_02' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'adc9eaa5-e5ce-5236-86f9-42de7dc82e92'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'ers_02' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e8a70985-cdaa-5d41-b3f3-1f0797c8ba38'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'ers_02' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'b64521c9-9acb-5880-81fc-c8199c4291d4'::uuid, q.id, '4', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'ers_02' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '29c583e8-9153-5707-914f-5d7ee78c5ad8'::uuid, v.id, 'ers_03', '❤️ Pareja', 'Los conflictos de pareja afectan mi sueño, alimentación o bienestar.', 'scale', 'positive', 3, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'ers' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'af3362d5-10f8-5a55-ae0a-4c764a8b02b3'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'ers_03' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'f9a53608-b854-5f56-a464-2156adf84ca8'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'ers_03' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'ae3ded8b-e477-5458-a2cc-a1fe2f1995c5'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'ers_03' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5d1f262f-5308-5652-8ff8-b84213003dd7'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'ers_03' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'b040ff5b-1394-58a8-bb9f-d3a621eb10b2'::uuid, q.id, '4', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'ers_03' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '25b0d0b9-bb4a-5d21-a5d0-cf45802e8bf8'::uuid, v.id, 'ers_04', '💼 Laboral', 'Mi trabajo me genera un nivel importante de estrés.', 'scale', 'positive', 4, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'ers' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e5b21d62-c6ac-5f96-9269-1843dc139a98'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'ers_04' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5a1056a4-635a-58ea-850c-322f90abbe85'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'ers_04' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'ea3c4485-6c3b-5ad5-9274-3c655b794d1d'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'ers_04' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '83b8121d-b8d6-5179-9c88-7b26c9117326'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'ers_04' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '76660452-0903-56c8-9938-64f9233c4844'::uuid, q.id, '4', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'ers_04' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '0832dae6-d2e2-5af7-9d2b-bb3e40007a0a'::uuid, v.id, 'ers_05', '💼 Laboral', 'Tengo dificultades para desconectarme mentalmente del trabajo.', 'scale', 'positive', 5, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'ers' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'a8904767-261a-5546-b33c-64bf0dbbdb22'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'ers_05' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '4420058a-4d9a-5794-9034-cb5c13e2e90c'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'ers_05' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '7301af3d-5922-58fc-ae8e-68452d41e533'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'ers_05' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5cc64cfa-3909-5206-bbbf-b712e4c8c3fa'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'ers_05' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '3748b31a-5fec-56ac-af1d-0b35c6fede15'::uuid, q.id, '4', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'ers_05' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'ab343c93-781b-5c80-9ba0-84466589d5eb'::uuid, v.id, 'ers_06', '🧑\u200d🤝\u200d🧑 Social', 'Me cuesta decir "no" cuando alguien me pide algo.', 'scale', 'positive', 6, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'ers' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '9889ed63-07ff-5776-ae45-422ebf1b75e1'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'ers_06' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '1399b533-3ba6-5935-a3f1-56192fda8563'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'ers_06' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '14d1fd32-6e9f-5440-8ae4-bb982371ed77'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'ers_06' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '24912156-6a44-5051-8fb2-fa81a9e39a09'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'ers_06' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '94b7c8ab-7bfb-5ef7-972d-20a29e751fea'::uuid, q.id, '4', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'ers_06' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '97419517-726e-5b04-8340-fc8165fae146'::uuid, v.id, 'ers_07', '🧠 Impacto', 'Cuando tengo conflictos, aumenta mi necesidad de comer o picar.', 'scale', 'positive', 7, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'ers' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '3430ab0b-b8d1-55c4-b03a-8142dcaaa723'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'ers_07' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '9ee0ee17-9a0d-56b5-a829-a4fb74197d62'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'ers_07' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'f7fe30b7-981e-545a-8acc-cc4669f3a803'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'ers_07' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'aec58c3f-e1d5-5ee2-83d0-f2c385a72a30'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'ers_07' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '4d9c87df-6a76-597a-8b45-5fc9ae829fe2'::uuid, q.id, '4', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'ers_07' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '83b4aabb-7c15-52b7-a157-5a88dbfbd86a'::uuid, v.id, 'ers_08', '🧠 Impacto', 'El estrés de mis relaciones está afectando mi calidad de vida.', 'scale', 'positive', 8, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'ers' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '44fda56e-5716-5dbc-9a77-422dfa2f7609'::uuid, q.id, '0', 0, 0, true
FROM app.health_test_questions q WHERE q.code = 'ers_08' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '201b2be9-abb8-5cfa-9f2d-149a336e1958'::uuid, q.id, '1', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'ers_08' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'f8e1ab24-1536-5548-a9e4-b284c9ce18f3'::uuid, q.id, '2', 2, 2, true
FROM app.health_test_questions q WHERE q.code = 'ers_08' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0691dd26-2155-51f2-825e-20a03ae69e4b'::uuid, q.id, '3', 3, 3, true
FROM app.health_test_questions q WHERE q.code = 'ers_08' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '7bb5e146-48d1-5a61-9844-a19fe61cb328'::uuid, q.id, '4', 4, 4, true
FROM app.health_test_questions q WHERE q.code = 'ers_08' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '8eae5632-1c9c-54f9-91cc-42356299af5d'::uuid, v.id, 'bateria-antares_00', '🧠 Mentalidad de salud', 'Mi estado de salud depende principalmente de mis propias acciones.', 'scale', 'positive', 0, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'bateria-antares' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '34a1f79c-1582-5a31-af6c-f9b1e2e25265'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'bateria-antares_00' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0737576b-6dee-5eee-a7aa-ab96a02a348c'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'bateria-antares_00' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'b295abba-689b-5f99-a2d3-1843ec1f2f8a'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'bateria-antares_00' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'fe415de3-104c-5ac4-8336-ccc7cb87e680'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'bateria-antares_00' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'd11b0cbc-ded4-592f-b262-4f58f3c90b9a'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'bateria-antares_00' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '18b9d07b-6659-50da-ae41-34607c34cd03'::uuid, v.id, 'bateria-antares_01', '🧠 Mentalidad de salud', 'Cuando fracaso en un hábito, me recupero sin abandonar el proceso.', 'scale', 'positive', 1, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'bateria-antares' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '8f13a757-d2ea-5edc-9969-50a770b175aa'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'bateria-antares_01' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'b771d852-e54a-5bf8-9c2c-2d9490bbcac9'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'bateria-antares_01' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'ecafa648-dc8e-5b70-83df-7455991a0fc6'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'bateria-antares_01' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5fb40885-55d3-5769-9a4f-db5630620695'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'bateria-antares_01' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'c4c6654e-f9ec-543c-80e1-2ecff6a3d521'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'bateria-antares_01' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '03e27e27-633a-5a76-8a3c-baa75371ef72'::uuid, v.id, 'bateria-antares_02', '🧠 Mentalidad de salud', 'Creo que cambios pequeños y constantes producen resultados importantes.', 'scale', 'positive', 2, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'bateria-antares' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0834cc4a-b1a5-5244-aaf1-650d25211c05'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'bateria-antares_02' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '06fac8f1-01ab-560b-bd93-d84d4003e287'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'bateria-antares_02' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '81a15d61-a98a-587c-be27-0a1508a14ca6'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'bateria-antares_02' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0c3912ba-c48d-525f-a17f-f1389a3ce281'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'bateria-antares_02' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '8d48e180-7221-52ff-a143-eace49d6ad6e'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'bateria-antares_02' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'bf953f1a-d1c5-5309-9034-b32f29ccea1d'::uuid, v.id, 'bateria-antares_03', '🧠 Mentalidad de salud', 'Me veo capaz de construir una versión más saludable de mí mismo/a.', 'scale', 'positive', 3, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'bateria-antares' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e0f19f89-732f-545a-9e6e-468152bf519b'::uuid, q.id, '1', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'bateria-antares_03' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'b7aa0331-a483-590f-834e-22b7ab761afd'::uuid, q.id, '2', 2, 1, true
FROM app.health_test_questions q WHERE q.code = 'bateria-antares_03' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e50ad1a2-ba54-5058-8e22-8dc6235bde00'::uuid, q.id, '3', 3, 2, true
FROM app.health_test_questions q WHERE q.code = 'bateria-antares_03' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '2578086c-5aeb-5cce-8164-928cd84a6350'::uuid, q.id, '4', 4, 3, true
FROM app.health_test_questions q WHERE q.code = 'bateria-antares_03' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e30e514b-5f57-537e-97df-a08ac7f4501d'::uuid, q.id, '5', 5, 4, true
FROM app.health_test_questions q WHERE q.code = 'bateria-antares_03' AND q.is_active = true
ON CONFLICT DO NOTHING;


-- Historia clínica: bloques multi-select (inventario).
INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'c097af53-b300-5c73-b8c0-1a49220d0ec0'::uuid, v.id, 'hist_antecedentes', 'Antecedentes', 'Antecedentes', 'multi', 'positive', 0, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'historia-clinica' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'ff21c2e9-c745-5cd0-b093-30876e954404'::uuid, q.id, 'Diabetes tipo 2', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'hist_antecedentes' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e1e77136-d6e1-53a4-8129-69ba192e2187'::uuid, q.id, 'Prediabetes', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'hist_antecedentes' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5563ec06-5497-59b1-8f18-c009b6b794cf'::uuid, q.id, 'Hipertensión', 1, 2, true
FROM app.health_test_questions q WHERE q.code = 'hist_antecedentes' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'ae6063b4-88b2-58c2-b5e5-ef589a4cc5aa'::uuid, q.id, 'Enf. cardiovascular', 1, 3, true
FROM app.health_test_questions q WHERE q.code = 'hist_antecedentes' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'bd808805-3c6e-5703-9705-1502e4d4b16a'::uuid, q.id, 'Colesterol alto', 1, 4, true
FROM app.health_test_questions q WHERE q.code = 'hist_antecedentes' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '92314e6b-4953-5f2f-8fcb-4f3817d6bed5'::uuid, q.id, 'Triglicéridos altos', 1, 5, true
FROM app.health_test_questions q WHERE q.code = 'hist_antecedentes' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'e406373b-417d-502f-adec-0df35a5f7395'::uuid, q.id, 'Hígado graso', 1, 6, true
FROM app.health_test_questions q WHERE q.code = 'hist_antecedentes' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '244aa0b3-bb6b-55e4-ab2e-3c4f1161e0bd'::uuid, q.id, 'Asma / EPOC', 1, 7, true
FROM app.health_test_questions q WHERE q.code = 'hist_antecedentes' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '88653310-b5be-58a0-afdf-1c73626ae878'::uuid, q.id, 'Artrosis', 1, 8, true
FROM app.health_test_questions q WHERE q.code = 'hist_antecedentes' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'ee4b6158-69d9-5044-9abc-d9f96cc5914b'::uuid, q.id, 'Depresión / Ansiedad', 1, 9, true
FROM app.health_test_questions q WHERE q.code = 'hist_antecedentes' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '37a95822-5950-5803-bc34-677c6f5a5317'::uuid, q.id, 'Tiroides', 1, 10, true
FROM app.health_test_questions q WHERE q.code = 'hist_antecedentes' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'a492645a-d1c3-5b64-bb93-adb1c4c0ea11'::uuid, q.id, 'Ninguno', 1, 11, true
FROM app.health_test_questions q WHERE q.code = 'hist_antecedentes' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '0fd231de-16dc-545b-8d12-09a24d604ca5'::uuid, v.id, 'hist_familia', 'Familia', 'Familia', 'multi', 'positive', 1, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'historia-clinica' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'ed581503-f89b-5900-9630-7d842eff9465'::uuid, q.id, 'Diabetes', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'hist_familia' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '8628e57e-b180-5720-acfe-e15937401017'::uuid, q.id, 'Enf. coronaria', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'hist_familia' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '3f96d313-d23b-5fea-8c79-b47a4592e334'::uuid, q.id, 'HTA', 1, 2, true
FROM app.health_test_questions q WHERE q.code = 'hist_familia' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '3d7f3458-ab4f-5b97-82d3-8b20e51730ad'::uuid, q.id, 'ACV', 1, 3, true
FROM app.health_test_questions q WHERE q.code = 'hist_familia' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '1550bb74-cd2e-5aec-9bba-96f9c4eae671'::uuid, q.id, 'Cáncer', 1, 4, true
FROM app.health_test_questions q WHERE q.code = 'hist_familia' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '32fc8e63-05ea-5381-a3bd-c39f7b11828d'::uuid, q.id, 'Ninguno conocido', 1, 5, true
FROM app.health_test_questions q WHERE q.code = 'hist_familia' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '35c20c9e-ac19-5f7c-b621-5202ce95d427'::uuid, v.id, 'hist_cabeza_cuello', 'Cabeza y cuello', 'Cabeza y cuello', 'multi', 'positive', 2, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'historia-clinica' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'b8a3ce93-5a76-5690-8cf1-63f78d894189'::uuid, q.id, 'Cefalea frecuente', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'hist_cabeza_cuello' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'cc5c5701-58a1-54a2-a960-230356df5a87'::uuid, q.id, 'Mareos o vértigo', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'hist_cabeza_cuello' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '2ac80723-7345-5019-b6a1-b596fda33a0e'::uuid, q.id, 'Visión borrosa', 1, 2, true
FROM app.health_test_questions q WHERE q.code = 'hist_cabeza_cuello' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '041c6b74-c23f-51b7-b42b-b7bc6ca9637b'::uuid, q.id, 'Tinnitus', 1, 3, true
FROM app.health_test_questions q WHERE q.code = 'hist_cabeza_cuello' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '356ebf72-bfb2-58b9-9b85-b5b63e57deec'::uuid, q.id, 'Sinusitis', 1, 4, true
FROM app.health_test_questions q WHERE q.code = 'hist_cabeza_cuello' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'de5f2de2-30c0-5ce8-80b9-c9c41d70e395'::uuid, q.id, 'Disfagia', 1, 5, true
FROM app.health_test_questions q WHERE q.code = 'hist_cabeza_cuello' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'b5ac560d-d9c5-58a2-85fb-fe3105dd9590'::uuid, v.id, 'hist_cardiorrespiratorio', 'Cardiorrespiratorio', 'Cardiorrespiratorio', 'multi', 'positive', 3, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'historia-clinica' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5bf000a0-71f1-5a0b-a80d-163387e3ef03'::uuid, q.id, 'Palpitaciones', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'hist_cardiorrespiratorio' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '89e29cab-3beb-596d-8c18-b42f0d8f6c90'::uuid, q.id, 'Dolor de pecho', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'hist_cardiorrespiratorio' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '8332f394-009b-5a13-86f2-fa87a6f8cb81'::uuid, q.id, 'Disnea al esfuerzo', 1, 2, true
FROM app.health_test_questions q WHERE q.code = 'hist_cardiorrespiratorio' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'c2c9bc8b-ad48-52ce-a3e7-f47cc05e0c3e'::uuid, q.id, 'Edema en piernas', 1, 3, true
FROM app.health_test_questions q WHERE q.code = 'hist_cardiorrespiratorio' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '51a99db9-79a8-50ae-aecb-ce02d1cef2ec'::uuid, q.id, 'Tos persistente', 1, 4, true
FROM app.health_test_questions q WHERE q.code = 'hist_cardiorrespiratorio' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0285580f-a96c-590d-92c6-13033a1d3544'::uuid, q.id, 'Apnea nocturna', 1, 5, true
FROM app.health_test_questions q WHERE q.code = 'hist_cardiorrespiratorio' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'ba291e1c-fadf-5d56-ba00-ae1547e6b187'::uuid, v.id, 'hist_digestivo', 'Digestivo', 'Digestivo', 'multi', 'positive', 4, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'historia-clinica' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'db56b083-626f-5663-8473-4b6f4f568b8b'::uuid, q.id, 'Reflujo', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'hist_digestivo' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'edd0d091-7a28-584b-86fa-d14838e8b9dc'::uuid, q.id, 'Náuseas', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'hist_digestivo' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'daaccca0-a66b-5f3f-9726-77f045e12963'::uuid, q.id, 'Dolor abdominal', 1, 2, true
FROM app.health_test_questions q WHERE q.code = 'hist_digestivo' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'bc4d2287-8899-50af-afbb-08ada0b39996'::uuid, q.id, 'Cambio de hábito intestinal', 1, 3, true
FROM app.health_test_questions q WHERE q.code = 'hist_digestivo' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '246d0fe4-a25f-5a03-8acd-b72797b44e0e'::uuid, q.id, 'Distensión', 1, 4, true
FROM app.health_test_questions q WHERE q.code = 'hist_digestivo' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '9d0054d7-74d8-5594-ad08-d4a0b2f0b740'::uuid, q.id, 'Pérdida de peso', 1, 5, true
FROM app.health_test_questions q WHERE q.code = 'hist_digestivo' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT 'b8d63a95-ba40-56b4-b187-99f6701d4b52'::uuid, v.id, 'hist_osteomuscular', 'Osteomuscular', 'Osteomuscular', 'multi', 'positive', 5, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'historia-clinica' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '96621a96-c56a-5b57-a658-c39ca0229fbc'::uuid, q.id, 'Dolor articular', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'hist_osteomuscular' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'a505c552-851a-551f-85d1-6525a5f22d9a'::uuid, q.id, 'Dolor lumbar', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'hist_osteomuscular' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '0e17a954-8965-5fc8-850b-dfec854e6285'::uuid, q.id, 'Rigidez matutina', 1, 2, true
FROM app.health_test_questions q WHERE q.code = 'hist_osteomuscular' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '9f415ac7-fbd8-518e-8be3-1d25f9344159'::uuid, q.id, 'Limitación de movimiento', 1, 3, true
FROM app.health_test_questions q WHERE q.code = 'hist_osteomuscular' AND q.is_active = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '7a696d96-118e-5a37-9a79-26e6c710209d'::uuid, v.id, 'hist_psicologico', 'Psicológico', 'Psicológico', 'multi', 'positive', 6, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'historia-clinica' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5684e596-ac59-5941-b528-a9f5bbb6e8ed'::uuid, q.id, 'Tristeza persistente', 1, 0, true
FROM app.health_test_questions q WHERE q.code = 'hist_psicologico' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT 'c16de441-a90f-5eb0-b6d6-1df081ee4226'::uuid, q.id, 'Ansiedad', 1, 1, true
FROM app.health_test_questions q WHERE q.code = 'hist_psicologico' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '450df109-f921-5510-a25f-484ec4f757b4'::uuid, q.id, 'Insomnio', 1, 2, true
FROM app.health_test_questions q WHERE q.code = 'hist_psicologico' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '7de0e4a0-8897-59fe-bcfe-3db66c55d3a4'::uuid, q.id, 'Irritabilidad', 1, 3, true
FROM app.health_test_questions q WHERE q.code = 'hist_psicologico' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '4b968e86-a3cd-54fe-b2c0-7a1459f9d04d'::uuid, q.id, 'Baja motivación', 1, 4, true
FROM app.health_test_questions q WHERE q.code = 'hist_psicologico' AND q.is_active = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_answer_options (id, question_id, text, score_value, sort_order, is_active)
SELECT '5cfae4a4-d1f6-5b14-a60a-a3864bbd52a3'::uuid, q.id, 'Estrés elevado', 1, 5, true
FROM app.health_test_questions q WHERE q.code = 'hist_psicologico' AND q.is_active = true
ON CONFLICT DO NOTHING;


-- Batería ANTARES: preguntas abiertas de propósito.
INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '4d19e762-2743-5f7f-8bef-9866cc7f259c'::uuid, v.id, 'antares_open_0', '🎯 Propósito', '¿Para qué quieres estar saludable?', 'open', 'positive', 100, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'bateria-antares' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '1b95096a-4962-5863-90f3-34a4a70c9da3'::uuid, v.id, 'antares_open_1', '🎯 Propósito', '¿Qué quieres poder hacer dentro de 5 años que hoy se te dificulta?', 'open', 'positive', 101, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'bateria-antares' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;
INSERT INTO app.health_test_questions (id, version_id, code, section, text, type, scoring_direction, sort_order, is_active)
SELECT '1b920e72-d733-5a04-875c-ceb2b1a611a2'::uuid, v.id, 'antares_open_2', '🎯 Propósito', '¿Qué perderías si tu salud empeorara?', 'open', 'positive', 102, true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'bateria-antares' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT (version_id, code) DO NOTHING;

-- Rangos de interpretación por versión.
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '868a8bee-c70f-548d-bbed-0038e84790e0'::uuid, v.id, 32, 63, 'bajo', 'low', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '7773cd9a-b16e-53cb-af69-38e42438df13'::uuid, v.id, 64, 95, 'moderado', 'moderate', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT 'aa77594c-3bdd-57e1-b525-f83d05bbd644'::uuid, v.id, 96, 128, 'alto', 'high', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'temperamento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '2f299022-e9fc-5fa9-a883-1bd3f0569cf4'::uuid, v.id, 17, 33, 'bajo', 'low', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'nutricional' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '79b5576e-c279-50a5-bb10-fb2b62f7bc98'::uuid, v.id, 34, 50, 'moderado', 'moderate', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'nutricional' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT 'be5cf079-c807-5a2c-aa2a-3c7efba783ca'::uuid, v.id, 51, 68, 'alto', 'high', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'nutricional' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '4069210e-e5d3-5e66-80ee-d957949bcde8'::uuid, v.id, 0, 12, 'bajo', 'low', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'movimiento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '30789bbb-09dd-5673-9e92-f3bf1201d8a2'::uuid, v.id, 13, 24, 'moderado', 'moderate', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'movimiento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '460e02c0-0484-578c-85f1-6a5d4a5cc91b'::uuid, v.id, 25, 36, 'alto', 'high', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'movimiento' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '5f1b2dd0-9c12-51a4-bd45-76255c9fd4da'::uuid, v.id, 0, 12, 'bajo', 'low', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'sueno' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '4463de42-ce41-5556-86eb-161e0c4f631c'::uuid, v.id, 13, 24, 'moderado', 'moderate', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'sueno' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT 'b6462553-d190-539b-8f52-791510eafb08'::uuid, v.id, 25, 36, 'alto', 'high', true
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
SELECT '8098b67c-e360-5b61-98e9-2783ce535604'::uuid, v.id, 0, 9, 'bajo', 'low', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'orp' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '3ccafa00-1cfe-50f8-a8c8-0ed083f1ffe7'::uuid, v.id, 10, 17, 'moderado', 'moderate', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'orp' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '8c972277-e0bc-562e-9de3-8438b773c33f'::uuid, v.id, 18, 24, 'alto', 'high', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'orp' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '06aa9388-1392-57a4-9331-768ef2b360cf'::uuid, v.id, 25, 30, 'critico', 'critical', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'orp' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '9786a828-1501-5dfc-8a1b-4cf009054dba'::uuid, v.id, 0, 9, 'bajo', 'low', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'ers' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT 'b73d3dee-8a40-5f24-9420-74239d0a8b4a'::uuid, v.id, 10, 18, 'moderado', 'moderate', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'ers' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '0c83a942-7ed6-5e54-9787-a642edbe4f95'::uuid, v.id, 19, 27, 'alto', 'high', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'ers' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '6d0b438a-1bbf-5e58-86c9-29b9ae1f7b00'::uuid, v.id, 28, 36, 'critico', 'critical', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'ers' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT 'e8aec319-a938-5d26-9c83-fb67ad27b568'::uuid, v.id, 4, 9, 'bajo', 'low', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'bateria-antares' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT 'fab3ce78-765d-5ed1-9800-1afbf0142d0d'::uuid, v.id, 10, 14, 'moderado', 'moderate', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'bateria-antares' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '01d5d163-8d02-5081-8bfd-3a2979ab0641'::uuid, v.id, 15, 20, 'alto', 'high', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'bateria-antares' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT 'f1007249-b462-5536-b5c7-aac2a0ba2b4f'::uuid, v.id, 0, 2, 'bajo', 'low', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'historia-clinica' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT 'b19436f9-adb2-5d5a-96ff-9aeaa711937e'::uuid, v.id, 3, 5, 'moderado', 'moderate', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'historia-clinica' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;
INSERT INTO app.health_test_score_ranges (id, version_id, min_value, max_value, label, severity, is_active)
SELECT '74487812-48aa-5d78-8581-3d4cf04b453a'::uuid, v.id, 6, 99, 'alto', 'high', true
FROM app.health_test_versions v JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE i.code = 'historia-clinica' AND v.version_number = 1 AND v.is_current = true
ON CONFLICT DO NOTHING;

INSERT INTO app.health_test_batteries (id, code, name, description, auto_assign_on_patient_create, is_active, created_at)
VALUES ('0d0198a4-ade2-50af-a6d4-e5fe29096d41'::uuid, 'bateria-inicial', 'Batería de evaluación inicial ANTARES', 'Evaluación inicial del programa (9 tests del onboarding)', true, true, now())
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
    ('db15ba04-66b6-5a9d-9550-195c02dfccaa'::uuid, 'iadherencia', 'Índice de adherencia IAC-ADRESD', 'Indicador derivado: Índice de adherencia IAC-ADRESD', '{"formula":"weighted","sources":[{"resultType":"subscale","code":"🧠 Motivación"},{"resultType":"subscale","code":"💪 Autoeficacia"},{"resultType":"subscale","code":"⏰ Organización"},{"resultType":"subscale","code":"🥗 Hábitos"},{"resultType":"subscale","code":"👥 Apoyo"},{"resultType":"subscale","code":"🔄 Resiliencia"},{"resultType":"subscale","code":"📲 Compromiso"}]}'::jsonb, true, now()),
    ('ba1c2658-464c-595f-9947-76ea9e52f0f2'::uuid, 'iapnea', 'Sospecha de apnea del sueño', 'Indicador derivado: Sospecha de apnea del sueño', '{"formula":"sum","sources":[{"resultType":"subscale","code":"🫁 Sueño"}]}'::jsonb, true, now())
) AS t(id, code, name, description, computation, is_active, created_at)
ON CONFLICT (code) DO NOTHING;

INSERT INTO app.health_test_alert_rules (id, code, name, condition, severity, message_template, is_active, created_at)
SELECT * FROM (VALUES
    ('7a8213b2-9487-53fb-bc0b-2f95ad706f71'::uuid, 'orp_riesgo_alto', 'Riesgo cardiometabólico alto', '{"when":{"resultType":"score","code":"orp","severity":["high","critical"]}}'::jsonb, 'high', 'ORP {value} - {label}: riesgo cardiometabólico elevado. Revisar prioridad.', true, now()),
    ('19632f6a-a4f1-5f9e-8a59-9726e302595d'::uuid, 'iapnea_sospecha', 'Sospecha de apnea del sueño', '{"when":{"resultType":"indicator","code":"iapnea","severity":["high"]}}'::jsonb, 'high', 'Sospecha de apnea: {value} señales de riesgo. Evaluar estudio de sueño.', true, now()),
    ('a7fe02c1-49d5-5d7a-b8ba-1271fd43c38f'::uuid, 'adherencia_baja', 'Adherencia baja al programa', '{"when":{"resultType":"indicator","code":"iadherencia","severity":["high"]}}'::jsonb, 'moderate', 'Adherencia IAC-ADRESD baja ({value}%). Requiere acompañamiento.', true, now())
) AS t(id, code, name, condition, severity, message_template, is_active, created_at)
ON CONFLICT (code) DO NOTHING;
