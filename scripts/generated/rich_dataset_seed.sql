-- ============================================================================
-- rich_dataset_seed.sql — GENERADO por scripts/generate_rich_dataset_seed.py
-- NO EDITAR A MANO: regenerar con  python generate_rich_dataset_seed.py
-- Alcance: pacientes con usuario demo (password Demo1234!) — scope=demo
-- Idempotente / sin borrados / nunca toca app.program_weeks (semanas congeladas).
-- Marca de origen: created_by/source 7e1c4d20-9a41-4f0e-9b5a-5f3a2c1d0e9f
-- Solo desarrollo local. NUNCA contra producción.
-- ============================================================================

BEGIN;

-- ====================================================================
-- A) MEDIA RICO: 40 ítems Publicados (30 podcasts + 6 videos + 4 audios)
--    StorageKey/ThumbnailKey -> claves demo que el usuario subirá a S3:
--    media/podcasts/demo-copp.mp3 / media/thumbnails/demo-copp.jpg
--    day = día de programa (1..83) · month = mes de programa (1..3)
--    83 días / 12 semanas: días 1-28 → mes 1, 29-56 → mes 2, 57-83 → mes 3
--    Idempotencia: guarda NOT EXISTS(title) — misma clave de negocio que usa
--    DevProgramSeeder para asegurar medios (id gen_random_uuid).
-- ====================================================================

-- 8. [Podcast · Nutricion] Ep. 8: Índice glucémico y carga glucémica
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Ep. 8: Índice glucémico y carga glucémica',
    'Cómo elegir carbohidratos que estabilizan tu glucosa y te mantienen satisfecho por más horas.',
    'Podcast',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    660,
    'Published',
    8,
    now() - interval '40 days',
    now(),
    now(),
    'Dr. Alejandro Gómez',
    'Nutricion',
    8,
    1,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Intro y contexto"}, {"atSeconds": 180, "label": "El error más común"}, {"atSeconds": 380, "label": "Lo que dice la ciencia"}, {"atSeconds": 540, "label": "Protocolo de la semana"}]'::jsonb,
    '["Prioriza proteína y fibra en cada plato principal.", "Los carbohidratos integrales estabilizan tu glucosa por horas.", "Lee etiquetas: más de 5 ingredientes desconocidos, devuélvelo a la repisa."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Ep. 8: Índice glucémico y carga glucémica' AND x.status = 'Published'
);

-- 9. [Podcast · Nutricion] Ep. 9: Proteína, saciedad y masa muscular
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Ep. 9: Proteína, saciedad y masa muscular',
    'Cuánta proteína necesitas por comida y cómo protege tu músculo mientras bajas de peso.',
    'Podcast',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    780,
    'Published',
    9,
    now() - interval '39 days',
    now(),
    now(),
    'Dra. Sofía Morales',
    'Nutricion',
    10,
    1,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Intro y contexto"}, {"atSeconds": 215, "label": "El error más común"}, {"atSeconds": 450, "label": "Lo que dice la ciencia"}, {"atSeconds": 635, "label": "Protocolo de la semana"}]'::jsonb,
    '["Prioriza proteína y fibra en cada plato principal.", "Los carbohidratos integrales estabilizan tu glucosa por horas.", "Lee etiquetas: más de 5 ingredientes desconocidos, devuélvelo a la repisa."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Ep. 9: Proteína, saciedad y masa muscular' AND x.status = 'Published'
);

-- 10. [Podcast · Nutricion] Ep. 10: Ultraprocesados: lee la etiqueta como experto
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Ep. 10: Ultraprocesados: lee la etiqueta como experto',
    'Las señales de alerta del empaque y reglas simples para limpiar tu despensa esta semana.',
    'Podcast',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    540,
    'Published',
    10,
    now() - interval '38 days',
    now(),
    now(),
    'Dr. Carlos Valencia',
    'Nutricion',
    12,
    1,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Intro y contexto"}, {"atSeconds": 150, "label": "El error más común"}, {"atSeconds": 310, "label": "Lo que dice la ciencia"}, {"atSeconds": 440, "label": "Protocolo de la semana"}]'::jsonb,
    '["Prioriza proteína y fibra en cada plato principal.", "Los carbohidratos integrales estabilizan tu glucosa por horas.", "Lee etiquetas: más de 5 ingredientes desconocidos, devuélvelo a la repisa."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Ep. 10: Ultraprocesados: lee la etiqueta como experto' AND x.status = 'Published'
);

-- 11. [Podcast · Nutricion] Ep. 11: Fibra: el nutriente que olvidamos
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Ep. 11: Fibra: el nutriente que olvidamos',
    'Por qué la fibra regula glucosa, colesterol y apetito, y cómo llegar a 30 g al día sin sufrir.',
    'Podcast',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    720,
    'Published',
    11,
    now() - interval '37 days',
    now(),
    now(),
    'Dra. Elena Ruiz',
    'Nutricion',
    14,
    1,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Intro y contexto"}, {"atSeconds": 200, "label": "El error más común"}, {"atSeconds": 415, "label": "Lo que dice la ciencia"}, {"atSeconds": 590, "label": "Protocolo de la semana"}]'::jsonb,
    '["Prioriza proteína y fibra en cada plato principal.", "Los carbohidratos integrales estabilizan tu glucosa por horas.", "Lee etiquetas: más de 5 ingredientes desconocidos, devuélvelo a la repisa."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Ep. 11: Fibra: el nutriente que olvidamos' AND x.status = 'Published'
);

-- 12. [Podcast · Nutricion] Ep. 12: Grasas que curan, grasas que dañan
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Ep. 12: Grasas que curan, grasas que dañan',
    'Del aguacate al omega-3: qué grasas priorizar y cuáles reducir para proteger tu corazón.',
    'Podcast',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    600,
    'Published',
    12,
    now() - interval '36 days',
    now(),
    now(),
    'Lic. Mateo Ríos',
    'Nutricion',
    16,
    1,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Intro y contexto"}, {"atSeconds": 165, "label": "El error más común"}, {"atSeconds": 345, "label": "Lo que dice la ciencia"}, {"atSeconds": 490, "label": "Protocolo de la semana"}]'::jsonb,
    '["Prioriza proteína y fibra en cada plato principal.", "Los carbohidratos integrales estabilizan tu glucosa por horas.", "Lee etiquetas: más de 5 ingredientes desconocidos, devuélvelo a la repisa."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Ep. 12: Grasas que curan, grasas que dañan' AND x.status = 'Published'
);

-- 13. [Podcast · SaludFisica] Ep. 13: Cardio zona 2: la base de tu resistencia
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Ep. 13: Cardio zona 2: la base de tu resistencia',
    'Entrena en la intensidad correcta para mejorar tu metabolismo sin agotarte.',
    'Podcast',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    840,
    'Published',
    13,
    now() - interval '35 days',
    now(),
    now(),
    'Dra. Camila Restrepo',
    'SaludFisica',
    18,
    1,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Calentamiento mental"}, {"atSeconds": 235, "label": "Técnica correcta"}, {"atSeconds": 485, "label": "Progresión práctica"}, {"atSeconds": 685, "label": "Prevención de lesiones"}]'::jsonb,
    '["Dos sesiones de fuerza por semana ya cambian tu metabolismo.", "Caminar más fuera del entrenamiento pesa más que el entrenamiento.", "Progresa carga o repeticiones cada 2 semanas; nunca a costa de dolor articular."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Ep. 13: Cardio zona 2: la base de tu resistencia' AND x.status = 'Published'
);

-- 14. [Podcast · SaludFisica] Ep. 14: Fuerza para principiantes: empieza bien
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Ep. 14: Fuerza para principiantes: empieza bien',
    'Los cinco patrones básicos de movimiento y cómo progresar sin lesionarte.',
    'Podcast',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    720,
    'Published',
    14,
    now() - interval '34 days',
    now(),
    now(),
    'Dr. Julián Ossa',
    'SaludFisica',
    20,
    1,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Calentamiento mental"}, {"atSeconds": 200, "label": "Técnica correcta"}, {"atSeconds": 415, "label": "Progresión práctica"}, {"atSeconds": 590, "label": "Prevención de lesiones"}]'::jsonb,
    '["Dos sesiones de fuerza por semana ya cambian tu metabolismo.", "Caminar más fuera del entrenamiento pesa más que el entrenamiento.", "Progresa carga o repeticiones cada 2 semanas; nunca a costa de dolor articular."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Ep. 14: Fuerza para principiantes: empieza bien' AND x.status = 'Published'
);

-- 15. [Podcast · SaludFisica] Ep. 15: Movilidad de cadera y hombro
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Ep. 15: Movilidad de cadera y hombro',
    'Rutinas de 10 minutos que desatan la rigidez del escritorio y mejoran tu técnica.',
    'Podcast',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    600,
    'Published',
    15,
    now() - interval '33 days',
    now(),
    now(),
    'Lic. Valeria Duque',
    'SaludFisica',
    21,
    1,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Calentamiento mental"}, {"atSeconds": 165, "label": "Técnica correcta"}, {"atSeconds": 345, "label": "Progresión práctica"}, {"atSeconds": 490, "label": "Prevención de lesiones"}]'::jsonb,
    '["Dos sesiones de fuerza por semana ya cambian tu metabolismo.", "Caminar más fuera del entrenamiento pesa más que el entrenamiento.", "Progresa carga o repeticiones cada 2 semanas; nunca a costa de dolor articular."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Ep. 15: Movilidad de cadera y hombro' AND x.status = 'Published'
);

-- 16. [Podcast · SaludFisica] Ep. 16: Recuperación: donde ocurre el progreso
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Ep. 16: Recuperación: donde ocurre el progreso',
    'Sueño, descanso activo y señales de sobreentrenamiento que debes respetar.',
    'Podcast',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    540,
    'Published',
    16,
    now() - interval '32 days',
    now(),
    now(),
    'Dr. Andrés Villegas',
    'SaludFisica',
    23,
    1,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Calentamiento mental"}, {"atSeconds": 150, "label": "Técnica correcta"}, {"atSeconds": 310, "label": "Progresión práctica"}, {"atSeconds": 440, "label": "Prevención de lesiones"}]'::jsonb,
    '["Dos sesiones de fuerza por semana ya cambian tu metabolismo.", "Caminar más fuera del entrenamiento pesa más que el entrenamiento.", "Progresa carga o repeticiones cada 2 semanas; nunca a costa de dolor articular."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Ep. 16: Recuperación: donde ocurre el progreso' AND x.status = 'Published'
);

-- 17. [Podcast · SaludFisica] Ep. 17: NEAT: los pasos que cambian tu metabolismo
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Ep. 17: NEAT: los pasos que cambian tu metabolismo',
    'Cómo moverte más fuera del gimnasio y convertir la oficina en tu aliada.',
    'Podcast',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    660,
    'Published',
    17,
    now() - interval '31 days',
    now(),
    now(),
    'Dra. Paula Ocampo',
    'SaludFisica',
    25,
    1,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Calentamiento mental"}, {"atSeconds": 180, "label": "Técnica correcta"}, {"atSeconds": 380, "label": "Progresión práctica"}, {"atSeconds": 540, "label": "Prevención de lesiones"}]'::jsonb,
    '["Dos sesiones de fuerza por semana ya cambian tu metabolismo.", "Caminar más fuera del entrenamiento pesa más que el entrenamiento.", "Progresa carga o repeticiones cada 2 semanas; nunca a costa de dolor articular."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Ep. 17: NEAT: los pasos que cambian tu metabolismo' AND x.status = 'Published'
);

-- 18. [Podcast · Habitos] Ep. 18: La anatomía de un hábito que dura
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Ep. 18: La anatomía de un hábito que dura',
    'Clave, rutina y recompensa: diseña tu sistema para no depender de la fuerza de voluntad.',
    'Podcast',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    720,
    'Published',
    18,
    now() - interval '30 days',
    now(),
    now(),
    'Dr. Alejandro Gómez',
    'Habitos',
    27,
    1,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Por qué fallamos"}, {"atSeconds": 200, "label": "El sistema simple"}, {"atSeconds": 415, "label": "Diseño de entorno"}, {"atSeconds": 590, "label": "Plan antifallo"}]'::jsonb,
    '["Diseña el entorno: la opción sana debe ser la más fácil.", "No dependas de la voluntad: automatiza comidas y horarios.", "Tras una recaída, el protocolo es volver en 48 horas sin culpa."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Ep. 18: La anatomía de un hábito que dura' AND x.status = 'Published'
);

-- 19. [Podcast · Habitos] Ep. 19: Rituales de mañana en 20 minutos
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Ep. 19: Rituales de mañana en 20 minutos',
    'Una secuencia simple de agua, luz, movimiento y desayuno para arrancar con energía.',
    'Podcast',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    540,
    'Published',
    19,
    now() - interval '29 days',
    now(),
    now(),
    'Dra. Sofía Morales',
    'Habitos',
    29,
    2,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Por qué fallamos"}, {"atSeconds": 150, "label": "El sistema simple"}, {"atSeconds": 310, "label": "Diseño de entorno"}, {"atSeconds": 440, "label": "Plan antifallo"}]'::jsonb,
    '["Diseña el entorno: la opción sana debe ser la más fácil.", "No dependas de la voluntad: automatiza comidas y horarios.", "Tras una recaída, el protocolo es volver en 48 horas sin culpa."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Ep. 19: Rituales de mañana en 20 minutos' AND x.status = 'Published'
);

-- 20. [Podcast · Habitos] Ep. 20: Tu entorno te adelgaza (o engorda)
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Ep. 20: Tu entorno te adelgaza (o engorda)',
    'Rediseña cocina, escritorio y celular para que la opción sana sea la opción fácil.',
    'Podcast',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    600,
    'Published',
    20,
    now() - interval '28 days',
    now(),
    now(),
    'Dr. Carlos Valencia',
    'Habitos',
    31,
    2,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Por qué fallamos"}, {"atSeconds": 165, "label": "El sistema simple"}, {"atSeconds": 345, "label": "Diseño de entorno"}, {"atSeconds": 490, "label": "Plan antifallo"}]'::jsonb,
    '["Diseña el entorno: la opción sana debe ser la más fácil.", "No dependas de la voluntad: automatiza comidas y horarios.", "Tras una recaída, el protocolo es volver en 48 horas sin culpa."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Ep. 20: Tu entorno te adelgaza (o engorda)' AND x.status = 'Published'
);

