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
