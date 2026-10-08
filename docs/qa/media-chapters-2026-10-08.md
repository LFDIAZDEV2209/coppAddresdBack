# Compatibilidad de capítulos históricos

El seed almacena capítulos JSON con atSeconds/label en camelCase; el convertidor de EF usaba nombres sensibles a mayúsculas. Al materializar esos registros se obtenían tiempos cero y etiquetas nulas.

El convertidor de MediaItem.Chapters acepta nombres sin distinguir mayúsculas. Conserva el formato de escritura anterior y no modifica datos ni requiere migración. Las regresiones usan el convertidor real de EF para camelCase, PascalCase, lista vacía y roundtrip.

Validación: compilación completa de la solución sin errores; 56 casos de capítulos, ciclo de vida, referencias y almacenamiento ejecutados directamente con sus aserciones xUnit. VSTest necesita un socket local permitido, por lo que este sandbox usa invocación directa y no equivale a una corrida completa del pipeline.

## Subida por gateway

S3ObjectStorageService copia cuerpos HTTP no seekables a un archivo temporal seekable antes de llamar al SDK de S3. El archivo se elimina al terminar y el stream del llamador queda abierto. La regresión cubre contenido íntegro, posición inicial, longitud, cierre del temporal y preservación del body.

El navegador confirmó que la subida directa falla por red y el fallback autenticado llega al gateway pero devuelve HTTP 500. Se encontró este defecto de almacenamiento en el código; se debe desplegar y repetir la subida para confirmar si resuelve el fallo de producción. Publicación y asignación de un contenido nuevo aún están pendientes.