-- 21. [Podcast · Habitos] Ep. 21: Recaídas: volver sin culpa
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Ep. 21: Recaídas: volver sin culpa',
    'El protocolo de 48 horas para retomar el plan después de un fin de semana imperfecto.',
    'Podcast',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    780,
    'Published',
    21,
    now() - interval '27 days',
    now(),
    now(),
    'Dra. Elena Ruiz',
    'Habitos',
    33,
    2,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Por qué fallamos"}, {"atSeconds": 215, "label": "El sistema simple"}, {"atSeconds": 450, "label": "Diseño de entorno"}, {"atSeconds": 635, "label": "Plan antifallo"}]'::jsonb,
    '["Diseña el entorno: la opción sana debe ser la más fácil.", "No dependas de la voluntad: automatiza comidas y horarios.", "Tras una recaída, el protocolo es volver en 48 horas sin culpa."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Ep. 21: Recaídas: volver sin culpa' AND x.status = 'Published'
);

-- 22. [Podcast · Habitos] Ep. 22: El costo de la decisión diaria
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Ep. 22: El costo de la decisión diaria',
    'Automatiza comidas y entrenamientos para ahorrar voluntad y avanzar en piloto automático.',
    'Podcast',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    660,
    'Published',
    22,
    now() - interval '26 days',
    now(),
    now(),
    'Lic. Mateo Ríos',
    'Habitos',
    35,
    2,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Por qué fallamos"}, {"atSeconds": 180, "label": "El sistema simple"}, {"atSeconds": 380, "label": "Diseño de entorno"}, {"atSeconds": 540, "label": "Plan antifallo"}]'::jsonb,
    '["Diseña el entorno: la opción sana debe ser la más fácil.", "No dependas de la voluntad: automatiza comidas y horarios.", "Tras una recaída, el protocolo es volver en 48 horas sin culpa."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Ep. 22: El costo de la decisión diaria' AND x.status = 'Published'
);

-- 23. [Podcast · BienestarEmocional] Ep. 23: Comida emocional: nombrar el hambre
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Ep. 23: Comida emocional: nombrar el hambre',
    'Aprende a distinguir hambre física de hambre emocional y qué hacer en el momento crítico.',
    'Podcast',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    720,
    'Published',
    23,
    now() - interval '25 days',
    now(),
    now(),
    'Dra. Camila Restrepo',
    'BienestarEmocional',
    37,
    2,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Lo que sientes es válido"}, {"atSeconds": 200, "label": "El mecanismo interno"}, {"atSeconds": 415, "label": "Herramienta de 2 minutos"}, {"atSeconds": 590, "label": "Práctica guiada"}]'::jsonb,
    '["Antes de comer por estrés, nombra la emoción y respira 4-7-8.", "Háblate como le hablarías a un amigo que está empezando.", "Proteger tus límites protege tu plan."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Ep. 23: Comida emocional: nombrar el hambre' AND x.status = 'Published'
);

-- 24. [Podcast · BienestarEmocional] Ep. 24: Autocompasión: el motor invisible
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Ep. 24: Autocompasión: el motor invisible',
    'Dejarte de castigar no es rendirse: la ciencia de tratarte como tratarías a un amigo.',
    'Podcast',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    660,
    'Published',
    24,
    now() - interval '24 days',
    now(),
    now(),
    'Dr. Julián Ossa',
    'BienestarEmocional',
    39,
    2,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Lo que sientes es válido"}, {"atSeconds": 180, "label": "El mecanismo interno"}, {"atSeconds": 380, "label": "Herramienta de 2 minutos"}, {"atSeconds": 540, "label": "Práctica guiada"}]'::jsonb,
    '["Antes de comer por estrés, nombra la emoción y respira 4-7-8.", "Háblate como le hablarías a un amigo que está empezando.", "Proteger tus límites protege tu plan."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Ep. 24: Autocompasión: el motor invisible' AND x.status = 'Published'
);

-- 25. [Podcast · BienestarEmocional] Ep. 25: Límites sanos y energía
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Ep. 25: Límites sanos y energía',
    'Cómo decir no sin culpa para proteger tu tiempo de sueño, comida y entrenamiento.',
    'Podcast',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    600,
    'Published',
    25,
    now() - interval '23 days',
    now(),
    now(),
    'Lic. Valeria Duque',
    'BienestarEmocional',
    41,
    2,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Lo que sientes es válido"}, {"atSeconds": 165, "label": "El mecanismo interno"}, {"atSeconds": 345, "label": "Herramienta de 2 minutos"}, {"atSeconds": 490, "label": "Práctica guiada"}]'::jsonb,
    '["Antes de comer por estrés, nombra la emoción y respira 4-7-8.", "Háblate como le hablarías a un amigo que está empezando.", "Proteger tus límites protege tu plan."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Ep. 25: Límites sanos y energía' AND x.status = 'Published'
);

-- 26. [Podcast · BienestarEmocional] Ep. 26: Emociones: nombrar para regular
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Ep. 26: Emociones: nombrar para regular',
    'El vocabulario emocional reduce el impulso: prácticas de 2 minutos antes de comer o reaccionar.',
    'Podcast',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    540,
    'Published',
    26,
    now() - interval '22 days',
    now(),
    now(),
    'Dr. Andrés Villegas',
    'BienestarEmocional',
    43,
    2,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Lo que sientes es válido"}, {"atSeconds": 150, "label": "El mecanismo interno"}, {"atSeconds": 310, "label": "Herramienta de 2 minutos"}, {"atSeconds": 440, "label": "Práctica guiada"}]'::jsonb,
    '["Antes de comer por estrés, nombra la emoción y respira 4-7-8.", "Háblate como le hablarías a un amigo que está empezando.", "Proteger tus límites protege tu plan."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Ep. 26: Emociones: nombrar para regular' AND x.status = 'Published'
);

-- 27. [Podcast · Motivacion] Ep. 27: Motivación vs. disciplina: qué usar cuándo
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Ep. 27: Motivación vs. disciplina: qué usar cuándo',
    'La motivación enciende, el sistema sostiene: cómo combinarlas para no depender del ánimo.',
    'Podcast',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    600,
    'Published',
    27,
    now() - interval '21 days',
    now(),
    now(),
    'Dra. Paula Ocampo',
    'Motivacion',
    45,
    2,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "El momento crítico"}, {"atSeconds": 165, "label": "Reencuadre"}, {"atSeconds": 345, "label": "Tu porqué"}, {"atSeconds": 490, "label": "Próximo paso"}]'::jsonb,
    '["La motivación te enciende; el sistema te sostiene.", "Escribe tu porqué profundo y tenlo visible.", "Celebra victorias pequeñas: son datos de que el proceso funciona."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Ep. 27: Motivación vs. disciplina: qué usar cuándo' AND x.status = 'Published'
);

-- 28. [Podcast · Motivacion] Ep. 28: Tu porqué profundo
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Ep. 28: Tu porqué profundo',
    'Excava más allá de la báscula: el propósito que te mantiene cuando el entusiasmo baja.',
    'Podcast',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    720,
    'Published',
    28,
    now() - interval '20 days',
    now(),
    now(),
    'Dr. Alejandro Gómez',
    'Motivacion',
    46,
    2,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "El momento crítico"}, {"atSeconds": 200, "label": "Reencuadre"}, {"atSeconds": 415, "label": "Tu porqué"}, {"atSeconds": 590, "label": "Próximo paso"}]'::jsonb,
    '["La motivación te enciende; el sistema te sostiene.", "Escribe tu porqué profundo y tenlo visible.", "Celebra victorias pequeñas: son datos de que el proceso funciona."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Ep. 28: Tu porqué profundo' AND x.status = 'Published'
);

-- 29. [Podcast · Motivacion] Ep. 29: Progreso invisible: confía en el proceso
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Ep. 29: Progreso invisible: confía en el proceso',
    'Las mejoras metabólicas que la báscula no muestra y cómo medirlas mes a mes.',
    'Podcast',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    540,
    'Published',
    29,
    now() - interval '19 days',
    now(),
    now(),
    'Dra. Sofía Morales',
    'Motivacion',
    48,
    2,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "El momento crítico"}, {"atSeconds": 150, "label": "Reencuadre"}, {"atSeconds": 310, "label": "Tu porqué"}, {"atSeconds": 440, "label": "Próximo paso"}]'::jsonb,
    '["La motivación te enciende; el sistema te sostiene.", "Escribe tu porqué profundo y tenlo visible.", "Celebra victorias pequeñas: son datos de que el proceso funciona."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Ep. 29: Progreso invisible: confía en el proceso' AND x.status = 'Published'
);

-- 30. [Podcast · Psicologia] Ep. 30: La trampa del todo o nada
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Ep. 30: La trampa del todo o nada',
    'Rompe la mentalidad de dieta perfecta: un desliz no es un fracaso, es un dato.',
    'Podcast',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    660,
    'Published',
    30,
    now() - interval '18 days',
    now(),
    now(),
    'Dr. Carlos Valencia',
    'Psicologia',
    50,
    2,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Qué está pasando"}, {"atSeconds": 180, "label": "La conexión mente-cuerpo"}, {"atSeconds": 380, "label": "Evidencia"}, {"atSeconds": 540, "label": "Estrategia práctica"}]'::jsonb,
    '["Un desliz no borra el progreso: es información, no fracaso.", "El estrés crónico eleva cortisol, antojos y grasa abdominal.", "Dormir mal amplifica el hambre y el malhumor del día siguiente."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Ep. 30: La trampa del todo o nada' AND x.status = 'Published'
);

-- 31. [Podcast · Psicologia] Ep. 31: Estrés crónico y metabolismo
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Ep. 31: Estrés crónico y metabolismo',
    'Cortisol, antojos y grasa abdominal: el circuito y tres interruptores para apagarlo.',
    'Podcast',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    780,
    'Published',
    31,
    now() - interval '17 days',
    now(),
    now(),
    'Dra. Elena Ruiz',
    'Psicologia',
    52,
    2,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Qué está pasando"}, {"atSeconds": 215, "label": "La conexión mente-cuerpo"}, {"atSeconds": 450, "label": "Evidencia"}, {"atSeconds": 635, "label": "Estrategia práctica"}]'::jsonb,
    '["Un desliz no borra el progreso: es información, no fracaso.", "El estrés crónico eleva cortisol, antojos y grasa abdominal.", "Dormir mal amplifica el hambre y el malhumor del día siguiente."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Ep. 31: Estrés crónico y metabolismo' AND x.status = 'Published'
);

-- 32. [Podcast · Psicologia] Ep. 32: Sueño y mente: la dupla olvidada
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Ep. 32: Sueño y mente: la dupla olvidada',
    'Cómo la privación de sueño amplifica el hambre y el mal humor, y cómo blindarte.',
    'Podcast',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    600,
    'Published',
    32,
    now() - interval '16 days',
    now(),
    now(),
    'Lic. Mateo Ríos',
    'Psicologia',
    54,
    2,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Qué está pasando"}, {"atSeconds": 165, "label": "La conexión mente-cuerpo"}, {"atSeconds": 345, "label": "Evidencia"}, {"atSeconds": 490, "label": "Estrategia práctica"}]'::jsonb,
    '["Un desliz no borra el progreso: es información, no fracaso.", "El estrés crónico eleva cortisol, antojos y grasa abdominal.", "Dormir mal amplifica el hambre y el malhumor del día siguiente."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Ep. 32: Sueño y mente: la dupla olvidada' AND x.status = 'Published'
);

-- 33. [Podcast · CrecimientoPersonal] Ep. 33: La identidad: ser la persona sana
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Ep. 33: La identidad: ser la persona sana',
    'El cambio duradero no empieza en la dieta, empieza en cómo te defines.',
    'Podcast',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    720,
    'Published',
    33,
    now() - interval '15 days',
    now(),
    now(),
    'Dra. Camila Restrepo',
    'CrecimientoPersonal',
    56,
    2,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "La pregunta inicial"}, {"atSeconds": 200, "label": "Identidad vs. resultados"}, {"atSeconds": 415, "label": "Ejercicio guiado"}, {"atSeconds": 590, "label": "Compromiso"}]'::jsonb,
    '["No persigas resultados: conviértete en la clase de persona que los logra.", "Visualiza a tu yo de 6 meses antes de decidir en la mesa.", "Cada elección es un voto por tu identidad sana."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Ep. 33: La identidad: ser la persona sana' AND x.status = 'Published'
);

-- 34. [Podcast · CrecimientoPersonal] Ep. 34: Tu yo del futuro te escribe
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Ep. 34: Tu yo del futuro te escribe',
    'Un ejercicio guiado de visualización para alinear las decisiones de hoy con tu meta de 6 meses.',
    'Podcast',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    600,
    'Published',
    34,
    now() - interval '14 days',
    now(),
    now(),
    'Dr. Julián Ossa',
    'CrecimientoPersonal',
    58,
    3,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "La pregunta inicial"}, {"atSeconds": 165, "label": "Identidad vs. resultados"}, {"atSeconds": 345, "label": "Ejercicio guiado"}, {"atSeconds": 490, "label": "Compromiso"}]'::jsonb,
    '["No persigas resultados: conviértete en la clase de persona que los logra.", "Visualiza a tu yo de 6 meses antes de decidir en la mesa.", "Cada elección es un voto por tu identidad sana."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Ep. 34: Tu yo del futuro te escribe' AND x.status = 'Published'
);

-- 35. [Podcast · Mindfulness] Ep. 35: Comer consciente en 5 pasos
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Ep. 35: Comer consciente en 5 pasos',
    'Del primer bocado al plato vacío: practica presencia para comer menos y disfrutar más.',
    'Podcast',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    540,
    'Published',
    35,
    now() - interval '13 days',
    now(),
    now(),
    'Lic. Valeria Duque',
    'Mindfulness',
    60,
    3,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Prepara el espacio"}, {"atSeconds": 150, "label": "Práctica principal"}, {"atSeconds": 310, "label": "Integración diaria"}, {"atSeconds": 440, "label": "Cierre"}]'::jsonb,
    '["Come sentado, sin pantallas y con el primer bocado consciente.", "Los antojos son oleadas: obsérvalas 10 minutos y pierden fuerza.", "Tres respiraciones profundas antes de cada comida regulan el impulso."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Ep. 35: Comer consciente en 5 pasos' AND x.status = 'Published'
);

-- 36. [Podcast · Mindfulness] Ep. 36: Mindfulness para antojos
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Ep. 36: Mindfulness para antojos',
    'Surfear el antojo: observarlo, respirarlo y dejarlo pasar sin ceder en 10 minutos.',
    'Podcast',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    480,
    'Published',
    36,
    now() - interval '12 days',
    now(),
    now(),
    'Dr. Andrés Villegas',
    'Mindfulness',
    62,
    3,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Prepara el espacio"}, {"atSeconds": 130, "label": "Práctica principal"}, {"atSeconds": 275, "label": "Integración diaria"}, {"atSeconds": 390, "label": "Cierre"}]'::jsonb,
    '["Come sentado, sin pantallas y con el primer bocado consciente.", "Los antojos son oleadas: obsérvalas 10 minutos y pierden fuerza.", "Tres respiraciones profundas antes de cada comida regulan el impulso."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Ep. 36: Mindfulness para antojos' AND x.status = 'Published'
);

