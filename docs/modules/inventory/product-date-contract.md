# Fecha de vencimiento del catálogo

`expirationDate` en las respuestas de productos (lista, detalle, creación,
actualización y productos incluidos en analítica) representa una fecha de
calendario: JSON `YYYY-MM-DD` o `null`. No representa un instante UTC.

La columna existente es PostgreSQL `date`. El dominio mantiene `DateTime?`
y los requests existentes siguen aceptando su contrato; los DTOs de respuesta
convierten a `DateOnly?` sin aplicar una conversión de zona horaria.
`createdAt` y `updatedAt` siguen siendo timestamps.

Esto corrige el valor inválido para `<input type="date">` en edición del ERP
y la concatenación de `T00:00:00` usada para clasificar el vencimiento. Antes
la respuesta incluía `2030-01-31T00:00:00` y la edición mostraba el campo vacío.
No hay migración ni cambio a fechas almacenadas.

Pruebas: `ProductDateContractTests`, serialización de lista/detalle con Kind
Unspecified/UTC/Local y vencimiento nulo. Requiere despliegue de API y retest
en el ERP antes de declarar el defecto corregido en producción.
