# Rutinas de ejercicio y demostraciones

Cada `RoutineExercise` conserva su `MediaId` opcional. El ERP permite buscar
vídeos o imágenes publicados, ver la demostración, quitarla o subir una nueva.
Editar una rutina conserva tanto el medio de cada ejercicio como la portada
de la rutina; no se reemplazan por `null` al guardar los campos de texto.

## Biblioteca y publicación

Se reutilizan `POST /api/v1/media/upload-intent`, la subida firmada, el catálogo
`/api/v1/media` y su transición `/publish`. Las imágenes usan el propósito de
subida `thumbnail`, que ya autoriza `image/*`; el `StorageKey` resultante se
registra como contenido de un `MediaItem` de tipo `Image`. No se exige ni se
inventa duración para una imagen. Los vídeos mantienen la duración real del
archivo y las verificaciones de publicación existentes.

- `Image`: JPEG, PNG, WebP y GIF, hasta 20 MB. SVG/HTML no se admiten como
  imágenes, incluso en creación y edición del catálogo.
- `Video`: MP4, WebM y QuickTime, hasta 1 GB, duración positiva.
- Buscar/consultar medios requiere `Media.View`; subir requiere `Media.Create`
  y publicar requiere `Media.Publish`. La autorización de modificar rutinas
  sigue siendo `Wellness.Manage`. La UI muestra las acciones correspondientes
  a los permisos globales o de la clínica activa.
- Se crea primero un borrador y se publica antes de adjuntarlo al ejercicio.
  Un fallo de subida o publicación no modifica la referencia anterior.

`MediaType.Image` se almacena en la columna de texto existente. `MediaId`
también existía: esta funcionalidad no requiere una migración de esquema.

## Contenido del paciente

El snapshot propio `/api/v1/program/me/snapshot` resuelve la inscripción desde
el JWT y únicamente proyecta medios `Published` de tipo `Video` o `Image` de
los ejercicios de la rutina asignada. No se abre el catálogo administrativo
al paciente ni se agrega una consulta arbitraria por ID de medio.

`ExerciseItemDto` incluye de forma aditiva `mediaId`, `mediaType`, `mediaUrl`,
`thumbnailUrl`, `weightKg`, `tempo` y `rpe`. Las claves de storage se convierten
en URL temporal de una hora en el mismo resolutor que usan los podcasts.
Con S3 se obtiene una URL presignada; Local usa el proxy firmado existente.
El estado sin contenido sigue siendo explícito, sin imágenes ni vídeos demo.

Las referencias del catálogo cuentan también `routine_exercises.media_id`
y `exercise_routines.media_id`, con consultas agrupadas y proyecciones
server-side. El guardia de eliminación devuelve 409 si una demostración está
en uso. Las relaciones existentes ya indexan esos FKs; no hay lecturas N+1.

## App y validación

Seleccionar un ejercicio abre su vista propia con el medio real, instrucciones,
series/repeticiones, descanso, peso, tempo, esfuerzo y consejos cuando existen.
Consultar la técnica pausa un circuito que esté corriendo. Abrir o reproducir
un medio no registra completación ni concede XP; se conservan las acciones
del circuito y su persistencia existente.

Pruebas relevantes:

```sh
dotnet test tests/CoppAddresd.UnitTests -p:AllowMissingPrunePackageData=true --filter 'FullyQualifiedName~Media.ExerciseMediaTests|FullyQualifiedName~Media.MediaLifecycleTests|FullyQualifiedName~Media.MediaReferenceGuardTests'
```

`MediaIntegrationTests.Delete_DemostracionReferenciadaPorEjercicio_ProtegeLaRutinaYCuentaUso`
comprueba las consultas y la protección de borrado con PostgreSQL aislado.
Requiere `COP_TEST_DB_CONNECTION`; no debe ejecutarse contra producción.
Las pruebas de la app y del ERP usan medios/destinos sintéticos y peticiones
interceptadas. No suben ni publican archivos reales durante la validación.