-- 37. [Podcast · Biologia] Ep. 37: Insulina: el interruptor metabólico
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Ep. 37: Insulina: el interruptor metabólico',
    'Cómo funciona la insulina y por qué entenderla cambia tus decisiones de cada comida.',
    'Podcast',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    900,
    'Published',
    37,
    now() - interval '11 days',
    now(),
    now(),
    'Dra. Paula Ocampo',
    'Biologia',
    64,
    3,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "El interruptor metabólico"}, {"atSeconds": 250, "label": "Insulina en acción"}, {"atSeconds": 520, "label": "Datos y matices"}, {"atSeconds": 735, "label": "Aplicación práctica"}]'::jsonb,
    '["La insulina alta bloquea la quema de grasa: espacia tus comidas.", "Combinar fibra + proteína aplana el pico de glucosa.", "Mueve el cuerpo después de comer: baja la glucosa sin medicamentos."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Ep. 37: Insulina: el interruptor metabólico' AND x.status = 'Published'
);

-- 38. [Video · SaludFisica] Video: Rutina de fuerza en casa — 20 minutos
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Video: Rutina de fuerza en casa — 20 minutos',
    'Sesión guiada sin equipo: sentadillas, empujes, bisagra de cadera y core para nivel inicial.',
    'Video',
    'media/podcasts/demo-copp.mp3',
    'video/mp4',
    NULL,
    1200,
    'Published',
    38,
    now() - interval '10 days',
    now(),
    now(),
    'Dr. Alejandro Gómez',
    'SaludFisica',
    66,
    3,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Calentamiento mental"}, {"atSeconds": 335, "label": "Técnica correcta"}, {"atSeconds": 695, "label": "Progresión práctica"}, {"atSeconds": 980, "label": "Prevención de lesiones"}]'::jsonb,
    '["Dos sesiones de fuerza por semana ya cambian tu metabolismo.", "Caminar más fuera del entrenamiento pesa más que el entrenamiento.", "Progresa carga o repeticiones cada 2 semanas; nunca a costa de dolor articular."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Video: Rutina de fuerza en casa — 20 minutos' AND x.status = 'Published'
);

-- 39. [Video · SaludFisica] Video: Movilidad matutina — 10 minutos
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Video: Movilidad matutina — 10 minutos',
    'Secuencia suave de cuello a tobillos para despertar articulaciones antes del día.',
    'Video',
    'media/podcasts/demo-copp.mp3',
    'video/mp4',
    NULL,
    600,
    'Published',
    39,
    now() - interval '9 days',
    now(),
    now(),
    'Dra. Sofía Morales',
    'SaludFisica',
    68,
    3,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Calentamiento mental"}, {"atSeconds": 165, "label": "Técnica correcta"}, {"atSeconds": 345, "label": "Progresión práctica"}, {"atSeconds": 490, "label": "Prevención de lesiones"}]'::jsonb,
    '["Dos sesiones de fuerza por semana ya cambian tu metabolismo.", "Caminar más fuera del entrenamiento pesa más que el entrenamiento.", "Progresa carga o repeticiones cada 2 semanas; nunca a costa de dolor articular."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Video: Movilidad matutina — 10 minutos' AND x.status = 'Published'
);

-- 40. [Video · Nutricion] Video: Batch cooking dominical en 60 minutos
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Video: Batch cooking dominical en 60 minutos',
    'Cocina la base de la semana: proteínas, verduras y carbohidratos con lista de compras.',
    'Video',
    'media/podcasts/demo-copp.mp3',
    'video/mp4',
    NULL,
    1080,
    'Published',
    40,
    now() - interval '8 days',
    now(),
    now(),
    'Dr. Carlos Valencia',
    'Nutricion',
    70,
    3,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Intro y contexto"}, {"atSeconds": 300, "label": "El error más común"}, {"atSeconds": 625, "label": "Lo que dice la ciencia"}, {"atSeconds": 885, "label": "Protocolo de la semana"}]'::jsonb,
    '["Prioriza proteína y fibra en cada plato principal.", "Los carbohidratos integrales estabilizan tu glucosa por horas.", "Lee etiquetas: más de 5 ingredientes desconocidos, devuélvelo a la repisa."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Video: Batch cooking dominical en 60 minutos' AND x.status = 'Published'
);

-- 41. [Video · Nutricion] Video: 5 desayunos altos en proteína
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Video: 5 desayunos altos en proteína',
    'Preparaciones de 10 minutos para empezar el día con energía estable.',
    'Video',
    'media/podcasts/demo-copp.mp3',
    'video/mp4',
    NULL,
    840,
    'Published',
    41,
    now() - interval '7 days',
    now(),
    now(),
    'Dra. Elena Ruiz',
    'Nutricion',
    71,
    3,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Intro y contexto"}, {"atSeconds": 235, "label": "El error más común"}, {"atSeconds": 485, "label": "Lo que dice la ciencia"}, {"atSeconds": 685, "label": "Protocolo de la semana"}]'::jsonb,
    '["Prioriza proteína y fibra en cada plato principal.", "Los carbohidratos integrales estabilizan tu glucosa por horas.", "Lee etiquetas: más de 5 ingredientes desconocidos, devuélvelo a la repisa."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Video: 5 desayunos altos en proteína' AND x.status = 'Published'
);

-- 42. [Video · Habitos] Video: Organiza tu cocina a tu favor
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Video: Organiza tu cocina a tu favor',
    'Un recorrido práctico para colocar lo sano a la vista y lo ultraprocesado fuera de alcance.',
    'Video',
    'media/podcasts/demo-copp.mp3',
    'video/mp4',
    NULL,
    480,
    'Published',
    42,
    now() - interval '6 days',
    now(),
    now(),
    'Lic. Mateo Ríos',
    'Habitos',
    73,
    3,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Por qué fallamos"}, {"atSeconds": 130, "label": "El sistema simple"}, {"atSeconds": 275, "label": "Diseño de entorno"}, {"atSeconds": 390, "label": "Plan antifallo"}]'::jsonb,
    '["Diseña el entorno: la opción sana debe ser la más fácil.", "No dependas de la voluntad: automatiza comidas y horarios.", "Tras una recaída, el protocolo es volver en 48 horas sin culpa."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Video: Organiza tu cocina a tu favor' AND x.status = 'Published'
);

-- 43. [Video · Mindfulness] Video: Respiración 4-7-8 guiada
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Video: Respiración 4-7-8 guiada',
    'Práctica de 6 minutos para bajar el estrés antes de comer o dormir.',
    'Video',
    'media/podcasts/demo-copp.mp3',
    'video/mp4',
    NULL,
    480,
    'Published',
    43,
    now() - interval '5 days',
    now(),
    now(),
    'Dra. Camila Restrepo',
    'Mindfulness',
    75,
    3,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Prepara el espacio"}, {"atSeconds": 130, "label": "Práctica principal"}, {"atSeconds": 275, "label": "Integración diaria"}, {"atSeconds": 390, "label": "Cierre"}]'::jsonb,
    '["Come sentado, sin pantallas y con el primer bocado consciente.", "Los antojos son oleadas: obsérvalas 10 minutos y pierden fuerza.", "Tres respiraciones profundas antes de cada comida regulan el impulso."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Video: Respiración 4-7-8 guiada' AND x.status = 'Published'
);

-- 44. [Audio · Mindfulness] Audio guiado: Escaneo corporal de 12 minutos
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Audio guiado: Escaneo corporal de 12 minutos',
    'Práctica de atención plena cuerpo a cuerpo para reducir tensión y comer con claridad.',
    'Audio',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    720,
    'Published',
    44,
    now() - interval '4 days',
    now(),
    now(),
    'Dr. Julián Ossa',
    'Mindfulness',
    77,
    3,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Prepara el espacio"}, {"atSeconds": 200, "label": "Práctica principal"}, {"atSeconds": 415, "label": "Integración diaria"}, {"atSeconds": 590, "label": "Cierre"}]'::jsonb,
    '["Come sentado, sin pantallas y con el primer bocado consciente.", "Los antojos son oleadas: obsérvalas 10 minutos y pierden fuerza.", "Tres respiraciones profundas antes de cada comida regulan el impulso."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Audio guiado: Escaneo corporal de 12 minutos' AND x.status = 'Published'
);

-- 45. [Audio · BienestarEmocional] Audio guiado: Respiración para el estrés
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Audio guiado: Respiración para el estrés',
    'Ejercicio de 8 minutos para calmar el sistema nervioso en momentos de ansiedad.',
    'Audio',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    480,
    'Published',
    45,
    now() - interval '3 days',
    now(),
    now(),
    'Lic. Valeria Duque',
    'BienestarEmocional',
    79,
    3,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Lo que sientes es válido"}, {"atSeconds": 130, "label": "El mecanismo interno"}, {"atSeconds": 275, "label": "Herramienta de 2 minutos"}, {"atSeconds": 390, "label": "Práctica guiada"}]'::jsonb,
    '["Antes de comer por estrés, nombra la emoción y respira 4-7-8.", "Háblate como le hablarías a un amigo que está empezando.", "Proteger tus límites protege tu plan."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Audio guiado: Respiración para el estrés' AND x.status = 'Published'
);

-- 46. [Audio · Motivacion] Audio: Pep talk para días grises
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Audio: Pep talk para días grises',
    'Un empujón de 8 minutos para retomar el plan cuando todo pide rendir.',
    'Audio',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    480,
    'Published',
    46,
    now() - interval '2 days',
    now(),
    now(),
    'Dr. Andrés Villegas',
    'Motivacion',
    81,
    3,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "El momento crítico"}, {"atSeconds": 130, "label": "Reencuadre"}, {"atSeconds": 275, "label": "Tu porqué"}, {"atSeconds": 390, "label": "Próximo paso"}]'::jsonb,
    '["La motivación te enciende; el sistema te sostiene.", "Escribe tu porqué profundo y tenlo visible.", "Celebra victorias pequeñas: son datos de que el proceso funciona."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Audio: Pep talk para días grises' AND x.status = 'Published'
);

-- 47. [Audio · Habitos] Audio: Reencuadre para antojos nocturnos
INSERT INTO app.media_items
    (title, description, media_type, storage_key, content_type, file_size_bytes,
     duration_secs, status, sort_order, published_at, created_at, updated_at,
     author, category, day, month, thumbnail_key, chapters, takeaways)
SELECT
    'Audio: Reencuadre para antojos nocturnos',
    'Guía de 8 minutos para identificar la emoción detrás del antojo de la noche.',
    'Audio',
    'media/podcasts/demo-copp.mp3',
    'audio/mpeg',
    NULL,
    480,
    'Published',
    47,
    now() - interval '1 days',
    now(),
    now(),
    'Dra. Paula Ocampo',
    'Habitos',
    83,
    3,
    'media/thumbnails/demo-copp.jpg',
    '[{"atSeconds": 0, "label": "Por qué fallamos"}, {"atSeconds": 130, "label": "El sistema simple"}, {"atSeconds": 275, "label": "Diseño de entorno"}, {"atSeconds": 390, "label": "Plan antifallo"}]'::jsonb,
    '["Diseña el entorno: la opción sana debe ser la más fácil.", "No dependas de la voluntad: automatiza comidas y horarios.", "Tras una recaída, el protocolo es volver en 48 horas sin culpa."]'::jsonb
WHERE NOT EXISTS (
    SELECT 1 FROM app.media_items x
    WHERE x.title = 'Audio: Reencuadre para antojos nocturnos' AND x.status = 'Published'
);

-- ====================================================================
-- B) ASIGNACIÓN DE PODCASTS A TAREAS DE PLANTILLA
--    MISMA lógica de rotación por weekday de DevProgramContentSeeder:
--        media_id = pool[(weekday - 1) % count]
--    pool = podcasts Publicados con la clave demo (orden sort_order,
--    created_at) — idéntico al ORDER BY del seeder. Solo toca tareas
--    task_code='podcast' de plantillas Active. NUNCA toca
--    app.program_weeks (los snapshots de semanas activadas quedan
--    congelados por diseño; solo las activaciones futuras heredan esto).
--    Si cambió algo, bump de program_templates.version (trazabilidad
--    TemplateVersionAtStart, igual que el seeder).
-- ====================================================================
WITH pool AS (
    SELECT m.id,
           row_number() OVER (ORDER BY m.sort_order, m.created_at) - 1 AS rn
    FROM app.media_items m
    WHERE m.media_type = 'Podcast'
      AND m.status = 'Published'
      AND m.storage_key = 'media/podcasts/demo-copp.mp3'
),
upd AS (
    UPDATE app.weekly_day_templates t
    SET media_id = p.id
    FROM pool p
    JOIN app.program_templates pt ON pt.status = 'Active'
    WHERE t.task_code = 'podcast'
      AND t.template_id = pt.id
      AND t.media_id IS DISTINCT FROM p.id
      AND p.rn = (t.weekday - 1) % (SELECT count(*) FROM pool)
    RETURNING t.template_id
)
UPDATE app.program_templates pt
SET version = pt.version + 1,
    updated_at = now()
WHERE pt.id IN (SELECT DISTINCT template_id FROM upd);

-- ====================================================================
-- C) VÍNCULO PACIENTE ↔ PROFESIONAL (Assigned/Active)
--    Solo crea la asignación si el paciente demo NO tiene ninguna;
--    los vínculos existentes (seed_full_demo_data) NO se tocan.
-- ====================================================================
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
),
    profs AS (SELECT p.id FROM erp.professionals p)
INSERT INTO app.patient_professionals
    (patient_id, professional_id, clinic_id, relationship_type, status, created_by, created_at, updated_at)
SELECT d.id, pr.id, d.clinic_id, 'Assigned', 'Active',
       (SELECT u."Id" FROM auth."Users" u ORDER BY u."NormalizedEmail" LIMIT 1), now(), now()
FROM demo d
JOIN LATERAL (
    SELECT p.id FROM profs p
    ORDER BY p.id
    OFFSET (d.ord % (SELECT count(*) FROM profs)) LIMIT 1
) pr ON true
WHERE NOT EXISTS (
    SELECT 1 FROM app.patient_professionals x WHERE x.patient_id = d.id
);

