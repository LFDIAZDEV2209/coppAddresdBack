"""Asigna avatares/covers a los perfiles ANTARES por display_name (UTF-8 nativo vía psycopg).
Los archivos ya están subidos al storage local."""

import psycopg

DSN = "host=127.0.0.1 port=5432 dbname=coppaddresd user=app_user password=CoppAddresdDev!2026"

ANTARES = [
    (
        "Valentina Ríos",
        "community/avatars/3e3f87d651fb4d3dac2a6a7081eadd3d.jpeg",
        "community/covers/63a9e42908f14a20813a4c8ce990896c.jpg",
    ),
    (
        "Andrés Cárdenas",
        "community/avatars/4089d60b8a24458a8cc0b4f34306453b.png",
        "community/covers/68cd942749f64fc7859b047777ee4232.jpg",
    ),
    (
        "Carolina Mendoza",
        "community/avatars/46d9cfdf151f429cbdda88d2123c3b4f.jpeg",
        "community/covers/a4812b7d8eb049afa71f76b18edb011a.webp",
    ),
    (
        "Jorge Herrera",
        "community/avatars/668e9e9f32f34f56b5622b962c2924aa.jpeg",
        "community/covers/a4c3bb1998fe4a539c6e9aee5e3cd55f.webp",
    ),
    (
        "Luisa Fernández",
        "community/avatars/8736ce4a458f43608f187d2f32a12c7f.jpg",
        "community/covers/ba3c896b678942b591aa0564f81c4658.jpeg",
    ),
    (
        "Miguel Angel Peña",
        "community/avatars/c32e6d0b8ba943dc86c8d63cdae413ca.jpeg",
        "community/covers/bbcfcbf685714b349315975f7667bf78.jpg",
    ),
    (
        "Diana Ospina",
        "community/avatars/e6bdc01cc2c747d0a0482f96559a2aa6.jpeg",
        "community/covers/e866038d43c74ab580ad0b7be2a5b3eb.webp",
    ),
    (
        "Camilo Restrepo",
        "community/avatars/f8a1a08788524a999d48da58e26d7ac9.jpeg",
        "community/covers/fd14c2551aef418686c5eda5aadd55c6.webp",
    ),
]

with psycopg.connect(DSN) as conn:
    with conn.cursor() as cur:
        for name, avatar, cover in ANTARES:
            cur.execute(
                "UPDATE community.profiles SET avatar_key = %s, cover_key = %s, updated_at = now() WHERE display_name = %s AND avatar_key IS NULL",
                (avatar, cover, name),
            )
            print(f"  {name}: {cur.rowcount} fila(s) actualizada(s)")
        # Juan Pérez (login demo de la app)
        cur.execute(
            "UPDATE community.profiles SET avatar_key = %s, cover_key = %s, updated_at = now() WHERE display_name = 'Juan Pérez' AND avatar_key IS NULL",
            (ANTARES[0][1], ANTARES[0][2]),
        )
        print(f"  Juan Pérez: {cur.rowcount} fila(s) actualizada(s)")
    conn.commit()
    with conn.cursor() as cur:
        cur.execute(
            "SELECT display_name, avatar_key IS NOT NULL, cover_key IS NOT NULL "
            "FROM community.profiles WHERE avatar_key IS NOT NULL OR cover_key IS NOT NULL "
            "ORDER BY created_at LIMIT 15"
        )
        for r in cur.fetchall():
            print(f"  {r[0]}: avatar={r[1]} cover={r[2]}")