## Demostraciones por ejercicio: wger, archivos y YouTube

La interfaz existente de rutinas conserva su prescripción (series, repeticiones,
pausas, tempo, RPE, equipo y técnica). Cada ejercicio elige **un archivo publicado
`mediaId` o `youTubeUrl`**, nunca ambos. El backend valida y normaliza los enlaces;
no admite dominios con sufijos, credenciales, puertos ni iframes arbitrarios.
La ausencia de campos nuevos conserva las referencias curadas históricas.

### Almacenamiento y límites

- `POST /api/v1/wellness/exercise-media/uploads` recibe multipart `file` + `title`.
  Entrada: vídeo de hasta **20 MiB y 180 segundos**; imagen de hasta **5 MiB**.
  No utiliza un PUT presignado sin límite de bytes para este flujo.
- Se comprueba la firma binaria, se inspecciona el archivo con FFprobe y se limita
  la resolución de entrada a 4096 píxeles por lado. FFmpeg solo admite protocolos
  `file,pipe`, sin ejecutar shells ni descargar referencias del archivo.
- Vídeo final: **MP4 H.264**, sin audio, `yuv420p`, `faststart`, ancho máximo 960 px,
  bitrate limitado. Imagen final: PNG del primer fotograma. Se genera poster JPG.
  **Antes de S3** se exige que el MP4 final no supere 20 MiB, la imagen 5 MiB
  y el poster 1 MiB. La conversión no recorta vídeos largos para hacerlos pasar.
- Los objetos usan `media/exercises/{id}/demo.mp4` y `poster.jpg`, a través de
  `IObjectStorageService`. Fuera de Development/Testing se exige proveedor **S3**;
  un servidor sin S3 configurado rechaza la preparación.
- Por usuario ERP y día UTC: **10 archivos / 100 MiB**, configurables mediante
  `ExerciseMedia:DailyUploadLimit` y `ExerciseMedia:DailyStorageMegabytes`.
  El ledger `app.exercise_media_usage` incluye vídeo + poster. Su reserva se
  confirma antes del primer PUT, bajo advisory lock transaccional por usuario.
  Un fallo posterior puede consumir cuota para impedir reintentos ilimitados.
  Borrar el medio no elimina el ledger ni repone cuota. Reutilizar una referencia
  publicada no genera otra copia. Estos límites pertenecen al flujo de ejercicios,
  no sustituyen presupuestos/facturación de AWS ni cuotas de otros módulos.
- El endpoint limita tres solicitudes por minuto por usuario/proceso; la cuota
  diaria y la cola son compartidas/durables en PostgreSQL entre réplicas.
- Requiere FFmpeg/FFprobe; el Dockerfile del API instala el paquete del sistema.

### Catálogo e importación de wger

`GET /api/v1/wellness/exercise-media/wger?search=...&page=1` consulta páginas acotadas
del API público de wger, con caché de diez minutos. Solo ofrece licencias reconocidas
(CC BY-SA 3/4, CC BY 4 y CC0); no inventa autor para recursos que requieren atribución.
Las traducciones españolas se prefieren cuando existen; también se puede buscar
el nombre inglés. El profesional elige la variante exacta; no se asignan vídeos por
similitud de músculo o por generación de IA.

`POST /.../wger/import` devuelve **202** con `jobId`. `GET /.../wger/imports/{jobId}`
permite al creador consultar queued/processing/completed/failed y el medio final.
El navegador consulta operaciones breves; una descarga lenta no mantiene abierto
el POST del editor ni depende del idle timeout de un balanceador.