-- ====================================================================
-- D) ENCOUNTERS CANÓNICOS (app.encounters): 4 por paciente en los
--    últimos 3 meses (evaluación inicial, seguimiento, telemedicina,
--    control trimestral), con motivo y notas clínicas coherentes.
-- ====================================================================
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
),
    prof_for AS (
    SELECT DISTINCT ON (pp.patient_id) pp.patient_id, pp.professional_id
    FROM app.patient_professionals pp
    WHERE pp.patient_id IN (SELECT id FROM demo)
    ORDER BY pp.patient_id, pp.created_at
),
    enc AS (
        SELECT * FROM (VALUES
            (0, 'consulta_periodica', 'Evaluación inicial del programa Copp Adresd',
             'Evaluación inicial: antecedentes de sobrepeso metabólico, sin alergias conocidas relevantes. Se acuerda plan nutricional de 1400-1800 kcal, batería inicial de tests y meta de 5% de peso en 12 semanas. Paciente motivado, con apoyo familiar.', 84),
            (1, 'seguimiento', 'Seguimiento de adherencia y hábitos',
             'Control de seguimiento: adherencia al plan nutricional en torno al 70%, sueño mejorable (6 h promedio). Se refuerza higiene del sueño, registro de ingesta diario y rutina de fuerza 2 veces por semana. Continuar medicación según prescripción.', 62),
            (2, 'telemedicina', 'Control de peso y glucosa por telemedicina',
             'Consulta remota: reporta mayor energía y -3 kg desde el inicio. Glucosa en ayunas en descenso. Se ajusta porción de cena, se mantiene ejercicio en zona 2 y se agenda control de laboratorio (HbA1c y perfil lipídico).', 38),
            (3, 'consulta_periodica', 'Control trimestral y ajuste del plan',
             'Control trimestral: evolución favorable de peso y perímetro de cintura. Presión arterial en rango objetivo. Se mantiene plan actual, se avanza a rutina intermedia y se re-evalúa batería de seguimiento a 30 días.', 14)
        ) AS v(k, etype, reason, notes, days_ago)
    )
INSERT INTO app.encounters
    (id, patient_id, professional_id, type, status, started_at, ended_at, reason, notes, created_by, created_at, updated_at)
SELECT gen_random_uuid(), d.id, pf.professional_id, v.etype, 'completed',
       (((current_date - (v.days_ago + (d.ord % 6)))::text || ' ' || ('14:' || lpad((((d.ord + v.k) % 4) * 15)::text, 2, '0')) || ':00')::timestamp AT TIME ZONE 'America/Bogota'),
       (((current_date - (v.days_ago + (d.ord % 6)))::text || ' ' || ('14:' || lpad((((d.ord + v.k) % 4) * 15)::text, 2, '0')) || ':00')::timestamp AT TIME ZONE 'America/Bogota') + interval '45 minutes',
       v.reason, v.notes,
       '7e1c4d20-9a41-4f0e-9b5a-5f3a2c1d0e9f', now(), now()
FROM demo d
JOIN prof_for pf ON pf.patient_id = d.id
JOIN enc v ON true
WHERE (SELECT count(*) FROM app.encounters x
       WHERE x.patient_id = d.id AND x.created_by = '7e1c4d20-9a41-4f0e-9b5a-5f3a2c1d0e9f') < 4
  AND NOT EXISTS (
      SELECT 1 FROM app.encounters x
      WHERE x.patient_id = d.id AND x.type = v.etype
        AND x.started_at = (((current_date - (v.days_ago + (d.ord % 6)))::text || ' ' || ('14:' || lpad((((d.ord + v.k) % 4) * 15)::text, 2, '0')) || ':00')::timestamp AT TIME ZONE 'America/Bogota')
  );

-- ====================================================================
-- E) MEDICIONES CLÍNICAS (app.clinical_measurements, source='rich-seed')
--    Ventana: últimos 3 meses. Varias por semana: peso/cintura/FC
--    semanales; TA sistólica/diastólica quincenal; HbA1c/glucosa
--    mensual. Tendencia de mejora con ruido determinista (sin random).
-- ====================================================================
-- Métrica weight (13 registros/paciente)
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
),
    m AS (SELECT mm.id, mm.code, mm.default_unit_id
          FROM app.measurement_metrics mm WHERE mm.code = 'weight')
INSERT INTO app.clinical_measurements
    (id, patient_id, metric_id, unit_id, value, observed_at, recorded_at, source, source_key)
SELECT gen_random_uuid(), d.id, m.id, m.default_unit_id,
       ROUND(we + (ws - we) * g.k / 12.0 + noise, 1),
       (((current_date - (g.k * 7 + 2))::text || ' ' || ('09:00') || ':00')::timestamp AT TIME ZONE 'America/Bogota'),
       now(),
       'rich-seed',
       'rich:' || d.doc || ':' || m.code || ':' || (current_date - (g.k * 7 + 2))::text
FROM demo d
JOIN m ON true
CROSS JOIN generate_series(0, 12) AS g(k)
CROSS JOIN LATERAL (
    SELECT (82 + (d.ord % 9) * 2.5) AS ws,
           (82 + (d.ord % 9) * 2.5 - (6 + d.ord % 5)) AS we,
           (8.2 - (d.ord % 3) * 0.5) AS a1_start,
           (8.2 - (d.ord % 3) * 0.5 - 1.4) AS a1_end,
           (((d.ord * 13 + g.k * 7) % 100) - 50) / 50.0 AS noise
) w
WHERE NOT EXISTS (
    SELECT 1 FROM app.clinical_measurements x
    WHERE x.patient_id = d.id AND x.metric_id = m.id
      AND x.observed_at = (((current_date - (g.k * 7 + 2))::text || ' ' || ('09:00') || ':00')::timestamp AT TIME ZONE 'America/Bogota')
)
  AND (SELECT count(*) FROM app.clinical_measurements y
       WHERE y.patient_id = d.id AND y.source = 'rich-seed'
         AND y.metric_id = m.id) < 13;

-- Métrica waist (13 registros/paciente)
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
),
    m AS (SELECT mm.id, mm.code, mm.default_unit_id
          FROM app.measurement_metrics mm WHERE mm.code = 'waist')
INSERT INTO app.clinical_measurements
    (id, patient_id, metric_id, unit_id, value, observed_at, recorded_at, source, source_key)
SELECT gen_random_uuid(), d.id, m.id, m.default_unit_id,
       ROUND((we + 8) + ((ws + 12) - (we + 8)) * g.k / 12.0 + noise * 2, 1),
       (((current_date - (g.k * 7 + 3))::text || ' ' || ('09:30') || ':00')::timestamp AT TIME ZONE 'America/Bogota'),
       now(),
       'rich-seed',
       'rich:' || d.doc || ':' || m.code || ':' || (current_date - (g.k * 7 + 3))::text
FROM demo d
JOIN m ON true
CROSS JOIN generate_series(0, 12) AS g(k)
CROSS JOIN LATERAL (
    SELECT (82 + (d.ord % 9) * 2.5) AS ws,
           (82 + (d.ord % 9) * 2.5 - (6 + d.ord % 5)) AS we,
           (8.2 - (d.ord % 3) * 0.5) AS a1_start,
           (8.2 - (d.ord % 3) * 0.5 - 1.4) AS a1_end,
           (((d.ord * 13 + g.k * 7) % 100) - 50) / 50.0 AS noise
) w
WHERE NOT EXISTS (
    SELECT 1 FROM app.clinical_measurements x
    WHERE x.patient_id = d.id AND x.metric_id = m.id
      AND x.observed_at = (((current_date - (g.k * 7 + 3))::text || ' ' || ('09:30') || ':00')::timestamp AT TIME ZONE 'America/Bogota')
)
  AND (SELECT count(*) FROM app.clinical_measurements y
       WHERE y.patient_id = d.id AND y.source = 'rich-seed'
         AND y.metric_id = m.id) < 13;

-- Métrica heart_rate (13 registros/paciente)
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
),
    m AS (SELECT mm.id, mm.code, mm.default_unit_id
          FROM app.measurement_metrics mm WHERE mm.code = 'heart_rate')
INSERT INTO app.clinical_measurements
    (id, patient_id, metric_id, unit_id, value, observed_at, recorded_at, source, source_key)
SELECT gen_random_uuid(), d.id, m.id, m.default_unit_id,
       ROUND(67 + 11 * g.k / 12.0 + noise * 3, 0),
       (((current_date - (g.k * 7 + 4))::text || ' ' || ('08:00') || ':00')::timestamp AT TIME ZONE 'America/Bogota'),
       now(),
       'rich-seed',
       'rich:' || d.doc || ':' || m.code || ':' || (current_date - (g.k * 7 + 4))::text
FROM demo d
JOIN m ON true
CROSS JOIN generate_series(0, 12) AS g(k)
CROSS JOIN LATERAL (
    SELECT (82 + (d.ord % 9) * 2.5) AS ws,
           (82 + (d.ord % 9) * 2.5 - (6 + d.ord % 5)) AS we,
           (8.2 - (d.ord % 3) * 0.5) AS a1_start,
           (8.2 - (d.ord % 3) * 0.5 - 1.4) AS a1_end,
           (((d.ord * 13 + g.k * 7) % 100) - 50) / 50.0 AS noise
) w
WHERE NOT EXISTS (
    SELECT 1 FROM app.clinical_measurements x
    WHERE x.patient_id = d.id AND x.metric_id = m.id
      AND x.observed_at = (((current_date - (g.k * 7 + 4))::text || ' ' || ('08:00') || ':00')::timestamp AT TIME ZONE 'America/Bogota')
)
  AND (SELECT count(*) FROM app.clinical_measurements y
       WHERE y.patient_id = d.id AND y.source = 'rich-seed'
         AND y.metric_id = m.id) < 13;

-- Métrica systolic_bp (7 registros/paciente)
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
),
    m AS (SELECT mm.id, mm.code, mm.default_unit_id
          FROM app.measurement_metrics mm WHERE mm.code = 'systolic_bp')
INSERT INTO app.clinical_measurements
    (id, patient_id, metric_id, unit_id, value, observed_at, recorded_at, source, source_key)
SELECT gen_random_uuid(), d.id, m.id, m.default_unit_id,
       ROUND(120 + 18 * g.k / 6.0 + noise * 8, 0),
       (((current_date - (g.k * 14 + 2))::text || ' ' || ('08:30') || ':00')::timestamp AT TIME ZONE 'America/Bogota'),
       now(),
       'rich-seed',
       'rich:' || d.doc || ':' || m.code || ':' || (current_date - (g.k * 14 + 2))::text
FROM demo d
JOIN m ON true
CROSS JOIN generate_series(0, 6) AS g(k)
CROSS JOIN LATERAL (
    SELECT (82 + (d.ord % 9) * 2.5) AS ws,
           (82 + (d.ord % 9) * 2.5 - (6 + d.ord % 5)) AS we,
           (8.2 - (d.ord % 3) * 0.5) AS a1_start,
           (8.2 - (d.ord % 3) * 0.5 - 1.4) AS a1_end,
           (((d.ord * 13 + g.k * 7) % 100) - 50) / 50.0 AS noise
) w
WHERE NOT EXISTS (
    SELECT 1 FROM app.clinical_measurements x
    WHERE x.patient_id = d.id AND x.metric_id = m.id
      AND x.observed_at = (((current_date - (g.k * 14 + 2))::text || ' ' || ('08:30') || ':00')::timestamp AT TIME ZONE 'America/Bogota')
)
  AND (SELECT count(*) FROM app.clinical_measurements y
       WHERE y.patient_id = d.id AND y.source = 'rich-seed'
         AND y.metric_id = m.id) < 7;

-- Métrica diastolic_bp (7 registros/paciente)
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
),
    m AS (SELECT mm.id, mm.code, mm.default_unit_id
          FROM app.measurement_metrics mm WHERE mm.code = 'diastolic_bp')
INSERT INTO app.clinical_measurements
    (id, patient_id, metric_id, unit_id, value, observed_at, recorded_at, source, source_key)
SELECT gen_random_uuid(), d.id, m.id, m.default_unit_id,
       ROUND(76 + 12 * g.k / 6.0 + noise * 6, 0),
       (((current_date - (g.k * 14 + 2))::text || ' ' || ('08:35') || ':00')::timestamp AT TIME ZONE 'America/Bogota'),
       now(),
       'rich-seed',
       'rich:' || d.doc || ':' || m.code || ':' || (current_date - (g.k * 14 + 2))::text
FROM demo d
JOIN m ON true
CROSS JOIN generate_series(0, 6) AS g(k)
CROSS JOIN LATERAL (
    SELECT (82 + (d.ord % 9) * 2.5) AS ws,
           (82 + (d.ord % 9) * 2.5 - (6 + d.ord % 5)) AS we,
           (8.2 - (d.ord % 3) * 0.5) AS a1_start,
           (8.2 - (d.ord % 3) * 0.5 - 1.4) AS a1_end,
           (((d.ord * 13 + g.k * 7) % 100) - 50) / 50.0 AS noise
) w
WHERE NOT EXISTS (
    SELECT 1 FROM app.clinical_measurements x
    WHERE x.patient_id = d.id AND x.metric_id = m.id
      AND x.observed_at = (((current_date - (g.k * 14 + 2))::text || ' ' || ('08:35') || ':00')::timestamp AT TIME ZONE 'America/Bogota')
)
  AND (SELECT count(*) FROM app.clinical_measurements y
       WHERE y.patient_id = d.id AND y.source = 'rich-seed'
         AND y.metric_id = m.id) < 7;

-- Métrica hba1c (3 registros/paciente)
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
),
    m AS (SELECT mm.id, mm.code, mm.default_unit_id
          FROM app.measurement_metrics mm WHERE mm.code = 'hba1c')
INSERT INTO app.clinical_measurements
    (id, patient_id, metric_id, unit_id, value, observed_at, recorded_at, source, source_key)
SELECT gen_random_uuid(), d.id, m.id, m.default_unit_id,
       ROUND((a1_end + (a1_start - a1_end) * g.k / 2.0 + noise * 0.4)::numeric, 1),
       (((current_date - (g.k * 30 + 6))::text || ' ' || ('07:30') || ':00')::timestamp AT TIME ZONE 'America/Bogota'),
       now(),
       'rich-seed',
       'rich:' || d.doc || ':' || m.code || ':' || (current_date - (g.k * 30 + 6))::text
FROM demo d
JOIN m ON true
CROSS JOIN generate_series(0, 2) AS g(k)
CROSS JOIN LATERAL (
    SELECT (82 + (d.ord % 9) * 2.5) AS ws,
           (82 + (d.ord % 9) * 2.5 - (6 + d.ord % 5)) AS we,
           (8.2 - (d.ord % 3) * 0.5) AS a1_start,
           (8.2 - (d.ord % 3) * 0.5 - 1.4) AS a1_end,
           (((d.ord * 13 + g.k * 7) % 100) - 50) / 50.0 AS noise
) w
WHERE NOT EXISTS (
    SELECT 1 FROM app.clinical_measurements x
    WHERE x.patient_id = d.id AND x.metric_id = m.id
      AND x.observed_at = (((current_date - (g.k * 30 + 6))::text || ' ' || ('07:30') || ':00')::timestamp AT TIME ZONE 'America/Bogota')
)
  AND (SELECT count(*) FROM app.clinical_measurements y
       WHERE y.patient_id = d.id AND y.source = 'rich-seed'
         AND y.metric_id = m.id) < 3;

