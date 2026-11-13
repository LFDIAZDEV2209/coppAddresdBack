# Clasificación del origen — tarea 1.6

Ejecutar con Python + `psycopg2` y las variables estándar `PGHOST`, `PGPORT`,
`PGDATABASE`, `PGUSER`, `PGPASSWORD` resueltas desde el entorno seguro. Nunca
pasar credenciales por argumentos ni guardarlas en un archivo del workspace.

```text
python scripts/classify_week_snapshot_origin.py --mode simulate
python scripts/classify_week_snapshot_origin.py --mode simulate --reverse
```

La simulación es el modo predeterminado y usa transacciones de solo lectura.
El JSON de salida solo contiene conteos, motivos y conflictos; no pacientes ni DSN.
En local se ejecutó únicamente simulación. La ejecución de relleno de asignaciones
antiguas pertenece a 2.10; este script clasifica los ítems existentes y no inventa
puntos ni copia asignaciones. Su coincidencia con una asignación antigua se
conserva como individual (con actor o ambigua sin actor).

La aplicación explícita es `--mode apply`; la reversa, `--mode reverse`.
Antes: revisar simulación, respaldo y ventana operativa. `--batch-size` (500 por
defecto, máximo 5000) limita cada transacción; paginación por UUID, sin OFFSET y
un UPDATE de conjunto por lote. Un fallo revierte el lote actual; los previos
pueden estar confirmados. Repetir es idempotente. Un conflicto concurrente se
informa y se reevalúa en otra pasada; no se sobrescribe la edición ni activación.

Solo se tocan semanas bloqueadas, sin activación ni cierre y que empiezan después
del día local actual. Las empezadas/completadas permanecen de solo lectura.
`--today` permite fijar la lectura para pruebas; la escritura siempre vuelve a
comprobar la fecha real. Omitirlo en operación.

Se compara contra la versión de plantilla al inicio solo si está disponible:
misma versión vigente y sin edición posterior a la creación de la semana.
El catálogo no conserva revisiones históricas: si no hay prueba, se conserva
como individual y se informa el motivo. Una fila específica gana a la genérica.
JSON inválido/duplicado se informa y se deja intacto.

Cada cambio guarda `backfill_version: 1` y `_origin_backfill_v1` con la preimagen
exacta del ítem. Reversa restaura solo ítems todavía idénticos a lo aplicado,
y únicamente en semanas futuras; si hubo una edición o activación posterior,
no la deshace. No retirar estas preimágenes antes de cerrar la ventana de reversa.

## Simulación local — 13-11-2026

- Semanas totales: 11.788.
- Excluidas por estado/fecha: 3.326.
- Futuras evaluadas: 8.462, todas con snapshot vacío.
- Ítems program/individual, conflictos y semanas cambiadas: 0.
- Escrituras: **0** (conexión de solo lectura).

Prueba PostgreSQL en BD efímera: simulación sin cambios, igualdad específica y
genérica, ambiguos individuales, aplicación repetida sin cambios, reversa exacta,
reversa repetida, y conservación de ediciones posteriores y semanas no elegibles.