`app.exercise_media_import_jobs` guarda los trabajos; el worker usa claim atómico
`FOR UPDATE SKIP LOCKED`, lease de seis minutos y máximo tres intentos de recuperación.
Cada importación limita su ejecución a cinco minutos y su descarga original a 128 MiB.
Solo descarga rutas oficiales de `https://wger.de/media/exercise-*`, sin redirecciones
ni URLs de descarga elegidas por el cliente. Al importar reconsulta la licencia
actual y conserva su metadata original completa. El medio utiliza el ID del trabajo:
si el proceso cae tras publicar, la recuperación reutiliza ese resultado/clave.
Un trabajo fallido muestra un error accionable. Salir del editor cancela la consulta
local, pero el trabajo continúa y el recurso terminado aparece en Mis archivos.

Se permiten dos importaciones pendientes por usuario y diez solicitudes nuevas por
día UTC. Repetir una pendiente devuelve el mismo trabajo; repetir una completada
propia y publicada reutiliza el medio. La cuota de almacenamiento también se aplica.

`MediaItem.Provenance` es jsonb: proveedor, ID, título, autores/historial, URL fuente,
licencia, URL de licencia, cambios, obra derivada si existe, URL del archivo original,
metadata original y fecha de consulta. Se muestran créditos en ERP y app, incluso
cuando falla el reproductor. Reemplazar el archivo de un medio importado está
bloqueado para evitar conservar una licencia sobre un archivo distinto.
La licencia declara permisos del proveedor; no constituye una garantía de titularidad
ni de cesiones de imagen. Las adaptaciones de esos recursos conservan su licencia.

### YouTube

Solo se guarda el enlace canónico/ID. No hay descarga, copia a S3, reproducción
offline ni extracción de audio. Web: iframe oficial sin autoplay, controles y
anuncios intactos, referrer policy `strict-origin-when-cross-origin`, tamaño mínimo
200 px de alto y enlace al origen siempre disponible. El proveedor controla vídeos
privados, eliminados o con inserción bloqueada.

En Capacitor se muestra el poster y se abre el reproductor en navegador nativo.
iOS SFSafariViewController usa `origin=https://<bundle-id>` obtenido en runtime
con `App.getInfo()` (Xcode: `com.coppadresd.mobile`), conforme al
mecanismo de identificación documentado por YouTube. Android abre la página completa
de YouTube para evitar un iframe local sin Referer. No se envían datos clínicos al
proveedor. La app muestra enlaces a condiciones de YouTube y privacidad de Google.
La reproducción nativa requiere validación en dispositivos iOS/Android reales.

### Permisos

- Catálogo: `Wellness.View` + `Media.View`.
- Subir/importar/publicar: `Wellness.Manage` + `Media.Create` + `Media.Publish`.
- Consultar trabajo propio: `Wellness.Manage` + `Media.Create`; otro usuario recibe 404.
- Guardar un enlace YouTube en una rutina: `Wellness.Manage`, con la validación del
  comando de rutinas existente. No concede permisos de biblioteca automáticamente.

### Validación

Los scripts Playwright `e2e/exercise-media-sources.mjs`,
`e2e/exercise-media-real-api.mjs` y `e2e/exercise-media-limits-real-api.mjs` viven en
el ERP. El primero intercepta APIs; los otros dos exigen API local y PostgreSQL
`copp_review_exercise_media_*`, con Auth de prueba. Verifican navegación, guardado,
lectura persistida, wger real, permisos, firma, duración, límite de bytes y dos
subidas concurrentes al agotar cuota. Los archivos de prueba son vídeo sintético
propio y evidencias bajo `/tmp`; no usar pacientes o credenciales de producción.
La prueba Ionic `e2e/exercise-media-sources.mjs` verifica sus pantallas con contratos
sintéticos, créditos, fallo/reintento, indicaciones y anchos 320/390/768.

Fuentes oficiales consultadas: [API wger](https://wger.readthedocs.io/en/latest/api/api.html),
[CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/),
[políticas YouTube](https://developers.google.com/youtube/terms/developer-policies),
[identificación móvil y tamaño del player](https://developers.google.com/youtube/terms/required-minimum-functionality).