-- Métrica glucose_fasting (3 registros/paciente)
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
),
    m AS (SELECT mm.id, mm.code, mm.default_unit_id
          FROM app.measurement_metrics mm WHERE mm.code = 'glucose_fasting')
INSERT INTO app.clinical_measurements
    (id, patient_id, metric_id, unit_id, value, observed_at, recorded_at, source, source_key)
SELECT gen_random_uuid(), d.id, m.id, m.default_unit_id,
       ROUND(96 + 39 * g.k / 2.0 + noise * 10, 0),
       (((current_date - (g.k * 30 + 7))::text || ' ' || ('07:00') || ':00')::timestamp AT TIME ZONE 'America/Bogota'),
       now(),
       'rich-seed',
       'rich:' || d.doc || ':' || m.code || ':' || (current_date - (g.k * 30 + 7))::text
FROM demo d
JOIN m ON true
CROSS JOIN generate_series(0, 2) AS g(k)
CROSS JOIN LATERAL (
    SELECT (82 + (d.ord % 9) * 2.5) AS ws,
           (82 + (d.ord % 9) * 2.5 - (6 + d.ord % 5)) AS we,
           (8.2 - (d.ord % 3) * 0.5) AS a1_start,
           (8.2 - (d.ord % 3) * 0.5 - 1.4) AS a1_end,
           (((d.ord * 13 + g.k * 7) % 100) - 50) / 50.0 AS noise
) w
WHERE NOT EXISTS (
    SELECT 1 FROM app.clinical_measurements x
    WHERE x.patient_id = d.id AND x.metric_id = m.id
      AND x.observed_at = (((current_date - (g.k * 30 + 7))::text || ' ' || ('07:00') || ':00')::timestamp AT TIME ZONE 'America/Bogota')
)
  AND (SELECT count(*) FROM app.clinical_measurements y
       WHERE y.patient_id = d.id AND y.source = 'rich-seed'
         AND y.metric_id = m.id) < 3;


-- ====================================================================
-- E2) SIGNOS VITALES SEMANALES (app.vital_signs): 13 registros/paciente
--     coherentes con la tendencia de clinical_measurements.
-- ====================================================================
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
)
INSERT INTO app.vital_signs
    (id, patient_id, measured_at, systolic, diastolic, heart_rate,
     temperature_c, o2_saturation, height_cm, weight_kg, created_at)
SELECT gen_random_uuid(), d.id,
       (((current_date - (g.k * 7 + 1))::text || ' ' || ('08:00') || ':00')::timestamp AT TIME ZONE 'America/Bogota'),
       ROUND(120 + 18 * g.k / 12.0 + noise * 8, 0)::int,
       ROUND(76 + 12 * g.k / 12.0 + noise * 6, 0)::int,
       ROUND(67 + 11 * g.k / 12.0 + noise * 6, 0)::int,
       36.5,
       ROUND(97 - 3 * g.k / 12.0, 0)::int,
       (165 + d.ord % 10)::numeric,
       ROUND(we + (ws - we) * g.k / 12.0 + noise, 1),
       now()
FROM demo d
CROSS JOIN generate_series(0, 12) AS g(k)
CROSS JOIN LATERAL (
    SELECT (82 + (d.ord % 9) * 2.5) AS ws,
           (82 + (d.ord % 9) * 2.5 - (6 + d.ord % 5)) AS we,
           (((d.ord * 13 + g.k * 7) % 100) - 50) / 50.0 AS noise
) w
WHERE NOT EXISTS (
    SELECT 1 FROM app.vital_signs x
    WHERE x.patient_id = d.id AND x.measured_at = (((current_date - (g.k * 7 + 1))::text || ' ' || ('08:00') || ':00')::timestamp AT TIME ZONE 'America/Bogota')
)
  AND (SELECT count(*) FROM app.vital_signs y WHERE y.patient_id = d.id) < 13;

-- ====================================================================
-- F) DIAGNÓSTICOS (ICD-10), ALERGIAS y MEDICAMENTOS por paciente.
--    Solo catálogos existentes (app.icd10_codes/app.allergens/app.
--    medications); si un código no existe en la BD, no se inserta.
-- ====================================================================
-- F1) Diagnósticos: 1 primario + 2 secundarios por paciente
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
)
INSERT INTO app.patient_diagnoses
    (id, patient_id, icd10_code_id, is_primary, created_at)
SELECT gen_random_uuid(), d.id, c.id, v.is_primary,
       now() - ((80 - v.sel) || ' days')::interval
FROM demo d
JOIN (VALUES
    ('E11.9', true, 0), ('I10', true, 1), ('E66.9', true, 2), ('E78.5', false, 10), ('F32.9', false, 11), ('N18.3', false, 12)
) AS v(code, is_primary, sel) ON true
JOIN app.icd10_codes c ON c.code = v.code
WHERE ((v.sel < 3 AND v.sel = d.ord % 3)
   OR (v.sel >= 10 AND (v.sel - 10) IN
        ((d.ord / 3) % 3, ((d.ord / 3) + 1) % 3)))
  AND NOT EXISTS (
      SELECT 1 FROM app.patient_diagnoses x
      WHERE x.patient_id = d.id AND x.icd10_code_id = c.id
  );

-- F2) Alergias: 2 por paciente (ON CONFLICT sobre clave única real)
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
)
INSERT INTO app.patient_allergies
    (id, patient_id, allergen_id, notes, created_at)
SELECT gen_random_uuid(), d.id, a.id,
       'Referida por el paciente en la evaluación inicial',
       now() - ((79 - v.sel) || ' days')::interval
FROM demo d
JOIN (VALUES
    ('Penicillin', 0), ('NSAIDs', 1), ('Peanuts', 2), ('Milk', 3), ('Eggs', 4), ('Sulfa', 5), ('Latex', 6), ('Shellfish', 7)
) AS v(name, sel) ON true
JOIN app.allergens a ON a.name = v.name
WHERE v.sel = d.ord % 8 OR v.sel = (d.ord + 3) % 8
ON CONFLICT (patient_id, allergen_id) DO NOTHING;

-- F3) Medicamentos: 2 por paciente (freq/variedad por rotación)
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
)
INSERT INTO app.patient_medications
    (id, patient_id, medication_id, frequency, sort_order, created_at)
SELECT gen_random_uuid(), d.id, m.id,
       (ARRAY['1 vez al día','Cada 12 horas','Con el desayuno'])[1 + v.sel % 3],
       CASE WHEN v.sel = d.ord % 8 THEN 0 ELSE 1 END,
       now() - ((78 - v.sel) || ' days')::interval
FROM demo d
JOIN (VALUES
    ('Metformin', 0), ('Losartan', 1), ('Amlodipine', 2), ('Atorvastatin', 3), ('Lisinopril', 4), ('Omeprazole', 5), ('Sertraline', 6), ('Levothyroxine', 7)
) AS v(name, sel) ON true
JOIN LATERAL (
    SELECT mm.id FROM app.medications mm
    WHERE mm.name ILIKE v.name || '%'
    ORDER BY mm.name LIMIT 1
) m ON true
WHERE (v.sel = d.ord % 8 OR v.sel = (d.ord + 1) % 8)
  AND NOT EXISTS (
      SELECT 1 FROM app.patient_medications x
      WHERE x.patient_id = d.id AND x.medication_id = m.id
  );

-- ====================================================================
-- G) TESTS DE SALUD por paciente demo:
--    · bateria-seguimiento   -> COMPLETADA (respuestas + resultados)
--    · bateria-cardiovascular-> EN CURSO (historia-clinica completa,
--      orp a medias, ers pendiente)
--    · bateria-adherencia    -> PENDIENTE (solo asignaciones)
--    Respuestas deterministas: opción cuyo score es el más cercano a
--    una fracción objetivo por paciente (sin random). Scores = suma de
--    las opciones elegidas; severity/qualifier desde los rangos reales.
-- ====================================================================
-- G1) Asegura las 3 baterías de seguimiento
INSERT INTO app.health_test_batteries
    (id, code, name, description, auto_assign_on_patient_create, is_active, created_at)
SELECT * FROM (VALUES
    (gen_random_uuid(), 'bateria-seguimiento', 'Seguimiento 30 días', 'Control mensual: nutrición, movimiento, sueño, adherencia y estrés.', false, true, now()),
    (gen_random_uuid(), 'bateria-cardiovascular', 'Riesgo cardiovascular', 'Tamizaje cardiometabólico: historia clínica, ORP y estrés.', false, true, now()),
    (gen_random_uuid(), 'bateria-adherencia', 'Adherencia y propósito', 'Motivación y barreras: IAC, Copp Adresd y temperamento.', false, true, now())
) AS t(id, code, name, description, auto_assign_on_patient_create, is_active, created_at)
ON CONFLICT (code) DO NOTHING;

-- G2) Ítems de batería (instrumento + versión vigente)
INSERT INTO app.health_test_battery_items
    (id, battery_id, instrument_id, version_id, sort_order, is_required)
SELECT gen_random_uuid(), b.id, i.id, v.id, x.pos, true
FROM (VALUES
    ('bateria-seguimiento', 'nutricional', 0),
    ('bateria-seguimiento', 'movimiento', 1),
    ('bateria-seguimiento', 'sueno', 2),
    ('bateria-seguimiento', 'iac-adresd', 3),
    ('bateria-seguimiento', 'ers', 4),
    ('bateria-cardiovascular', 'historia-clinica', 0),
    ('bateria-cardiovascular', 'orp', 1),
    ('bateria-cardiovascular', 'ers', 2),
    ('bateria-adherencia', 'iac-adresd', 0),
    ('bateria-adherencia', 'bateria-antares', 1),
    ('bateria-adherencia', 'temperamento', 2)
) AS x(bcode, icode, pos)
JOIN app.health_test_batteries b ON b.code = x.bcode
JOIN app.health_test_instruments i ON i.code = x.icode
JOIN app.health_test_versions v ON v.instrument_id = i.id AND v.is_current
WHERE NOT EXISTS (SELECT 1 FROM app.health_test_battery_items bi
                  WHERE bi.battery_id = b.id AND bi.instrument_id = i.id);

-- G3) Asignaciones de batería (seguimiento completada, cardiovascular en
--     curso, adherencia pendiente) con fechas relativas a hoy
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
)
INSERT INTO app.health_test_battery_assignments
    (id, patient_id, battery_id, status, assigned_by, assigned_at, due_date, completed_at)
SELECT gen_random_uuid(), d.id, b.id, v.status,
       '7e1c4d20-9a41-4f0e-9b5a-5f3a2c1d0e9f',
       (((current_date - (v.days_ago + (d.ord % 5)))::text || ' ' || ('10:00') || ':00')::timestamp AT TIME ZONE 'America/Bogota') + ((((d.ord * 3) % 4)::text || ' hours'))::interval,
       ba.assigned_at + interval '14 days',
       CASE WHEN v.status = 'completed' THEN ba.assigned_at + interval '10 days' END
FROM demo d
JOIN (VALUES
    ('bateria-seguimiento', 'completed', 62),
    ('bateria-cardiovascular', 'in_progress', 56),
    ('bateria-adherencia', 'pending', 50)
) AS v(bcode, status, days_ago) ON true
JOIN app.health_test_batteries b ON b.code = v.bcode
JOIN LATERAL (
    SELECT (((current_date - (v.days_ago + (d.ord % 5)))::text || ' ' || ('10:00') || ':00')::timestamp AT TIME ZONE 'America/Bogota')
           + ((((d.ord * 3) % 4)::text || ' hours'))::interval AS assigned_at
) ba ON true
WHERE NOT EXISTS (SELECT 1 FROM app.health_test_battery_assignments x
                  WHERE x.patient_id = d.id AND x.battery_id = b.id);

-- G4) Asignaciones por instrumento (estado según rol de la batería)
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
)
INSERT INTO app.health_test_assignments
    (id, patient_id, battery_assignment_id, version_id, status, priority, assigned_by,
     assigned_at, due_date, expires_at, started_at, completed_at, notes)
SELECT gen_random_uuid(), ba.patient_id, ba.id, bi.version_id,
       CASE
         WHEN ba.status = 'completed' THEN 'completed'
         WHEN ba.status = 'in_progress' AND i.code = 'orp' THEN 'in_progress'
         ELSE 'pending'
       END,
       bi.sort_order, '7e1c4d20-9a41-4f0e-9b5a-5f3a2c1d0e9f',
       ba.assigned_at, ba.due_date, ba.due_date + interval '16 days',
       CASE WHEN ba.status = 'completed'
               OR (ba.status = 'in_progress' AND i.code = 'orp')
            THEN ba.assigned_at + interval '2 hours' END,
       CASE WHEN ba.status = 'completed'
            THEN ba.assigned_at + interval '2 hours' + interval '11 minutes' END,
       NULL
FROM app.health_test_battery_assignments ba
JOIN app.health_test_batteries b ON b.id = ba.battery_id
    AND b.code IN ('bateria-seguimiento', 'bateria-cardiovascular', 'bateria-adherencia')
JOIN app.health_test_battery_items bi ON bi.battery_id = b.id
JOIN app.health_test_versions v ON v.id = bi.version_id
JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE ba.patient_id IN (SELECT id FROM demo)
  AND NOT EXISTS (SELECT 1 FROM app.health_test_assignments x
                  WHERE x.patient_id = ba.patient_id
                    AND x.battery_assignment_id = ba.id
                    AND x.version_id = bi.version_id);

-- G5) Evaluaciones: completadas (seguimiento + historia-clinica) y la
--     parcial 'started' para orp
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
)
INSERT INTO app.health_test_evaluations
    (id, assignment_id, patient_id, version_id, status, started_at, completed_at)
SELECT gen_random_uuid(), a.id, a.patient_id, a.version_id,
       CASE WHEN i.code = 'orp' THEN 'started' ELSE 'completed' END,
       a.started_at,
       CASE WHEN i.code = 'orp' THEN NULL ELSE a.completed_at END
FROM app.health_test_assignments a
JOIN app.health_test_battery_assignments ba ON ba.id = a.battery_assignment_id
JOIN app.health_test_batteries b ON b.id = ba.battery_id
JOIN app.health_test_versions v ON v.id = a.version_id
JOIN app.health_test_instruments i ON i.id = v.instrument_id
WHERE a.status IN ('completed', 'in_progress')
  AND ba.patient_id IN (SELECT id FROM demo)
  AND (b.code = 'bateria-seguimiento'
       OR (b.code = 'bateria-cardiovascular' AND i.code IN ('historia-clinica', 'orp')))
  AND NOT EXISTS (SELECT 1 FROM app.health_test_evaluations x WHERE x.assignment_id = a.id);

