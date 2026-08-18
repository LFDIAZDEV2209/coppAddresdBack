# Caché y diseño: búsqueda de códigos postales (autocompletado escalable)

## Problema

La tabla `app.postal_codes` nunca puede contener los códigos postales del
mundo (~40k+ solo en EE. UU.): no es escalable ni mantenible. El formulario
de pacientes necesita sugerir códigos postales con el flujo moderno:

```text
Country → State → City → Address → Postal Code
        (y también: escribir el ZIP y que se complete ciudad/estado/país)
```

## Decisión técnica

**Proveedor externo (Zippopotam) vía proxy del backend, con caché y
fail-open a la BD local.** Comparación de alternativas:

| Opción | Costo | API key | Cobertura | Veredicto |
|---|---|---|---|---|
| Zippopotam | Gratis | No | EE. UU. + ~50 países | **Elegido**: perfecto para lookup por código y por ciudad |
| GeoNames | Gratis (créditos/día) | Username | Mundial | Alternativa si se necesita búsqueda por prefijo global |
| Google/Mapbox/HERE (address autocomplete) | Por llamada | Sí | Mundial | Overkill y costoso para solo ZIP; reservado para autocompletado de dirección completo |
| SmartyStreets/Lob | Por lookup | Sí | EE. UU. (precisión) | Validación de dirección pagada, no necesario hoy |

Zippopotam devuelve por código (`/us/92101` → ciudad/estado) y por ciudad
(`/us/CA/San Diego` → todos los ZIPs). Sin SLA ni límites documentados →
por eso: caché (24 h) + timeout corto (3 s) + fallback.

## Implementación

```
Frontend (ZIP combobox)
   → GET /api/v1/catalogs/postal-codes/search
        ?countryCode=US&stateCode=CA&city=...&zip=...        (proxy)
   → SearchPostalCodesQuery (Application)
   → IPostalCodeLookupService (Application/Interfaces)
   → ZippopotamPostalCodeLookup (Infrastructure)
        ├─ caché IMemoryCache (TTL 24 h, ver abajo)
        ├─ HTTP client nombrado "Zippopotam" (BaseUrl configurable)
        └─ fail-open → app.postal_codes (datos seed dev/offline)
   → enriquecimiento: CityId resuelto contra app.cities (una consulta IN)
```

Reglas de búsqueda (handler):
- `zip` + `city` + `state` → ZIPs de la ciudad filtrados por prefijo (autocomplete).
- `zip` solo (≥5 dígitos) → lookup directo → sugiere ciudad/estado (autofill).
- `city` + `state` → todos los ZIPs de la ciudad (autofill si hay exactamente 1).

## Caché documentada

| Qué se almacena | TTL | Invalidación | Consistencia | Tamaño estimado | Impacto |
|---|---|---|---|---|---|
| Resultados Zippopotam por clave `zip:{país}:{código}` y `city:{país}:{estado}:{ciudad}` | 24 h (configurable `PostalCodeLookup:CacheTtlMinutes`) | Ninguna (datos estáticos; caduca sola) | Eventual (24 h de tolerancia, aceptable) | Decenas de KB (solo claves consultadas) | Evita golpear al proveedor por cada keystroke del formulario |

El **enriquecimiento** (CityId del catálogo local) se hace después de la caché,
por lo que siempre refleja el catálogo actual. La clave de ciudad incluye el
nombre tal como lo devuelve el proveedor: un mismo lugar con distintas
normalizaciones genera claves distintas (caso límite aceptado).

## Fail-open

- Proveedor deshabilitado (`Enabled: false`), timeout (3 s), error de red o
  HTTP 404 → **nunca 500**: se degrada a `app.postal_codes` (si aplica) y se
  registra Warning con la ruta. Lista vacía si no hay datos locales.
- La BD local **nunca** es la fuente primaria: solo seed de desarrollo.

## Configuración

```jsonc
// appsettings.json → "PostalCodeLookup"
{
  "Enabled": true,                          // false = solo BD local (offline)
  "BaseUrl": "https://api.zippopotam.us",
  "TimeoutSeconds": 3,
  "CacheTtlMinutes": 1440                   // 24 h
}
```

## Notas de integración

- Estados: Zippopotam exige el código de estado en el formato del país
  (EE. UU. → mayúsculas, `CA`); los códigos de `app.states` son ISO 3166-2.
- Búsquedas por ciudad devuelven el código postal en cada `place`; por código,
  a nivel raíz — el mapeo cubre ambos.
- Cambiar de proveedor en el futuro = nueva implementación de
  `IPostalCodeLookupService` (interfaz única, DI). El frontend no cambia.
