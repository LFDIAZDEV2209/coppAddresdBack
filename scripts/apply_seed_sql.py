"""Aplica un archivo SQL de Migrations/Seed a la BD local de desarrollo.

Contexto: los seeds de catálogos viajan embebidos en migraciones EF que solo
corren una vez. En una BD ya migrada, regenerar el SQL con
`generate_*_seed.py` NO reaplica nada por sí solo (caso real: `URGENT_CARE`
generado pero ausente en local). Este script ejecuta el archivo tal cual; los
INSERTs usan `ON CONFLICT DO NOTHING`, así que es idempotente y seguro
reejecutar. Solo para desarrollo local, nunca contra producción.

Uso desde coppAddresdBack/scripts con:
  uv run --with psycopg[binary] python apply_seed_sql.py <ruta-al-sql>

Ejemplo:
  uv run --with psycopg[binary] python apply_seed_sql.py ../coppAddresdBack/src/CoppAddresd.Infrastructure/Migrations/Seed/AddProfessionalCatalogs.sql
"""

from __future__ import annotations

import sys
from pathlib import Path

import psycopg

DB_DSN = "host=localhost port=5432 dbname=coppaddresd user=app_user password=CoppAddresdDev!2026"


def main() -> None:
    if len(sys.argv) != 2:
        print("Uso: apply_seed_sql.py <ruta-al-archivo-sql>")
        raise SystemExit(2)

    path = Path(sys.argv[1])
    if not path.is_file():
        print(f"No existe el archivo: {path}")
        raise SystemExit(2)

    sql = path.read_text(encoding="utf-8")
    with psycopg.connect(DB_DSN) as conn:
        conn.execute(sql)

    print(f"Aplicado (idempotente) {path.name}")


if __name__ == "__main__":
    main()