-- G6) Respuestas: nutricional (evaluaciones completadas de este dataset)
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
),
    evs AS (
        SELECT ev.id, ev.patient_id, ev.started_at
        FROM app.health_test_evaluations ev
        JOIN app.health_test_assignments a ON a.id = ev.assignment_id
        JOIN app.health_test_battery_assignments ba ON ba.id = a.battery_assignment_id
        JOIN app.health_test_batteries b ON b.id = ba.battery_id
        JOIN app.health_test_versions v ON v.id = ev.version_id
        JOIN app.health_test_instruments i ON i.id = v.instrument_id AND i.code = 'nutricional'
        WHERE ev.status = 'completed'
          AND ev.patient_id IN (SELECT id FROM demo)
          AND b.code IN ('bateria-seguimiento', 'bateria-cardiovascular')
    ),
    qs AS (
        SELECT qt.id, qt.type, qt.section, qt.sort_order, qt.code AS qcode, qt.default_value,
               (SELECT max(o.score_value) FROM app.health_test_answer_options o
                WHERE o.question_id = qt.id AND o.is_active) AS max_score
        FROM app.health_test_questions qt
        WHERE qt.version_id = (SELECT v2.id FROM app.health_test_versions v2
                               JOIN app.health_test_instruments i2 ON i2.id = v2.instrument_id
                               WHERE i2.code = 'nutricional' AND v2.is_current)
          AND qt.is_active
    ),
    pick AS (
        SELECT DISTINCT ON (evs.id, qs.id)
               evs.id AS evaluation_id, qs.id AS question_id, o.id AS answer_option_id,
               evs.started_at
        FROM evs
        CROSS JOIN qs
        JOIN demo d ON d.id = evs.patient_id
        JOIN app.health_test_answer_options o ON o.question_id = qs.id AND o.is_active
        WHERE qs.type IN ('scale', 'single', 'multi')
        ORDER BY evs.id, qs.id,
                 abs(o.score_value - ((ARRAY[0.15, 0.35, 0.55, 0.75, 0.9])[1 + ((d.ord + 0) % 5)] * qs.max_score)),
                 o.sort_order
    )
INSERT INTO app.health_test_responses
    (id, evaluation_id, question_id, answer_option_id, created_at)
SELECT gen_random_uuid(), p.evaluation_id, p.question_id, p.answer_option_id, p.started_at
FROM pick p
WHERE NOT EXISTS (SELECT 1 FROM app.health_test_responses r
                  WHERE r.evaluation_id = p.evaluation_id AND r.question_id = p.question_id);

-- G6) Respuestas: movimiento (evaluaciones completadas de este dataset)
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
),
    evs AS (
        SELECT ev.id, ev.patient_id, ev.started_at
        FROM app.health_test_evaluations ev
        JOIN app.health_test_assignments a ON a.id = ev.assignment_id
        JOIN app.health_test_battery_assignments ba ON ba.id = a.battery_assignment_id
        JOIN app.health_test_batteries b ON b.id = ba.battery_id
        JOIN app.health_test_versions v ON v.id = ev.version_id
        JOIN app.health_test_instruments i ON i.id = v.instrument_id AND i.code = 'movimiento'
        WHERE ev.status = 'completed'
          AND ev.patient_id IN (SELECT id FROM demo)
          AND b.code IN ('bateria-seguimiento', 'bateria-cardiovascular')
    ),
    qs AS (
        SELECT qt.id, qt.type, qt.section, qt.sort_order, qt.code AS qcode, qt.default_value,
               (SELECT max(o.score_value) FROM app.health_test_answer_options o
                WHERE o.question_id = qt.id AND o.is_active) AS max_score
        FROM app.health_test_questions qt
        WHERE qt.version_id = (SELECT v2.id FROM app.health_test_versions v2
                               JOIN app.health_test_instruments i2 ON i2.id = v2.instrument_id
                               WHERE i2.code = 'movimiento' AND v2.is_current)
          AND qt.is_active
    ),
    pick AS (
        SELECT DISTINCT ON (evs.id, qs.id)
               evs.id AS evaluation_id, qs.id AS question_id, o.id AS answer_option_id,
               evs.started_at
        FROM evs
        CROSS JOIN qs
        JOIN demo d ON d.id = evs.patient_id
        JOIN app.health_test_answer_options o ON o.question_id = qs.id AND o.is_active
        WHERE qs.type IN ('scale', 'single', 'multi')
        ORDER BY evs.id, qs.id,
                 abs(o.score_value - ((ARRAY[0.15, 0.35, 0.55, 0.75, 0.9])[1 + ((d.ord + 1) % 5)] * qs.max_score)),
                 o.sort_order
    )
INSERT INTO app.health_test_responses
    (id, evaluation_id, question_id, answer_option_id, created_at)
SELECT gen_random_uuid(), p.evaluation_id, p.question_id, p.answer_option_id, p.started_at
FROM pick p
WHERE NOT EXISTS (SELECT 1 FROM app.health_test_responses r
                  WHERE r.evaluation_id = p.evaluation_id AND r.question_id = p.question_id);

-- G6) Respuestas: sueno (evaluaciones completadas de este dataset)
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
),
    evs AS (
        SELECT ev.id, ev.patient_id, ev.started_at
        FROM app.health_test_evaluations ev
        JOIN app.health_test_assignments a ON a.id = ev.assignment_id
        JOIN app.health_test_battery_assignments ba ON ba.id = a.battery_assignment_id
        JOIN app.health_test_batteries b ON b.id = ba.battery_id
        JOIN app.health_test_versions v ON v.id = ev.version_id
        JOIN app.health_test_instruments i ON i.id = v.instrument_id AND i.code = 'sueno'
        WHERE ev.status = 'completed'
          AND ev.patient_id IN (SELECT id FROM demo)
          AND b.code IN ('bateria-seguimiento', 'bateria-cardiovascular')
    ),
    qs AS (
        SELECT qt.id, qt.type, qt.section, qt.sort_order, qt.code AS qcode, qt.default_value,
               (SELECT max(o.score_value) FROM app.health_test_answer_options o
                WHERE o.question_id = qt.id AND o.is_active) AS max_score
        FROM app.health_test_questions qt
        WHERE qt.version_id = (SELECT v2.id FROM app.health_test_versions v2
                               JOIN app.health_test_instruments i2 ON i2.id = v2.instrument_id
                               WHERE i2.code = 'sueno' AND v2.is_current)
          AND qt.is_active
    ),
    pick AS (
        SELECT DISTINCT ON (evs.id, qs.id)
               evs.id AS evaluation_id, qs.id AS question_id, o.id AS answer_option_id,
               evs.started_at
        FROM evs
        CROSS JOIN qs
        JOIN demo d ON d.id = evs.patient_id
        JOIN app.health_test_answer_options o ON o.question_id = qs.id AND o.is_active
        WHERE qs.type IN ('scale', 'single', 'multi')
        ORDER BY evs.id, qs.id,
                 abs(o.score_value - ((ARRAY[0.15, 0.35, 0.55, 0.75, 0.9])[1 + ((d.ord + 2) % 5)] * qs.max_score)),
                 o.sort_order
    )
INSERT INTO app.health_test_responses
    (id, evaluation_id, question_id, answer_option_id, created_at)
SELECT gen_random_uuid(), p.evaluation_id, p.question_id, p.answer_option_id, p.started_at
FROM pick p
WHERE NOT EXISTS (SELECT 1 FROM app.health_test_responses r
                  WHERE r.evaluation_id = p.evaluation_id AND r.question_id = p.question_id);

-- G6) Respuestas: iac-adresd (evaluaciones completadas de este dataset)
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
),
    evs AS (
        SELECT ev.id, ev.patient_id, ev.started_at
        FROM app.health_test_evaluations ev
        JOIN app.health_test_assignments a ON a.id = ev.assignment_id
        JOIN app.health_test_battery_assignments ba ON ba.id = a.battery_assignment_id
        JOIN app.health_test_batteries b ON b.id = ba.battery_id
        JOIN app.health_test_versions v ON v.id = ev.version_id
        JOIN app.health_test_instruments i ON i.id = v.instrument_id AND i.code = 'iac-adresd'
        WHERE ev.status = 'completed'
          AND ev.patient_id IN (SELECT id FROM demo)
          AND b.code IN ('bateria-seguimiento', 'bateria-cardiovascular')
    ),
    qs AS (
        SELECT qt.id, qt.type, qt.section, qt.sort_order, qt.code AS qcode, qt.default_value,
               (SELECT max(o.score_value) FROM app.health_test_answer_options o
                WHERE o.question_id = qt.id AND o.is_active) AS max_score
        FROM app.health_test_questions qt
        WHERE qt.version_id = (SELECT v2.id FROM app.health_test_versions v2
                               JOIN app.health_test_instruments i2 ON i2.id = v2.instrument_id
                               WHERE i2.code = 'iac-adresd' AND v2.is_current)
          AND qt.is_active
    ),
    pick AS (
        SELECT DISTINCT ON (evs.id, qs.id)
               evs.id AS evaluation_id, qs.id AS question_id, o.id AS answer_option_id,
               evs.started_at
        FROM evs
        CROSS JOIN qs
        JOIN demo d ON d.id = evs.patient_id
        JOIN app.health_test_answer_options o ON o.question_id = qs.id AND o.is_active
        WHERE qs.type IN ('scale', 'single', 'multi')
        ORDER BY evs.id, qs.id,
                 abs(o.score_value - ((ARRAY[0.15, 0.35, 0.55, 0.75, 0.9])[1 + ((d.ord + 3) % 5)] * qs.max_score)),
                 o.sort_order
    )
INSERT INTO app.health_test_responses
    (id, evaluation_id, question_id, answer_option_id, created_at)
SELECT gen_random_uuid(), p.evaluation_id, p.question_id, p.answer_option_id, p.started_at
FROM pick p
WHERE NOT EXISTS (SELECT 1 FROM app.health_test_responses r
                  WHERE r.evaluation_id = p.evaluation_id AND r.question_id = p.question_id);

-- G6) Respuestas: ers (evaluaciones completadas de este dataset)
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
),
    evs AS (
        SELECT ev.id, ev.patient_id, ev.started_at
        FROM app.health_test_evaluations ev
        JOIN app.health_test_assignments a ON a.id = ev.assignment_id
        JOIN app.health_test_battery_assignments ba ON ba.id = a.battery_assignment_id
        JOIN app.health_test_batteries b ON b.id = ba.battery_id
        JOIN app.health_test_versions v ON v.id = ev.version_id
        JOIN app.health_test_instruments i ON i.id = v.instrument_id AND i.code = 'ers'
        WHERE ev.status = 'completed'
          AND ev.patient_id IN (SELECT id FROM demo)
          AND b.code IN ('bateria-seguimiento', 'bateria-cardiovascular')
    ),
    qs AS (
        SELECT qt.id, qt.type, qt.section, qt.sort_order, qt.code AS qcode, qt.default_value,
               (SELECT max(o.score_value) FROM app.health_test_answer_options o
                WHERE o.question_id = qt.id AND o.is_active) AS max_score
        FROM app.health_test_questions qt
        WHERE qt.version_id = (SELECT v2.id FROM app.health_test_versions v2
                               JOIN app.health_test_instruments i2 ON i2.id = v2.instrument_id
                               WHERE i2.code = 'ers' AND v2.is_current)
          AND qt.is_active
    ),
    pick AS (
        SELECT DISTINCT ON (evs.id, qs.id)
               evs.id AS evaluation_id, qs.id AS question_id, o.id AS answer_option_id,
               evs.started_at
        FROM evs
        CROSS JOIN qs
        JOIN demo d ON d.id = evs.patient_id
        JOIN app.health_test_answer_options o ON o.question_id = qs.id AND o.is_active
        WHERE qs.type IN ('scale', 'single', 'multi')
        ORDER BY evs.id, qs.id,
                 abs(o.score_value - ((ARRAY[0.15, 0.35, 0.55, 0.75, 0.9])[1 + ((d.ord + 4) % 5)] * qs.max_score)),
                 o.sort_order
    )
INSERT INTO app.health_test_responses
    (id, evaluation_id, question_id, answer_option_id, created_at)
SELECT gen_random_uuid(), p.evaluation_id, p.question_id, p.answer_option_id, p.started_at
FROM pick p
WHERE NOT EXISTS (SELECT 1 FROM app.health_test_responses r
                  WHERE r.evaluation_id = p.evaluation_id AND r.question_id = p.question_id);

-- G6) Respuestas: historia-clinica (evaluaciones completadas de este dataset)
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
),
    evs AS (
        SELECT ev.id, ev.patient_id, ev.started_at
        FROM app.health_test_evaluations ev
        JOIN app.health_test_assignments a ON a.id = ev.assignment_id
        JOIN app.health_test_battery_assignments ba ON ba.id = a.battery_assignment_id
        JOIN app.health_test_batteries b ON b.id = ba.battery_id
        JOIN app.health_test_versions v ON v.id = ev.version_id
        JOIN app.health_test_instruments i ON i.id = v.instrument_id AND i.code = 'historia-clinica'
        WHERE ev.status = 'completed'
          AND ev.patient_id IN (SELECT id FROM demo)
          AND b.code IN ('bateria-seguimiento', 'bateria-cardiovascular')
    ),
    qs AS (
        SELECT qt.id, qt.type, qt.section, qt.sort_order, qt.code AS qcode, qt.default_value,
               (SELECT max(o.score_value) FROM app.health_test_answer_options o
                WHERE o.question_id = qt.id AND o.is_active) AS max_score
        FROM app.health_test_questions qt
        WHERE qt.version_id = (SELECT v2.id FROM app.health_test_versions v2
                               JOIN app.health_test_instruments i2 ON i2.id = v2.instrument_id
                               WHERE i2.code = 'historia-clinica' AND v2.is_current)
          AND qt.is_active
    ),
    pick AS (
        SELECT DISTINCT ON (evs.id, qs.id)
               evs.id AS evaluation_id, qs.id AS question_id, o.id AS answer_option_id,
               evs.started_at
        FROM evs
        CROSS JOIN qs
        JOIN demo d ON d.id = evs.patient_id
        JOIN app.health_test_answer_options o ON o.question_id = qs.id AND o.is_active
        WHERE qs.type IN ('scale', 'single', 'multi')
        ORDER BY evs.id, qs.id,
                 abs(o.score_value - ((ARRAY[0.15, 0.35, 0.55, 0.75, 0.9])[1 + ((d.ord + 5) % 5)] * qs.max_score)),
                 o.sort_order
    )
INSERT INTO app.health_test_responses
    (id, evaluation_id, question_id, answer_option_id, created_at)
SELECT gen_random_uuid(), p.evaluation_id, p.question_id, p.answer_option_id, p.started_at
FROM pick p
WHERE NOT EXISTS (SELECT 1 FROM app.health_test_responses r
                  WHERE r.evaluation_id = p.evaluation_id AND r.question_id = p.question_id);

