# Correspondencia entre indicaciones españolas e inglesas

`NutritionPrescription.Instructions` conserva las indicaciones fuente y
`InstructionsEn` su texto inglés opcional. `InstructionsEnSourceHash` registra
el SHA-256 hexadecimal de los bytes UTF-8 exactos de `Instructions` cuando se
recibe una pareja explícita ES/EN. No se recortan espacios ni se normaliza Unicode.
Estos campos son independientes de las cantidades estructuradas, las comidas
y los metadatos de aprobación.

Al guardar un borrador mediante `NutritionCases`:

- Si cambia exactamente `Instructions` y no llega `InstructionsEn`, se elimina
  el inglés anterior y su hash. No se reutiliza una traducción de otra indicación.
- Si la fuente es idéntica, o no se envía una edición de la fuente, se conserva
  el inglés y el hash existentes, incluido un hash histórico `null`.
- Si el profesional envía una nueva pareja ES/EN, se conservan ambos textos
  tal como llegaron y se guarda el hash de esa fuente. Ambos deben tener
  contenido visible para establecer procedencia.
- Una corrección solo inglesa conserva ese texto para revisión del equipo,
  pero elimina el hash: la fuente debe enviarse con la traducción para
  permitir su reutilización en la lectura del paciente.
- La generación, incluido el fallback determinista bilingüe existente, guarda
  el hash de su pareja. Regenerar conserva la procedencia cuando se conservan
  las ediciones confirmadas, o la sustituye junto con la pareja nueva. Crear
  otra versión copia el hash existente sin validar automáticamente parejas antiguas.

La comparación de la fuente es ordinal, sin recortar ni normalizar espacios;
una variación de contenido o formato invalida la traducción anterior. No se
invoca IA durante esta escritura. La instantánea de auditoría registra la pareja
resultante y el hash, incluido `instructionsEn: null` cuando se invalida. Se mantienen la
validación, la revisión y las reglas de publicación existentes. Esta corrección
no aprueba un borrador ni modifica dosis, gramos, macros, horarios o comidas.

La lectura móvil solo expone `notesEn` si el hash coincide con la fuente actual
y la comida usa la indicación general. Un hash nulo o distinto evita presentar
el inglés histórico como correspondiente a esa fuente. El filtro de presentación
traduce entonces la fuente española vigente sin modificar la entidad, la nota
específica, las cantidades ni la aprobación. La caché del traductor identifica
la fuente exacta, el idioma y la versión de su política.

La migración `20261106142836_AddNutritionInstructionTranslationProvenance` añade
`app.nutrition_prescriptions.instructions_en_source_hash` como `varchar(64)`
nullable, sin default, backfill ni índice (se comprueba después de cargar el plan
por su selección existente). Las filas antiguas conservan ES/EN y quedan sin
procedencia verificada. `Down` elimina solo esta columna; `Up` después de `Down`
no restaura hashes perdidos ni valida automáticamente el inglés conservado.

Regresión sin base de datos ni proveedor externo:

```bash
dotnet test tests/CoppAddresd.UnitTests --filter FullyQualifiedName~NutritionInstructionTranslationTests
```

El test `NutritionInstructionProvenanceMigrationTests` usa PostgreSQL real y
`COP_TEST_DB_CONNECTION` solo como conexión administrativa para crear y eliminar
una base efímera. Aplica Up/Down/Up sobre 1.001 planes históricos y verifica
fuentes, notas, porciones, macros, horarios, IDs y aprobación sin cambiarlos.
