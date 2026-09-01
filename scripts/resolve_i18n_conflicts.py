"""Resuelve conflictos de merge en es.json/en.json combinando ambos lados (HEAD + dev),
deduplicando por key. Mantiene el formato del archivo (JSON, indent 2)."""

import json
import re
from pathlib import Path

BASE = (
    Path(__file__).resolve().parent.parent.parent / "antares-paciente" / "src" / "i18n"
)


def strip_trailing_commas(text: str) -> str:
    # Quita comas antes de } o ] (el proyecto usa trailing commas en JSON)
    return re.sub(r",(\s*[}\]])", r"\1", text)


def parse(block: str) -> dict:
    return json.loads(strip_trailing_commas("{" + block + "}"))


def resolve(path: Path) -> None:
    raw = path.read_text(encoding="utf-8")
    if "<<<<<<<" not in raw:
        print(f"{path.name}: sin conflictos")
        return

    # Estructura: ...base<<<<<<< HEAD\n(head)\n=======\n(dev)\n>>>>>>> hash\n...rest
    parts = re.split(r"<<<<<<< HEAD\r?\n|=======\r?\n|>>>>>>> [^\r\n]*\r?\n", raw)
    if len(parts) != 4:
        print(f"{path.name}: estructura inesperada ({len(parts)} partes)")
        return
    base, head_block, dev_block, rest = parts

    head_json = parse(head_block)
    dev_json = parse(dev_block)

    full = json.loads(strip_trailing_commas(base + rest))
    merged = dict(full)
    # dev primero, HEAD encima (gana HEAD en colisión, como el merge de git)
    merged.update(dev_json)
    merged.update(head_json)

    # orden: HEAD block antes de dev block (las keys de HEAD conservan su posición)
    ordered = {}
    for k in head_json:
        ordered[k] = merged[k]
    for k in dev_json:
        if k not in ordered:
            ordered[k] = merged[k]
    for k, v in full.items():
        if k not in ordered:
            ordered[k] = v

    # formato: "key": "value", con la coma final que usa el proyecto
    lines = ["{"]
    keys = list(ordered.keys())
    for i, k in enumerate(keys):
        comma = "," if i < len(keys) - 1 else ""
        lines.append(
            f"  {json.dumps(k, ensure_ascii=False)}: {json.dumps(ordered[k], ensure_ascii=False)}{comma}"
        )
    lines.append("}")
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(
        f"{path.name}: resuelto ({len(head_json)} keys HEAD + {len(dev_json)} keys dev, total {len(ordered)})"
    )


resolve(BASE / "es.json")
resolve(BASE / "en.json")