-- G6b) Respuestas numéricas/abiertas (historia-clinica): valores por código
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
),
    evs AS (
        SELECT ev.id, ev.patient_id, ev.started_at
        FROM app.health_test_evaluations ev
        JOIN app.health_test_assignments a ON a.id = ev.assignment_id
        JOIN app.health_test_battery_assignments ba ON ba.id = a.battery_assignment_id
        JOIN app.health_test_batteries b ON b.id = ba.battery_id
        JOIN app.health_test_versions v ON v.id = ev.version_id
        JOIN app.health_test_instruments i ON i.id = v.instrument_id AND i.code = 'historia-clinica'
        WHERE ev.status = 'completed'
          AND ev.patient_id IN (SELECT id FROM demo)
          AND b.code IN ('bateria-seguimiento', 'bateria-cardiovascular')
    ),
    qs AS (
        SELECT qt.id, qt.type, qt.code AS qcode, qt.default_value
        FROM app.health_test_questions qt
        WHERE qt.version_id = (SELECT v2.id FROM app.health_test_versions v2
                               JOIN app.health_test_instruments i2 ON i2.id = v2.instrument_id
                               WHERE i2.code = 'historia-clinica' AND v2.is_current)
          AND qt.is_active AND qt.type IN ('num', 'open')
    )
INSERT INTO app.health_test_responses
    (id, evaluation_id, question_id, value_text, created_at)
SELECT gen_random_uuid(), evs.id, qs.id,
       CASE qs.type
         WHEN 'num' THEN CASE qs.qcode
             WHEN 'peso' THEN '88' WHEN 'talla' THEN '170' WHEN 'cintura' THEN '102'
             WHEN 'cadera' THEN '105' WHEN 'muneca' THEN '17' WHEN 'edad' THEN '45'
             ELSE COALESCE(qs.default_value::text, '0') END
         ELSE (ARRAY[
             'Quería recuperar energía y control de mi glucosa.',
             'Mi familia tiene historia de diabetes y quiero prevenirla.',
             'Me cansé de sentirme sin energía; quiero vivir mejor.',
             'Por mis hijos: quiero estar presente y sano muchos años.',
             'Tras un susto de salud decidí tomar mi vida en serio.']
             )[1 + (d.ord % 5)]
       END,
       evs.started_at
FROM evs
CROSS JOIN qs
JOIN demo d ON d.id = evs.patient_id
WHERE NOT EXISTS (SELECT 1 FROM app.health_test_responses r
                  WHERE r.evaluation_id = evs.id AND r.question_id = qs.id);

-- G6c) Respuestas PARCIALES de orp (evaluación 'started': la mitad par)
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
),
    evs AS (
        SELECT ev.id, ev.patient_id, ev.started_at
        FROM app.health_test_evaluations ev
        JOIN app.health_test_assignments a ON a.id = ev.assignment_id
        JOIN app.health_test_battery_assignments ba ON ba.id = a.battery_assignment_id
        JOIN app.health_test_batteries b ON b.id = ba.battery_id AND b.code = 'bateria-cardiovascular'
        JOIN app.health_test_versions v ON v.id = ev.version_id
        JOIN app.health_test_instruments i ON i.id = v.instrument_id AND i.code = 'orp'
        WHERE ev.status = 'started'
          AND ev.patient_id IN (SELECT id FROM demo)
    ),
    qs AS (
        SELECT qt.id, qt.type, qt.sort_order,
               (SELECT max(o.score_value) FROM app.health_test_answer_options o
                WHERE o.question_id = qt.id AND o.is_active) AS max_score
        FROM app.health_test_questions qt
        WHERE qt.version_id = (SELECT v2.id FROM app.health_test_versions v2
                               JOIN app.health_test_instruments i2 ON i2.id = v2.instrument_id
                               WHERE i2.code = 'orp' AND v2.is_current)
          AND qt.is_active AND qt.type IN ('scale', 'single', 'multi')
    ),
    pick AS (
        SELECT DISTINCT ON (evs.id, qs.id)
               evs.id AS evaluation_id, qs.id AS question_id, o.id AS answer_option_id,
               evs.started_at
        FROM evs
        CROSS JOIN qs
        JOIN demo d ON d.id = evs.patient_id
        JOIN app.health_test_answer_options o ON o.question_id = qs.id AND o.is_active
        WHERE qs.sort_order % 2 = 0
        ORDER BY evs.id, qs.id,
                 abs(o.score_value - ((ARRAY[0.15, 0.35, 0.55, 0.75, 0.9])[1 + ((d.ord + 6) % 5)] * qs.max_score)),
                 o.sort_order
    )
INSERT INTO app.health_test_responses
    (id, evaluation_id, question_id, answer_option_id, created_at)
SELECT gen_random_uuid(), p.evaluation_id, p.question_id, p.answer_option_id, p.started_at
FROM pick p
WHERE NOT EXISTS (SELECT 1 FROM app.health_test_responses r
                  WHERE r.evaluation_id = p.evaluation_id AND r.question_id = p.question_id);

-- G7) Score y score_percentage de las evaluaciones completadas propias
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
),
    myev AS (
        SELECT ev.id
        FROM app.health_test_evaluations ev
        JOIN app.health_test_assignments a ON a.id = ev.assignment_id
        JOIN app.health_test_battery_assignments ba ON ba.id = a.battery_assignment_id
        JOIN app.health_test_batteries b ON b.id = ba.battery_id
        WHERE ev.status = 'completed' AND ev.score IS NULL
          AND ev.patient_id IN (SELECT id FROM demo)
          AND b.code IN ('bateria-seguimiento', 'bateria-cardiovascular')
    ),
    agg AS (
        SELECT r.evaluation_id,
               sum(o.score_value) AS total,
               sum((SELECT max(x.score_value) FROM app.health_test_answer_options x
                    WHERE x.question_id = r.question_id AND x.is_active)) AS max_total
        FROM app.health_test_responses r
        JOIN app.health_test_answer_options o ON o.id = r.answer_option_id
        WHERE r.evaluation_id IN (SELECT id FROM myev)
        GROUP BY r.evaluation_id
    ),
    upd AS (
        UPDATE app.health_test_evaluations ev
        SET score = agg.total,
            score_percentage = round(agg.total * 100.0 / NULLIF(agg.max_total, 0), 2),
            completed_at = ev.started_at + interval '11 minutes'
        FROM agg WHERE agg.evaluation_id = ev.id
    )
INSERT INTO app.health_test_results
    (id, evaluation_id, result_type, code, label, value, qualifier, severity, created_at)
SELECT gen_random_uuid(), agg.evaluation_id, 'score', i.code, 'Score total',
       agg.total, rg.label, rg.severity, ev.completed_at
FROM agg
JOIN app.health_test_evaluations ev ON ev.id = agg.evaluation_id
JOIN app.health_test_versions v ON v.id = ev.version_id
JOIN app.health_test_instruments i ON i.id = v.instrument_id
JOIN LATERAL (
    SELECT rq.label, rq.severity
    FROM app.health_test_score_ranges rq
    WHERE rq.version_id = v.id AND rq.is_active
      AND agg.total >= rq.min_value AND agg.total <= rq.max_value
    ORDER BY rq.min_value DESC LIMIT 1
) rg ON true
WHERE NOT EXISTS (SELECT 1 FROM app.health_test_results x
                  WHERE x.evaluation_id = agg.evaluation_id
                    AND x.result_type = 'score' AND x.code = i.code);

-- G8) Resultados 'subscale' (suma por sección, interpretada con los rangos)
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
),
    myev AS (
        SELECT ev.id, ev.completed_at
        FROM app.health_test_evaluations ev
        JOIN app.health_test_assignments a ON a.id = ev.assignment_id
        JOIN app.health_test_battery_assignments ba ON ba.id = a.battery_assignment_id
        JOIN app.health_test_batteries b ON b.id = ba.battery_id
        WHERE ev.status = 'completed'
          AND ev.patient_id IN (SELECT id FROM demo)
          AND b.code IN ('bateria-seguimiento', 'bateria-cardiovascular')
    ),
    sec AS (
        SELECT r.evaluation_id, q.section, sum(o.score_value) AS sval
        FROM app.health_test_responses r
        JOIN app.health_test_answer_options o ON o.id = r.answer_option_id
        JOIN app.health_test_questions q ON q.id = r.question_id
        WHERE r.evaluation_id IN (SELECT id FROM myev)
          AND q.section IS NOT NULL AND q.section <> ''
        GROUP BY r.evaluation_id, q.section
    )
INSERT INTO app.health_test_results
    (id, evaluation_id, result_type, code, label, value, qualifier, severity, created_at)
SELECT gen_random_uuid(), sec.evaluation_id, 'subscale',
       i.code || '.' || sec.section, sec.section, sec.sval, rg.label, rg.severity,
       myev.completed_at
FROM sec
JOIN myev ON myev.id = sec.evaluation_id
JOIN app.health_test_evaluations ev ON ev.id = sec.evaluation_id
JOIN app.health_test_versions v ON v.id = ev.version_id
JOIN app.health_test_instruments i ON i.id = v.instrument_id
JOIN LATERAL (
    SELECT rq.label, rq.severity
    FROM app.health_test_score_ranges rq
    WHERE rq.version_id = v.id AND rq.is_active
      AND sec.sval >= rq.min_value AND sec.sval <= rq.max_value
    ORDER BY rq.min_value DESC LIMIT 1
) rg ON true
WHERE NOT EXISTS (SELECT 1 FROM app.health_test_results x
                  WHERE x.evaluation_id = sec.evaluation_id
                    AND x.result_type = 'subscale'
                    AND x.code = i.code || '.' || sec.section);

-- G9) Indicadores derivados del IAC-ADRESD: iapnea e iadherencia
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
),
    myev_iac AS (
        SELECT ev.id, ev.completed_at, ev.score_percentage
        FROM app.health_test_evaluations ev
        JOIN app.health_test_versions v ON v.id = ev.version_id
        JOIN app.health_test_instruments i ON i.id = v.instrument_id AND i.code = 'iac-adresd'
        WHERE ev.status = 'completed' AND ev.score IS NOT NULL
          AND ev.patient_id IN (SELECT id FROM demo)
    ),
    rop AS (
        SELECT r.evaluation_id, sum(o.score_value) AS sval
        FROM app.health_test_responses r
        JOIN app.health_test_questions q ON q.id = r.question_id
        JOIN app.health_test_answer_options o ON o.id = r.answer_option_id
        WHERE r.evaluation_id IN (SELECT id FROM myev_iac)
          AND q.section = 'Red flags · Ronquidos'
        GROUP BY r.evaluation_id
    )
INSERT INTO app.health_test_results
    (id, evaluation_id, result_type, code, label, value, qualifier, severity, created_at)
SELECT gen_random_uuid(), m.id, 'indicator', v.code, v.label, v.val, v.qual, v.sev, m.completed_at
FROM myev_iac m
JOIN LATERAL (
    SELECT 'iapnea' AS code, 'Sospecha de apnea' AS label,
           COALESCE(rop.sval, 0) AS val,
           CASE WHEN COALESCE(rop.sval, 0) >= 2 THEN 'alto' ELSE 'bajo' END AS qual,
           CASE WHEN COALESCE(rop.sval, 0) >= 2 THEN 'high' ELSE 'low' END AS sev
    FROM (SELECT r2.evaluation_id, sum(o2.score_value) AS sval
          FROM app.health_test_responses r2
          JOIN app.health_test_questions q2 ON q2.id = r2.question_id
          JOIN app.health_test_answer_options o2 ON o2.id = r2.answer_option_id
          WHERE r2.evaluation_id = m.id AND q2.section = 'Red flags · Ronquidos'
          GROUP BY r2.evaluation_id) rop
) v ON true
WHERE NOT EXISTS (SELECT 1 FROM app.health_test_results x
                  WHERE x.evaluation_id = m.id
                    AND x.result_type = 'indicator' AND x.code = v.code);

WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
),
    myev_iac AS (
        SELECT ev.id, ev.completed_at, ev.score_percentage
        FROM app.health_test_evaluations ev
        JOIN app.health_test_versions v ON v.id = ev.version_id
        JOIN app.health_test_instruments i ON i.id = v.instrument_id AND i.code = 'iac-adresd'
        WHERE ev.status = 'completed' AND ev.score IS NOT NULL
          AND ev.patient_id IN (SELECT id FROM demo)
    )
INSERT INTO app.health_test_results
    (id, evaluation_id, result_type, code, label, value, qualifier, severity, created_at)
SELECT gen_random_uuid(), m.id, 'indicator', 'iadherencia', 'Índice de adherencia',
       m.score_percentage,
       CASE WHEN m.score_percentage < 40 THEN 'bajo' ELSE 'alto' END,
       CASE WHEN m.score_percentage < 40 THEN 'high' ELSE 'low' END, m.completed_at
FROM myev_iac m
WHERE NOT EXISTS (SELECT 1 FROM app.health_test_results x
                  WHERE x.evaluation_id = m.id
                    AND x.result_type = 'indicator' AND x.code = 'iadherencia');

-- G10) La batería de seguimiento queda 'completed' cuando todos sus
--     instrumentos están completados
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
)
UPDATE app.health_test_battery_assignments ba
SET status = 'completed', completed_at = ba.assigned_at + interval '10 days'
FROM app.health_test_batteries b
WHERE ba.battery_id = b.id AND b.code = 'bateria-seguimiento'
  AND ba.status <> 'completed'
  AND ba.patient_id IN (SELECT id FROM demo)
  AND NOT EXISTS (SELECT 1 FROM app.health_test_assignments a
                  WHERE a.battery_assignment_id = ba.id AND a.status <> 'completed');

-- ====================================================================
-- H) RUTINAS DE EJERCICIO ASIGNADAS: 2 activas por paciente
--    (rotación determinista sobre el catálogo existente).
-- ====================================================================
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
)
INSERT INTO app.routine_assignments
    (id, patient_id, routine_id, start_date, end_date, frequency, status, notes, created_by, created_at, updated_at)
SELECT gen_random_uuid(), d.id, r.id,
       current_date - (60 + (d.ord % 10)),
       current_date + 90,
       3, 1,
       CASE v.sel % 3
         WHEN 0 THEN 'Empezar con series cortas y aumentar progresivamente.'
         WHEN 1 THEN 'Priorizar técnica sobre carga; registrar el esfuerzo en la app.'
         ELSE 'Combinar con una caminata de 10 minutos de calentamiento.' END,
       '7e1c4d20-9a41-4f0e-9b5a-5f3a2c1d0e9f', now(), now()
