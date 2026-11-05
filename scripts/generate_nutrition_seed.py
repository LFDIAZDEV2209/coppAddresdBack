#!/usr/bin/env python3
"""Genera el seed idempotente del catalogo exacto de engine-rules-spec.md, seccion 5."""
import argparse
from pathlib import Path

# Fuente: prototipo SHA256 97f035539be608e172c92b66452008a8f305933e3e4f5c63f4a5945f910eb3ff.
# Filas literales por 100 g; no recalcular las kcal declaradas desde los macros.
ROWS = [
    ['avena', '🥣', 'Avena en hojuelas (seca)', 'C', '379', '67.7', '13.2', '6.5', '10.1', '—', '—', '—', '—'],
    ['panint', '🍞', 'Pan integral 100%', 'C', '250', '42', '12.5', '3.5', '6', 'rebanada', 'rebanadas', '30', '.5'],
    ['arroz', '🍚', 'Arroz integral cocido', 'C', '123', '25.6', '2.7', '1', '1.6', '—', '—', '—', '—'],
    ['quinoa', '🌾', 'Quinoa cocida', 'C', '120', '21.3', '4.4', '1.9', '2.8', '—', '—', '—', '—'],
    ['camote', '🍠', 'Camote cocido', 'C', '90', '20.7', '2', '.2', '3.3', '—', '—', '—', '—'],
    ['platano', '🍌', 'Plátano verde cocido', 'C', '116', '31.2', '.8', '.2', '2.3', '—', '—', '—', '—'],
    ['lentejas', '🫘', 'Lentejas cocidas', 'C', '116', '20.1', '9', '.4', '7.9', '—', '—', '—', '—'],
    ['frijol', '🫘', 'Frijoles negros cocidos', 'C', '132', '23.7', '8.9', '.5', '8.7', '—', '—', '—', '—'],
    ['garbanzo', '🫘', 'Garbanzos cocidos', 'C', '164', '27.4', '8.9', '2.6', '7.6', '—', '—', '—', '—'],
    ['frutos', '🍓', 'Frutos rojos mixtos', 'C', '45', '11', '.7', '.3', '3', '—', '—', '—', '—'],
    ['manzana', '🍎', 'Manzana', 'C', '52', '13.8', '.3', '.2', '2.4', 'manzana', 'manzanas', '180', '.5'],
    ['banano', '🍌', 'Banano', 'C', '89', '22.8', '1.1', '.3', '2.6', 'banano', 'bananos', '120', '.5'],
    ['papaya', '🥭', 'Papaya', 'C', '43', '10.8', '.5', '.3', '1.7', '—', '—', '—', '—'],
    ['clara', '🥚', 'Claras de huevo', 'P', '52', '.7', '10.9', '.2', '0', 'clara', 'claras', '33', '1'],
    ['huevo', '🍳', 'Huevo entero', 'P', '143', '.7', '12.6', '9.5', '0', 'huevo', 'huevos', '50', '1'],
    ['yogur', '🥛', 'Yogur griego natural sin azúcar', 'P', '59', '3.6', '10.2', '.4', '0', '—', '—', '—', '—'],
    ['cottage', '🧀', 'Queso cottage bajo en grasa', 'P', '72', '2.7', '12.4', '1', '0', '—', '—', '—', '—'],
    ['pollo', '🍗', 'Pechuga de pollo a la plancha', 'P', '165', '0', '31', '3.6', '0', '—', '—', '—', '—'],
    ['pavo', '🦃', 'Pechuga de pavo', 'P', '135', '0', '30', '.7', '0', '—', '—', '—', '—'],
    ['salmon', '🐟', 'Salmón al horno', 'P', '206', '0', '22.1', '12.4', '0', '—', '—', '—', '—'],
    ['tilapia', '🐟', 'Tilapia a la plancha', 'P', '128', '0', '26.2', '2.7', '0', '—', '—', '—', '—'],
    ['atun', '🐟', 'Atún en agua', 'P', '116', '0', '25.5', '.8', '0', '—', '—', '—', '—'],
    ['tofu', '🍲', 'Tofu firme', 'P', '144', '2.8', '17.3', '8.7', '2.3', '—', '—', '—', '—'],
    ['aceite', '🫒', 'Aceite de oliva extra virgen', 'F', '884', '0', '0', '100', '0', 'cucharadita', 'cucharaditas', '4.5', '.5'],
    ['almendras', '🌰', 'Almendras naturales sin sal', 'F', '579', '21.6', '21.2', '49.9', '12.5', '—', '—', '—', '—'],
    ['nueces', '🥜', 'Nueces', 'F', '654', '13.7', '15.2', '65.2', '6.7', '—', '—', '—', '—'],
    ['chia', '🌱', 'Semillas de chía', 'F', '486', '42.1', '16.5', '30.7', '34.4', '—', '—', '—', '—'],
    ['aguacate', '🥑', 'Aguacate', 'F', '160', '8.5', '2', '14.7', '6.7', '—', '—', '—', '—'],
    ['hummus', '🧆', 'Hummus', 'F', '166', '14.3', '7.9', '9.6', '6', '—', '—', '—', '—'],
    ['ensalada', '🥗', 'Ensalada de hojas, tomate y pepino', 'V', '17', '3.3', '1.2', '.2', '1.5', '—', '—', '—', '—'],
    ['brocoli', '🥦', 'Brócoli al vapor', 'V', '35', '7.2', '2.4', '.4', '3.3', '—', '—', '—', '—'],
    ['espinaca', '🥬', 'Espinaca', 'V', '23', '3.6', '2.9', '.4', '2.2', '—', '—', '—', '—'],
    ['vegcoc', '🥒', 'Vegetales salteados al vapor', 'V', '35', '7', '2', '.3', '3', '—', '—', '—', '—'],
    ['zanahoria', '🥕', 'Zanahoria en bastones', 'V', '41', '9.6', '.9', '.2', '2.8', '—', '—', '—', '—'],
    ['te', '🍵', 'Té verde sin azúcar', 'V', '1', '0', '0', '0', '0', '—', '—', '—', '—'],
]


