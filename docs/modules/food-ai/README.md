# Food AI: nombres bilingües de análisis

La ingesta conserva `FoodAnalysisItem.NameEn` cuando el proveedor entrega
`name_en`. `Name`, porciones, nutrición y feedback conservan su significado y
valores originales. La migración `AddFoodAnalysisEnglishNames` añade únicamente
`foodai.food_analysis_items.name_en` como texto nullable, sin default ni backfill.
Su `Down` elimina esa columna y conserva las filas y datos originales.

`POST /api/v1/foodai/analyze` y `GET /api/v1/foodai/analyses/{id}` admiten
`?language=en|es` (con prioridad sobre `Accept-Language`; sin selección, español).
El detalle autenticado conserva la autorización antes de resolver nombres.
La respuesta añade `foods[].nameEn`; nunca sustituye `foods[].name`.

Para un resultado histórico sin inglés persistido:

1. Se consulta la identidad exacta del catálogo curado empacado
   `Seeders/data/food_usda_curated.json`: canonical inglés o nombre visible
   conocido, solo cuando el mapeo es único. No se usa una equivalencia de
   nutrientes USDA como nombre de otro alimento.
2. Los nombres restantes pasan a `IContentTranslationService` como texto de
   presentación. Solo se envían los nombres necesarios, sin imagen, identidad,
   porciones ni valores nutricionales. El original permanece intacto.
3. Si la traducción falla o devuelve un mapa incompleto, la API devuelve el
   error recuperable de traducción, nunca otro idioma presentado como éxito.

La fuente .NET y el servicio de análisis histórico no retienen un `raw_json`
con traducciones descartadas. Por eso no existe backfill seguro desde un
resultado original omitido: el rescate de catálogo/traductor es de lectura,
no escribe análisis antiguos. La ingesta futura sí persiste el inglés original.
La app solicita el idioma activo y vuelve a hidratar el detalle al cambiarlo.

Validación: handler de ingesta, autorización y proyección del controlador,
prioridad persistido/catálogo/traductor, ambigüedad de catálogo y cancelación.
La prueba PostgreSQL crea y elimina solo `copp_foodai_i18n_test_*`, añade 2.000
filas legacy, ejecuta Up/Down/Up y comprueba identidad, porciones, macros y
round-trip del inglés futuro. No modifica la base configurada de entrada.
