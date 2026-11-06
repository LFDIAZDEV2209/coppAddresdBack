# Idioma de contenido para la app

La interfaz usa catálogos ES/EN locales. El contenido publicado desde el ERP
se localiza en una proyección de lectura de la API; no se modifica la fuente
clínica, su revisión, aprobación, valores o auditoría.

## Negociación y campos

Las lecturas anotadas con `LocalizePatientContent` admiten `language=en|es` y,
si no existe ese parámetro, `X-App-Language`, con rangos regionales y calidad.
La proyección exige JWT con `aud=app` y esa elección explícita. Un encabezado
`Accept-Language` automático del navegador/SO no habilita traducción en clientes
anteriores. El ERP recibe siempre la fuente canónica para no editar una etiqueta
traducida como si fuera el nombre original. Las respuestas declaran
`Content-Language` y varían por `X-App-Language`, `Accept-Language` y `Authorization`.

`PatientContentLocalization` clona el JSON resultante después de autorización
y proyección. Cada perfil enumera rutas explícitas de presentación:

| Lectura | Contenido localizable |
| --- | --- |
| `/me/nutrition-plan` | Nombre, condición, alérgenos, horario general, alimentos, descripción y notas de comidas. |
| `/program/me/snapshot` | Nombre del programa, contenido del podcast, capítulos, ideas clave, planes y ejercicios. |
| `/program/notifications` y alias `/program/me/notifications` | Título y mensaje del log entregado; no reescribe el registro original. |
| `/health-tests/me/assignments`, `/batteries`, `/tests/{id}`, `/results`, `/history` | Nombres, categorías, instrucciones, preguntas, opciones y etiquetas de resultados. |
| `/me/measurements` | Nombre de la métrica; conserva código, unidad, valor, origen y cursor. |
| `/specialties` | Nombre, categoría y descripción del catálogo. |
| `/professionals-catalog` | `professionalTypeDisplayName` y nombres de especialidad. La profesión fuente y los nombres de personas/sedes permanecen intactos. |

Los IDs, códigos, enums, fechas, cifras, URLs, unidades y respuestas libres del
paciente no forman parte de esa lista. Las mutaciones de evaluaciones no se
traducen después de guardar: la app consulta la proyección de resultados, para
que una indisponibilidad del traductor no informe falsamente que falló un envío.

`PatientNutritionInstructions` retira únicamente seis líneas exactas del
boilerplate interno generado (por ejemplo `Propuesta determinista pendiente de
revisión.`) de la proyección pública de notas. Esa narrativa podía persistir
después de aprobar y confundir el estado que ve el paciente. Se conservan todas
las indicaciones clínicas restantes, notas específicas, cifras y texto fuente
almacenado; no se modifica la aprobación ni se añade una indicación inventada.
Se aplica tanto al plan publicado como a la misión del protocolo y su ruta legacy.

Una nota inglesa ya aprobada (`notesEn`) prevalece sobre traducción automática
solo si corresponde a la indicación general que originó `notes` y el hash
persistido verifica la fuente exacta. Las parejas históricas sin esa procedencia
se traducen desde la fuente vigente; no se reutiliza su inglés no verificado. Una edición
española de esa fuente sin sustitución inglesa invalida la traducción anterior;
véase [nutrición](../nutrition/instructions-localization.md).

## Adaptador interno y errores

`ContentTranslationService` llama a `AiService:BaseUrl` en
`POST /internal/content/translate`, con la clave interna existente. Envía solo
los campos seleccionados, deduplicados, en lotes de hasta 64 textos / 32.000
caracteres (8.000 por texto). Cada llamada tiene un límite de 30 segundos y
respeta la cancelación del cliente. Valida idioma, IDs, completitud y cifras.

El servicio AI protege además unidades, cantidades, códigos, placeholders y
marcado, comprueba el contrato y mantiene una cache acotada por hash de fuente.
Usa sublotes de hasta ocho elementos y un semáforo compartido de cuatro llamadas,
con un plazo total de 28 segundos que incluye la cola. La cache se actualiza
solo si el lote completo supera la validación; un fallo cancela sus llamadas
hermanas. Dos equivalencias terminológicas revisadas de carbohidrato complejo
se restauran mediante los mismos tokens protegidos, sin cambiar cifras o marcado.
No se registran fuentes, salidas o claves. La traducción de presentación no es
una nueva aprobación clínica ni una prueba formal de equivalencia semántica.

Si falta una traducción o el proveedor no responde, la API devuelve **503**,
`title: CONTENT_TRANSLATION_UNAVAILABLE` y `Retry-After: 5`. No devuelve un éxito
parcial con texto español presentado como inglés. La app muestra el error
localizado y permite reintentar. Las lecturas inglesas de estos endpoints tienen
un presupuesto de cliente de 45 segundos; las demás conservan su presupuesto.

Food AI conserva `NameEn` de análisis nuevos. Los resultados históricos usan la
fuente inglesa persistida, identidad exacta del catálogo curado y finalmente el
traductor, sin sustituir alimentos o nutrientes. Véase
[Food AI](../food-ai/README.md).

## Entrega y verificación

Publicar primero el endpoint del servicio AI, después API/migraciones y por
último la app. El selector de idioma no convierte un servidor anterior en
bilingüe. Las credenciales/proveedores existentes se mantienen; no se agregan
claves al repositorio. Ningún paso de esta auditoría publica automáticamente.

Las regresiones incluyen contrato HTTP del adaptador, cancelación, lotes y
respuestas inválidas; invariantes de todos los perfiles; negociación de idioma;
y un servidor HTTP de prueba con el controlador real de mediciones, filtro MVC,
autorización y middleware. Se comprueban EN/ES, 401 sin consulta downstream y
503 recuperable, conservando IDs, decimales, unidad, fecha y cursor.
