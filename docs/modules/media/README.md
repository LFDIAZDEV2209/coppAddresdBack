# Biblioteca de medios — origen y etiquetas

## Contrato 1.8

Los campos y permisos anteriores se conservan. `source_kind` es `file` (valor
predeterminado de los medios existentes) o `external`. `tags` es un array JSON de
hasta 32 etiquetas (1–64 caracteres), normalizadas a minúsculas, sin duplicados.

Un video externo usa los metadatos habituales y este bloque de origen. Ejemplo:

```json
{
  "title": "Movilidad suave",
  "author": "Equipo clínico",
  "mediaType": "Video",
  "category": "Biologia",
  "status": "Draft",
  "source_kind": "external",
  "provider": "youtube",
  "external_url": "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
  "tags": ["movilidad", "suave"],
  "thumbnailUrl": "https://images.example.test/cover.jpg",
  "durationSeconds": 45
}
```

POST `/api/v1/media` y PUT `/api/v1/media/{id}` aceptan el bloque. `storageKey`,
MIME y tamaño no se requieren para externos. `thumbnailUrl` y `durationSeconds`
son opcionales; los campos anteriores `thumbnailKey`/`durationSecs` siguen siendo
compatibles. Si se informa duración, debe ser positiva. La portada externa es HTTPS.
Archivos siguen requiriendo `storageKey`; no admiten proveedor/enlace externos.

Se admiten enlaces HTTPS de video de YouTube (`watch?v=`, `youtu.be`, `embed`,
`shorts`, incluido `www.youtube-nocookie.com/embed`) y Vimeo (`vimeo.com/{id}`,
`player.vimeo.com/video/{id}`). Proveedor y URL deben coincidir. Se rechazan
hosts parecidos, credenciales en URL, puertos ajenos, playlists, IDs inválidos y
esquemas distintos de HTTPS. No se solicita contenido al proveedor desde el backend.
La validación comprueba formato/proveedor; no comprueba disponibilidad del video.

El ERP reproduce el externo mediante el reproductor del proveedor usando
`provider` + `external_url`. El `storageKey` devuelto es un identificador interno
estable, no un archivo para descargar o firmar. Crear el mismo video con URL corta
o larga conserva el ID mediante la idempotencia existente por clave.

GET `/api/v1/media?source_kind=external&tags=movilidad&tags=suave` filtra por origen
y por **todas** las etiquetas, antes del conteo y la paginación. Se combinan los
filtros previos de búsqueda, tipo, categoría, estado y uso. El envelope no cambia.
Listado y detalle añaden `tags`, `source_kind`, `external_url`, `provider`,
`thumbnailUrl` y `durationSeconds`. El proveedor se deriva de la URL validada.
`source_kind` inválido o etiquetas inválidas: 422 en listado; input inválido de
creación/edición: 400 mediante el pipeline de validación (guardia interna 422).

Publicar un externo valida su enlace sin buscar un blob. La respuesta informa
`externalUrlVerified: true`, `storageKeyVerified: false` y `thumbnailVerified: false`.
No se eliminan blobs ficticios ni URLs de portada al editar/eliminar externos.
Los permisos siguen siendo Media.View/Create/Edit/Publish/Delete según operación.

## Migración y verificación

`AddMediaSourceAndTags` añade columnas con defaults constantes (`file`, `[]`),
sin backfill ni cambio de filas clínicas. Índice GIN de etiquetas concurrente,
checks NOT VALID/VALIDATE y reversa protegida documentados en la migración.
El Down bloquea brevemente en una ventana sin escritores y aborta si hay etiquetas
o enlaces nuevos: exportarlos/restablecerlos de forma explícita antes de revertir.
Si se interrumpe CONCURRENTLY, comprobar índices inválidos y retirarlos antes del retry.
La BD local (55 medios) no se migró en esta fase; Up/Down/Up se probaron con
10.000 medios en una BD efímera. EXPLAIN ANALYZE usa `ix_media_items_tags`.
Pruebas de validación URL por proveedor, filtros SQL combinados, total/páginas,
idempotencia, persistencia y lifecycle externo sin llamadas a storage.

## Nota de Luis para 1.12 (pendiente, sin implementar aquí)

El drawer de `Biblioteca de podcasts.png` dice: al reemplazar el archivo se
conservan las asignaciones y se avisa a cuántos pacientes llega; despublicar un
podcast en uso pide elegir sustituto. El contrato de reemplazo/despublicación
debe devolver **cuántos pacientes y qué plantillas están afectados**, para que el
front pinte esa advertencia. Esta nota no habilita todavía la tarea 1.12.