FROM demo d
JOIN (VALUES
    ('Cardio Básico', 0), ('Pierna', 1), ('Cardio HIIT', 2), ('Espalda', 3), ('Full Body', 4), ('Core y Abdominales', 5)
) AS v(name, sel) ON true
JOIN app.exercise_routines r ON r.name = v.name
WHERE (v.sel = d.ord % 6 OR v.sel = (d.ord + 2) % 6)
  AND NOT EXISTS (SELECT 1 FROM app.routine_assignments x
                  WHERE x.patient_id = d.id AND x.routine_id = r.id);

-- ====================================================================
-- I) NUTRICIÓN: habit_checks + nutrition_intake_logs de los últimos
--    21 días (5 comidas/día: des, alm, mer, cen, agua). Macros
--    coherentes con el objetivo calórico por paciente. ON CONFLICT
--    sobre las claves únicas reales; no pisa registros existentes.
-- ====================================================================
-- I1) habit_checks (idempotente por paciente+plantilla+fecha)
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
),
    days AS (SELECT generate_series(1, 21) AS k)
INSERT INTO app.habit_checks
    (id, patient_id, habit_template_id, local_date, is_done, created_at)
SELECT gen_random_uuid(), d.id, ht.id, current_date - g.k, true, now()
FROM demo d
CROSS JOIN days g
JOIN app.habit_templates ht ON ht.code IN ('des', 'alm', 'mer', 'cen', 'agua')
ON CONFLICT (patient_id, habit_template_id, local_date) DO NOTHING;

-- I2) Ingestas con macros por comida (25/35/10/30% del objetivo)
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
),
    days AS (SELECT generate_series(1, 21) AS k)
INSERT INTO app.nutrition_intake_logs
    (id, patient_id, habit_check_id, local_date, meal_code, calories, protein_g,
     carbs_g, fat_g, fiber_g, water_ml, source, nutrition_plan_id, created_at)
SELECT gen_random_uuid(), d.id, hc.id, hc.local_date, ht.code,
       CASE WHEN ht.code = 'agua' THEN NULL
            ELSE ROUND(kcal.target * sh.s * jit.j, 0) END,
       CASE WHEN ht.code = 'agua' THEN NULL
            ELSE ROUND(kcal.target * sh.s * jit.j * 0.25 / 4.0, 1) END,
       CASE WHEN ht.code = 'agua' THEN NULL
            ELSE ROUND(kcal.target * sh.s * jit.j * 0.45 / 4.0, 1) END,
       CASE WHEN ht.code = 'agua' THEN NULL
            ELSE ROUND(kcal.target * sh.s * jit.j * 0.30 / 9.0, 1) END,
       CASE ht.code WHEN 'des' THEN 6.0 WHEN 'alm' THEN 10.0 WHEN 'mer' THEN 3.0
                    WHEN 'cen' THEN 9.0 ELSE NULL END,
       CASE WHEN ht.code = 'agua' THEN 1500 + (d.ord % 5) * 250 ELSE NULL END,
       'rich_seed',
       (SELECT npa.plan_id FROM app.nutrition_plan_assignments npa
        WHERE npa.patient_id = d.id AND npa.status = 1
        ORDER BY npa.start_date DESC LIMIT 1),
       now()
FROM demo d
CROSS JOIN days g
JOIN app.habit_templates ht ON ht.code IN ('des', 'alm', 'mer', 'cen', 'agua')
JOIN app.habit_checks hc ON hc.patient_id = d.id AND hc.habit_template_id = ht.id
    AND hc.local_date = current_date - g.k
CROSS JOIN LATERAL (SELECT (ARRAY[1400, 1600, 1800, 1900])[1 + d.ord % 4] AS target) kcal
CROSS JOIN LATERAL (SELECT CASE ht.code WHEN 'des' THEN 0.25 WHEN 'alm' THEN 0.35
                            WHEN 'mer' THEN 0.10 WHEN 'cen' THEN 0.30 ELSE 0 END AS s) sh
CROSS JOIN LATERAL (SELECT 0.9 + ((d.ord * 7 + g.k) % 7)::numeric / 30.0 AS j) jit
ON CONFLICT (patient_id, local_date, meal_code) DO NOTHING;

-- ====================================================================
-- J) TELEMEDICINA: 5 citas pasadas por paciente (3 Completed,
--    1 Cancelled, 1 NoShow) y 2 futuras Confirmed, ligando el paciente
--    con su profesional existente. Respeta el índice único parcial de
--    anti doble reserva (Requested/Confirmed/InProgress) con guardas
--    NOT EXISTS (professional, scheduled_start).
-- ====================================================================
-- J1) Citas pasadas (ventana 3 meses)
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
),
    prof_for AS (
    SELECT DISTINCT ON (pp.patient_id) pp.patient_id, pp.professional_id
    FROM app.patient_professionals pp
    WHERE pp.patient_id IN (SELECT id FROM demo)
    ORDER BY pp.patient_id, pp.created_at
),
    slots AS (
        SELECT d.id AS patient_id, d.ord, v.k, v.status,
               (((current_date - (8 + v.k * 17 + (d.ord % 9)))::text || ' ' || ('13:' || lpad((((d.ord + v.k) % 2) * 30)::text, 2, '0')) || ':00')::timestamp AT TIME ZONE 'America/Bogota') AS ts
        FROM demo d
        JOIN (VALUES (0, 'Completed'), (1, 'Completed'), (2, 'Completed'),
             (3, 'Cancelled'), (4, 'NoShow')) AS v(k, status) ON true
    )
INSERT INTO tele.appointments
    (id, patient_id, professional_id, specialty_id, organization_id, clinic_id,
     scheduled_start, scheduled_end, duration_minutes, status, reschedule_count,
     cancellation_reason, cancelled_by, cancelled_at, no_show_reason,
     created_by, created_at, updated_at, completed_at)
SELECT gen_random_uuid(), s.patient_id, pf.professional_id,
       (ARRAY['5c6afbf8-d00f-451c-8d71-16983bea0b87', '97f1799b-250f-4297-a4d6-2fc882acb325']::uuid[])[1 + s.ord % 2],
       '5fde219a-89ea-4cf9-be48-379e8b1042cb', '360a13fe-8adc-4a00-94df-04d2fa703b7c',
       s.ts, s.ts + interval '30 minutes', 30, s.status, 0,
       CASE s.status WHEN 'Cancelled' THEN 'Solicitud del paciente' END,
       CASE s.status WHEN 'Cancelled'
            THEN (ARRAY['Patient', 'Professional'])[1 + s.ord % 2] END,
       CASE s.status WHEN 'Cancelled' THEN s.ts - interval '2 days' END,
       CASE s.status WHEN 'NoShow' THEN 'El paciente no se conectó a la sala virtual' END,
       '7e1c4d20-9a41-4f0e-9b5a-5f3a2c1d0e9f', s.ts - interval '7 days', now(),
       CASE s.status WHEN 'Completed' THEN s.ts + interval '38 minutes' END
FROM slots s
JOIN prof_for pf ON pf.patient_id = s.patient_id
WHERE (SELECT count(*) FROM tele.appointments x
       WHERE x.patient_id = s.patient_id AND x.created_by = '7e1c4d20-9a41-4f0e-9b5a-5f3a2c1d0e9f'
         AND x.scheduled_start < now()) < 5
  AND NOT EXISTS (SELECT 1 FROM tele.appointments y
                  WHERE y.patient_id = s.patient_id
                    AND y.professional_id = pf.professional_id
                    AND y.scheduled_start = s.ts);

-- J2) Citas futuras Confirmed (2 por paciente; día distinto por slot;
--     hora/minuto deterministas que garantizan separación >= 30 min
--     entre slots del mismo profesional; guarda anti-solapamiento por
--     rango contra citas activas existentes (exclusión GiST).
WITH
    demo AS (
    SELECT pp.id,
           pp.document_number AS doc,
           pp.clinic_id,
           ((row_number() OVER (ORDER BY pp.document_number) - 1))::int AS ord
    FROM app.patient_profiles pp
    JOIN auth."Users" u ON u."Id" = pp.user_id
    WHERE u."NormalizedEmail" IN (
           'LUIS.PRUEBA@COPPADDRESD.COM',
           'PLAYWRIGHT.E2E@COPPADDRESD.COM',
           'TEST.E2E@COPPADDRESD.COM',
           'JUAN.PEREZ@COPPADDRESD.COM',
           'MARIA.GOMEZ@COPPADDRESD.COM',
           'CARLOS.RODRIGUEZ@COPPADDRESD.COM',
           'ANA.MARTINEZ@COPPADDRESD.COM',
           'LUIS.FERNANDEZ@COPPADDRESD.COM',
           'LAURA.SANCHEZ@COPPADDRESD.COM',
           'PACIENTE.PRUEBA@MEDIQUER.COM',
           'PACIENTE.TEST.T23@COPPADDRESD.COM',
           'PACIENTE.1@MEDIQUER.COM',
           'PACIENTE.2@MEDIQUER.COM',
           'PACIENTE.3@MEDIQUER.COM',
           'PACIENTE.4@MEDIQUER.COM',
           'PACIENTE.5@MEDIQUER.COM',
           'PACIENTE.6@MEDIQUER.COM',
           'PACIENTE.7@MEDIQUER.COM',
           'PACIENTE.8@MEDIQUER.COM',
           'PACIENTE.9@MEDIQUER.COM',
           'PACIENTE.10@MEDIQUER.COM',
           'PACIENTE.11@MEDIQUER.COM',
           'PACIENTE.12@MEDIQUER.COM',
           'PACIENTE.13@MEDIQUER.COM',
           'PACIENTE.14@MEDIQUER.COM',
           'PACIENTE.15@MEDIQUER.COM',
           'PACIENTE.16@MEDIQUER.COM',
           'PACIENTE.17@MEDIQUER.COM',
           'PACIENTE.18@MEDIQUER.COM',
           'PACIENTE.19@MEDIQUER.COM',
           'PACIENTE.20@MEDIQUER.COM',
           'PACIENTE.21@MEDIQUER.COM',
           'PACIENTE.22@MEDIQUER.COM',
           'PACIENTE.23@MEDIQUER.COM',
           'PACIENTE.24@MEDIQUER.COM'
    )
),
    prof_for AS (
    SELECT DISTINCT ON (pp.patient_id) pp.patient_id, pp.professional_id
    FROM app.patient_professionals pp
    WHERE pp.patient_id IN (SELECT id FROM demo)
    ORDER BY pp.patient_id, pp.created_at
),
    slots AS (
        SELECT d.id AS patient_id, d.ord, i.i,
               (((current_date + 7 + ((d.ord * 2 + i.i) % 30))::text || ' ' || ('13:00') || ':00')::timestamp AT TIME ZONE 'America/Bogota')
               + ((((d.ord + i.i) % 9)::text || ' hours'))::interval
               + ((((d.ord % 2) * 30)::text || ' minutes'))::interval AS ts
        FROM demo d CROSS JOIN (VALUES (0), (1)) AS i(i)
    )
INSERT INTO tele.appointments
    (id, patient_id, professional_id, specialty_id, organization_id, clinic_id,
     scheduled_start, scheduled_end, duration_minutes, status, reschedule_count,
     created_by, created_at, updated_at)
SELECT gen_random_uuid(), s.patient_id, pf.professional_id,
       (ARRAY['5c6afbf8-d00f-451c-8d71-16983bea0b87', '97f1799b-250f-4297-a4d6-2fc882acb325']::uuid[])[1 + s.ord % 2],
       '5fde219a-89ea-4cf9-be48-379e8b1042cb', '360a13fe-8adc-4a00-94df-04d2fa703b7c',
       s.ts, s.ts + interval '30 minutes', 30, 'Confirmed', 0,
       '7e1c4d20-9a41-4f0e-9b5a-5f3a2c1d0e9f', now(), now()
FROM slots s
JOIN prof_for pf ON pf.patient_id = s.patient_id
WHERE (SELECT count(*) FROM tele.appointments x
       WHERE x.patient_id = s.patient_id AND x.created_by = '7e1c4d20-9a41-4f0e-9b5a-5f3a2c1d0e9f'
         AND x.scheduled_start > now()) < 2
  AND NOT EXISTS (SELECT 1 FROM tele.appointments y
                  WHERE y.professional_id = pf.professional_id
                    AND y.status IN ('Requested', 'Confirmed', 'InProgress')
                    AND tstzrange(y.scheduled_start, y.scheduled_end)
                        && tstzrange(s.ts, s.ts + interval '30 minutes'))
  AND NOT EXISTS (SELECT 1 FROM tele.appointments y
                  WHERE y.patient_id = s.patient_id AND y.scheduled_start = s.ts);

-- ====================================================================
-- K) DEVICE TOKENS DEMO para push FCM: 8 usuarios prioritarios ×
--    (ios, android) = 8 tokens por plataforma con prefijo fcm-seed-*.
--    Idempotente por la clave única (user_id, token).
-- ====================================================================
WITH fcm_users AS (
    SELECT u."Id" AS uid, v.prio
    FROM auth."Users" u
    JOIN (VALUES
        ('LUIS.PRUEBA@COPPADDRESD.COM', 0),
        ('PLAYWRIGHT.E2E@COPPADDRESD.COM', 1),
        ('TEST.E2E@COPPADDRESD.COM', 2),
        ('JUAN.PEREZ@COPPADDRESD.COM', 3),
        ('MARIA.GOMEZ@COPPADDRESD.COM', 4),
        ('CARLOS.RODRIGUEZ@COPPADDRESD.COM', 5),
        ('ANA.MARTINEZ@COPPADDRESD.COM', 6),
        ('PACIENTE.1@MEDIQUER.COM', 7)
    ) AS v(email, prio) ON u."NormalizedEmail" = v.email
)
INSERT INTO app.device_tokens
    (id, user_id, token, platform, created_at)
SELECT gen_random_uuid(), f.uid, 'fcm-seed-' || p.plat || '-' || (f.prio + 1),
       p.plat, now()
FROM fcm_users f
CROSS JOIN (VALUES ('ios'), ('android')) AS p(plat)
WHERE f.prio < 8
ON CONFLICT (user_id, token) DO NOTHING;

-- ====================================================================
-- L) VERIFICACIÓN (se ejecuta como parte del archivo; los SELECT solo
--    muestran, no modifican)
-- ====================================================================
-- Medios demo por categoría: media/podcasts/demo-copp.mp3
SELECT category, count(*), round(avg(duration_secs) / 60.0, 1) AS minutos_prom
FROM app.media_items
WHERE storage_key = 'media/podcasts/demo-copp.mp3'
GROUP BY category ORDER BY count(*) DESC;

-- Rotación por weekday en las plantillas activas
SELECT pt.code, t.weekday, m.title
FROM app.weekly_day_templates t
JOIN app.program_templates pt ON pt.id = t.template_id
JOIN app.media_items m ON m.id = t.media_id
WHERE t.task_code = 'podcast'
ORDER BY pt.code, t.weekday LIMIT 14;

COMMIT;
