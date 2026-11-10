# Exámenes de laboratorio: archivo y extracción pendiente

## Límite del flujo

`UploadLabExamCommandHandler` conserva el documento original y los candidatos de
la extracción automática. Una respuesta OCR **no equivale a una medición clínica
verificada**. El flujo no inserta `ClinicalMeasurement`, no reemplaza unidades ni
fechas ausentes, no completa controles del programa y no inyecta los candidatos
como hechos establecidos en el contexto del chat. Las mediciones históricas y los
registros manuales o profesionales existentes se conservan sin cambios.

El mensaje devuelto es determinista en español o inglés e indica que los valores
requieren verificación contra el original. No se muestra el resumen clínico libre
del extractor y no se solicita narración del OCR pendiente al servicio de IA.

## Conservación y procedencia

- El binario **original**, sin compresión ni transformación, se almacena como
  `documents/{patientId}/{documentId}/original.{ext}`. La versión comprimida se usa solamente
  como entrada de extracción.
- Se crea una fila en el repositorio documental existente, con paciente, clínica
  y tipo genérico activo `OTHER`. No se adivina un subtipo de laboratorio. Si ese
  tipo no está disponible o no admite la extensión, la operación falla antes de
  almacenar o enviar el archivo al extractor.
- `Document.Id` coincide con `batchId`; el estado documental `Ready` indica que
  el archivo está disponible, **no** que los valores estén aprobados. La descripción
  visible explica que la extracción está pendiente, sin rutas técnicas.
- Los candidatos se conservan también en el objeto JSON
  `documents/{patientId}/{documentId}/extraction.json`, derivable de los identificadores
  documentales. Incluye `schemaVersion: 1`, `batchId`, `documentId`,
  `originalFileName`, `originalStorageKey`, `uploadedAt`, `verificationStatus`,
  `extractedMetrics` y `comparisons`. Conserva texto/página de origen si el
  extractor los proporciona. Estas referencias no hacen públicos los objetos.
- Si la persistencia documental falla, se intenta compensar la escritura de los
  objetos. No se modifica ninguna medición previa. Un fallo del extractor devuelve
  error y no afirma que el archivo haya quedado archivado correctamente.

Este cambio no implementa una cola duradera de revisión, notificaciones automáticas
a profesionales ni un nuevo acto de aprobación. El equipo debe consultar el
original y usar el flujo existente de registro profesional cuando corresponda.
Los candidatos no se promueven automáticamente al historial clínico.

## Contrato de respuesta aditivo

Se conservan los cinco campos anteriores; `measurementCount` cuenta mediciones
clínicas establecidas y por eso vale **0** en este flujo. `detectedMetrics` describe
coincidencias tentativas con el catálogo, no aprobación ni guardado de mediciones.

```json
{
  "batchId": "uuid",
  "summary": "Archivo recibido ... requiere verificación ...",
  "measurementCount": 0,
  "detectedMetrics": ["Presión sistólica"],
  "storageKey": "documents/patient-uuid/document-uuid/original.pdf",
  "verificationStatus": "pending_review",
  "documentId": "uuid",
  "extractionStorageKey": "documents/patient-uuid/document-uuid/extraction.json",
  "extractedMetrics": [
    {
      "metricName": "presión sistólica",
      "metricCode": "systolic_bp",
      "value": 60,
      "unitSymbol": "mmHg",
      "observedAt": "2026-10-20T00:00:00Z",
      "source": "ocr_unverified",
      "sourceText": "60 mmHg",
      "sourcePage": 1
    }
  ],
  "comparisons": {
    "systolic_bp": {
      "direction": "changed",
      "previous_value": 120,
      "previous_unit": "mmHg",
      "previous_date": "2026-10-19T00:00:00Z",
      "delta": -60,
      "current_value": 60,
      "current_unit": "mmHg",
      "change": "decrease",
      "current_date": "2026-10-20T00:00:00Z",
      "previous_source": "professional",
      "current_source": "ocr_unverified"
    }
  }
}
```

`verificationStatus` es `pending_review`, `unreadable` o `no_metrics`. Las tres
opciones mantienen `measurementCount: 0`. Con una unidad o fecha no impresa,
`unitSymbol`/`observedAt` permanecen en `null`; no se usa la unidad predeterminada
del catálogo ni la fecha de subida. Una métrica desconocida conserva el nombre
extraído y `metricCode: null`. El cliente debe mostrar recepción/pendiente y no
«mediciones guardadas», «procesado» ni «control completado».

## Comparación factual

El contrato interno de IA conserva los nombres `snake_case` de `MetricEvolution`.
`direction` solo se emite como `changed`, `stable` o `first_record`. Los valores
históricos `improved`/`worsened` se pueden deserializar por compatibilidad, pero se
serializan de forma neutral como `changed`.

`change` expresa `increase`, `decrease`, `unchanged`, `not_comparable` o
`first_record`. Solo se calcula `delta` si ambas unidades son explícitas e iguales
(comparación sin distinción de mayúsculas y espacios exteriores) y ambas fechas
están presentes y ordenadas. No se convierten unidades, aplican tolerancias clínicas
ni deduce beneficio de una disminución: pasar de 120 a 60 mmHg indica únicamente
`decrease`, no «mejoró». En `not_comparable` se conservan ambos valores, unidades,
fechas y fuentes y `delta` es `null`.

La fuente histórica se devuelve tal como está almacenada: incluso `lab` puede
representar extracción OCR previa y no prueba verificación. La fuente actual es
siempre `ocr_unverified`. Si falla la consulta del historial, `comparisons` queda
vacío; no se inventa un primer registro.

## Verificación

Pruebas enfocadas en `tests/CoppAddresd.UnitTests/LabExam/`: cambios numéricos
potencialmente peligrosos sin juicio clínico, igualdad exacta y pequeñas diferencias,
fechas/unidades faltantes o no comparables, lectura de contratos históricos,
preservación del original y candidatos, ausencia de inserción clínica/contexto IA y
de finalización de controles, fallos documentales con compensación y cancelación.