def sql_literal(value):
    if value == "\u2014":
        return "NULL"
    return "'" + value.replace("'", "''") + "'"


def generate():
    columns = ("id", "icon", "name", "food_group", "calories_100", "carbs_100",
               "protein_100", "fat_100", "fiber_100", "unit_singular", "unit_plural",
               "grams_per_unit", "unit_step")
    lines = ["-- Generado por scripts/generate_nutrition_seed.py; no editar manualmente.",
             "-- Catalogo exacto del prototipo; valores declarados por 100 g."]
    for row in ROWS:
        values = [sql_literal(v) if i in (0, 1, 2, 3, 9, 10) or v == "\u2014" else v
                  for i, v in enumerate(row)]
        updates = ", ".join(f"{column} = EXCLUDED.{column}" for column in columns[1:])
        lines.append(f"INSERT INTO app.nutrition_food_items ({', '.join(columns)}) "
                     f"VALUES ({', '.join(values)}) ON CONFLICT (id) DO UPDATE SET {updates};")
    return "\n".join(lines) + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=Path(__file__).resolve().parents[1] /
                        "src/CoppAddresd.Infrastructure/Migrations/Seed/AddNutritionFoodCatalog.sql")
    parser.add_argument("--check", action="store_true", help="Verifica sin escribir el SQL generado")
    args = parser.parse_args()
    data = generate().encode("utf-8")
    if args.check:
        if not args.output.exists() or args.output.read_bytes() != data:
            raise SystemExit("El SQL no coincide con el generador")
        print("Seed identico: 35 alimentos")
    else:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_bytes(data)
        print("Seed generado: 35 alimentos")


if __name__ == "__main__":
    main()
