"""Genera Migrations/Seed/AddPatientCatalogs.sql con el seed de catálogos de
pacientes (países, estados US, ciudades, ZIPs, tipos de documento, etnias,
grupos sanguíneos). Los INSERTs usan ON CONFLICT DO NOTHING: son idempotentes.

Ejecutar desde coppAddresdBack/scripts con:
  python generate_patient_catalogs_seed.py
"""

from pathlib import Path

OUT = Path(__file__).resolve().parent.parent / "src" / "CoppAddresd.Infrastructure" / "Migrations" / "Seed" / "AddPatientCatalogs.sql"

# (código ISO2, nombre, prefijo E.164 sin '+')
COUNTRIES = [
    ("US", "United States", "1"),
    ("CA", "Canada", "1"),
    ("MX", "Mexico", "52"),
    ("AL", "Albania", "355"),
    ("AR", "Argentina", "54"),
    ("AU", "Australia", "61"),
    ("AT", "Austria", "43"),
    ("BS", "Bahamas", "1"),
    ("BD", "Bangladesh", "880"),
    ("BE", "Belgium", "32"),
    ("BZ", "Belize", "501"),
    ("BO", "Bolivia", "591"),
    ("BR", "Brazil", "55"),
    ("BG", "Bulgaria", "359"),
    ("CM", "Cameroon", "237"),
    ("CL", "Chile", "56"),
    ("CN", "China", "86"),
    ("CO", "Colombia", "57"),
    ("CR", "Costa Rica", "506"),
    ("HR", "Croatia", "385"),
    ("CU", "Cuba", "53"),
    ("CZ", "Czech Republic", "420"),
    ("DK", "Denmark", "45"),
    ("DO", "Dominican Republic", "1"),
    ("EC", "Ecuador", "593"),
    ("EG", "Egypt", "20"),
    ("SV", "El Salvador", "503"),
    ("ET", "Ethiopia", "251"),
    ("FI", "Finland", "358"),
    ("FR", "France", "33"),
    ("DE", "Germany", "49"),
    ("GH", "Ghana", "233"),
    ("GR", "Greece", "30"),
    ("GT", "Guatemala", "502"),
    ("GY", "Guyana", "592"),
    ("HN", "Honduras", "504"),
    ("HU", "Hungary", "36"),
    ("IN", "India", "91"),
    ("ID", "Indonesia", "62"),
    ("IE", "Ireland", "353"),
    ("IL", "Israel", "972"),
    ("IT", "Italy", "39"),
    ("JM", "Jamaica", "1"),
    ("JP", "Japan", "81"),
    ("KE", "Kenya", "254"),
    ("KR", "South Korea", "82"),
    ("KW", "Kuwait", "965"),
    ("LK", "Sri Lanka", "94"),
    ("MA", "Morocco", "212"),
    ("MY", "Malaysia", "60"),
    ("NI", "Nicaragua", "505"),
    ("NG", "Nigeria", "234"),
    ("NO", "Norway", "47"),
    ("NZ", "New Zealand", "64"),
    ("PA", "Panama", "507"),
    ("PY", "Paraguay", "595"),
    ("PE", "Peru", "51"),
    ("PH", "Philippines", "63"),
    ("PL", "Poland", "48"),
    ("PT", "Portugal", "351"),
    ("PR", "Puerto Rico", "1"),
    ("QA", "Qatar", "974"),
    ("RO", "Romania", "40"),
    ("RU", "Russia", "7"),
    ("SA", "Saudi Arabia", "966"),
    ("SN", "Senegal", "221"),
    ("RS", "Serbia", "381"),
    ("SG", "Singapore", "65"),
    ("SK", "Slovakia", "421"),
    ("ZA", "South Africa", "27"),
    ("ES", "Spain", "34"),
    ("SR", "Suriname", "597"),
    ("SE", "Sweden", "46"),
    ("CH", "Switzerland", "41"),
    ("TW", "Taiwan", "886"),
    ("TZ", "Tanzania", "255"),
    ("TH", "Thailand", "66"),
    ("TT", "Trinidad and Tobago", "1"),
    ("TN", "Tunisia", "216"),
    ("TR", "Turkey", "90"),
    ("UA", "Ukraine", "380"),
    ("AE", "United Arab Emirates", "971"),
    ("GB", "United Kingdom", "44"),
    ("UY", "Uruguay", "598"),
    ("VE", "Venezuela", "58"),
    ("VN", "Vietnam", "84"),
]

# Estados/territorios US: 50 estados + DC + PR + VI + GU + AS + MP
US_STATES = [
    ("AL", "Alabama"), ("AK", "Alaska"), ("AZ", "Arizona"), ("AR", "Arkansas"),
    ("CA", "California"), ("CO", "Colorado"), ("CT", "Connecticut"), ("DE", "Delaware"),
    ("FL", "Florida"), ("GA", "Georgia"), ("HI", "Hawaii"), ("ID", "Idaho"),
    ("IL", "Illinois"), ("IN", "Indiana"), ("IA", "Iowa"), ("KS", "Kansas"),
    ("KY", "Kentucky"), ("LA", "Louisiana"), ("ME", "Maine"), ("MD", "Maryland"),
    ("MA", "Massachusetts"), ("MI", "Michigan"), ("MN", "Minnesota"), ("MS", "Mississippi"),
    ("MO", "Missouri"), ("MT", "Montana"), ("NE", "Nebraska"), ("NV", "Nevada"),
    ("NH", "New Hampshire"), ("NJ", "New Jersey"), ("NM", "New Mexico"), ("NY", "New York"),
    ("NC", "North Carolina"), ("ND", "North Dakota"), ("OH", "Ohio"), ("OK", "Oklahoma"),
    ("OR", "Oregon"), ("PA", "Pennsylvania"), ("RI", "Rhode Island"), ("SC", "South Carolina"),
    ("SD", "South Dakota"), ("TN", "Tennessee"), ("TX", "Texas"), ("UT", "Utah"),
    ("VT", "Vermont"), ("VA", "Virginia"), ("WA", "Washington"), ("WV", "West Virginia"),
    ("WI", "Wisconsin"), ("WY", "Wyoming"),
    ("DC", "District of Columbia"), ("PR", "Puerto Rico"), ("VI", "U.S. Virgin Islands"),
    ("GU", "Guam"), ("AS", "American Samoa"), ("MP", "Northern Mariana Islands"),
]

# Ciudades genéricas presentes en el dato legacy (seedeadas en TODOS los estados)
GENERIC_CITIES = ["Fairview", "Georgetown", "Madison", "Riverside", "Springfield"]

# Ciudades reales con sus ZIPs primarios (state code -> [(city, zip)])
REAL_CITIES = {
    "CA": [("Los Angeles", "90001"), ("San Francisco", "94101"), ("Sacramento", "95814"),
           ("Fresno", "93721"), ("San Diego", "92101")],
    "TX": [("Austin", "73301"), ("San Antonio", "78205"), ("Dallas", "75201"),
           ("Fort Worth", "76102"), ("Houston", "77002")],
    "FL": [("Fort Lauderdale", "33301"), ("Tampa", "33602"), ("Miami", "33101"),
           ("Orlando", "32801"), ("Jacksonville", "32202")],
    "NY": [("Buffalo", "14202"), ("Syracuse", "13202"), ("Rochester", "14614"),
           ("New York City", "10001"), ("Albany", "12207")],
    "IL": [("Chicago", "60601"), ("Joliet", "60432"), ("Rockford", "61101"),
           ("Aurora", "60506"), ("Naperville", "60540")],
}

BLOOD_TYPES = ["A+", "A-", "B+", "B-", "AB+", "AB-", "O+", "O-"]

DOCUMENT_TYPES = [
    ("SSN", "Social Security Number (SSN)"),
    ("DRIVERS_LICENSE", "Driver's License"),
    ("STATE_ID", "State ID Card"),
    ("US_PASSPORT", "U.S. Passport"),
    ("FOREIGN_PASSPORT", "Foreign Passport"),
    ("GREEN_CARD", "Permanent Resident Card (Green Card)"),
    ("MILITARY_ID", "U.S. Military ID"),
    ("TRIBAL_ID", "Tribal Identification Card"),
    ("BIRTH_CERTIFICATE", "Birth Certificate"),
    ("OTHER", "Other"),
]

ETHNICITIES = [
    ("HISPANIC_OR_LATINO", "Hispanic or Latino"),
    ("WHITE", "White"),
    ("BLACK_OR_AFRICAN_AMERICAN", "Black or African American"),
    ("ASIAN", "Asian"),
    ("AMERICAN_INDIAN_OR_ALASKA_NATIVE", "American Indian or Alaska Native"),
    ("NATIVE_HAWAIIAN_OR_PACIFIC_ISLANDER", "Native Hawaiian or Pacific Islander"),
    ("MIDDLE_EASTERN_OR_NORTH_AFRICAN", "Middle Eastern or North African"),
    ("MULTIRACIAL", "Multiracial"),
    ("OTHER", "Other"),
]

# Mapeo del dato legacy (valores string) -> nuevo código de etnia
ETHNICITY_BACKFILL = {
    "White/Caucasian": "WHITE",
    "Hispanic/Latino": "HISPANIC_OR_LATINO",
    "Black/African American": "BLACK_OR_AFRICAN_AMERICAN",
    "Asian": "ASIAN",
    "Native American": "AMERICAN_INDIAN_OR_ALASKA_NATIVE",
    "Pacific Islander": "NATIVE_HAWAIIAN_OR_PACIFIC_ISLANDER",
    "Two or more races": "MULTIRACIAL",
    "Other": "OTHER",
}

LIFESTYLE_NORMALIZATION = [
    ("exercise_level", "Light (1-2x/wk)", "Light"),
    ("exercise_level", "Moderate (3-4x/wk)", "Moderate"),
    ("exercise_level", "Active (5+/wk)", "Active"),
    ("hospitalization_history", "No", "None"),
    ("hospitalization_history", "Yes - Once", "Once"),
    ("hospitalization_history", "Yes - Multiple", "Multiple"),
]


def sql_quote(value: str) -> str:
    return "'" + value.replace("'", "''") + "'"


def build() -> str:
    lines = [
        "-- Seed de catálogos del módulo de pacientes.",
        "-- Idempotente: todos los INSERT usan ON CONFLICT DO NOTHING.",
        "-- Generado por scripts/generate_patient_catalogs_seed.py; no editar a mano.",
        "",
        "-- Grupos sanguíneos (código canónico).",
        "INSERT INTO app.blood_types (code, name, sort_order) VALUES",
        *[f"    ({sql_quote(code)}, {sql_quote(code)}, {i + 1})," for i, code in enumerate(BLOOD_TYPES[:-1])],
        f"    ({sql_quote(BLOOD_TYPES[-1])}, {sql_quote(BLOOD_TYPES[-1])}, {len(BLOOD_TYPES)})",
        "ON CONFLICT (code) DO NOTHING;",
        "",
        "-- Tipos de documento (contexto USA, USCIS I-9).",
        "INSERT INTO app.document_types (code, name, sort_order) VALUES",
        *[f"    ({sql_quote(code)}, {sql_quote(name)}, {i + 1})," for i, (code, name) in enumerate(DOCUMENT_TYPES[:-1])],
        f"    ({sql_quote(DOCUMENT_TYPES[-1][0])}, {sql_quote(DOCUMENT_TYPES[-1][1])}, {len(DOCUMENT_TYPES)})",
        "ON CONFLICT (code) DO NOTHING;",
        "",
        "-- Etnias (categorías OMB 2024).",
        "INSERT INTO app.ethnicities (code, name, sort_order) VALUES",
        *[f"    ({sql_quote(code)}, {sql_quote(name)}, {i + 1})," for i, (code, name) in enumerate(ETHNICITIES[:-1])],
        f"    ({sql_quote(ETHNICITIES[-1][0])}, {sql_quote(ETHNICITIES[-1][1])}, {len(ETHNICITIES)})",
        "ON CONFLICT (code) DO NOTHING;",
        "",
        "-- Países (ISO 3166-1 alpha-2 + prefijo E.164).",
        "INSERT INTO app.countries (code, name, phone_code, is_active, sort_order) VALUES",
    ]

    sorted_rest = sorted(COUNTRIES[3:], key=lambda c: c[1])
    ordered = COUNTRIES[:3] + sorted_rest
    for i, (code, name, phone) in enumerate(ordered[:-1]):
        lines.append(f"    ({sql_quote(code)}, {sql_quote(name)}, {sql_quote(phone)}, true, {i + 1}),")
    last_code, last_name, last_phone = ordered[-1]
    lines.append(f"    ({sql_quote(last_code)}, {sql_quote(last_name)}, {sql_quote(last_phone)}, true, {len(ordered)})")
    lines.extend(["ON CONFLICT (code) DO NOTHING;", ""])

    # Estados US
    lines += [
        "-- Estados y territorios de EE. UU.",
        "INSERT INTO app.states (id, country_id, code, name)",
        "SELECT gen_random_uuid(), c.id, s.code, s.name",
        "FROM (VALUES",
    ]
    for i, (code, name) in enumerate(US_STATES[:-1]):
        lines.append(f"    ({sql_quote(code)}, {sql_quote(name)}),")
    last_code, last_name = US_STATES[-1]
    lines.append(f"    ({sql_quote(last_code)}, {sql_quote(last_name)})")
    lines.extend([
        ") AS s(code, name)",
        "JOIN app.countries c ON c.code = 'US'",
        "ON CONFLICT (country_id, code) DO NOTHING;",
        "",
        "-- Ciudades: 5 genéricas por estado (presentes en el dato legacy) + reales con ZIP.",
        "INSERT INTO app.cities (id, state_id, name)",
        "SELECT gen_random_uuid(), st.id, c.name",
        "FROM (VALUES",
    ])

    city_rows = []
    for state_code, _ in US_STATES:
        for generic in GENERIC_CITIES:
            city_rows.append((state_code, generic))
        for city, _ in REAL_CITIES.get(state_code, []):
            city_rows.append((state_code, city))
    for i, (state_code, city) in enumerate(city_rows[:-1]):
        lines.append(f"    ({sql_quote(state_code)}, {sql_quote(city)}),")
    last_state, last_city = city_rows[-1]
    lines.append(f"    ({sql_quote(last_state)}, {sql_quote(last_city)})")
    lines.extend([
        ") AS c(state_code, name)",
        "JOIN app.states st ON st.code = c.state_code",
        "ON CONFLICT (state_id, name) DO NOTHING;",
        "",
        "-- Códigos postales (ZIPs primarios de las ciudades reales).",
        "INSERT INTO app.postal_codes (id, city_id, zip_code)",
        "SELECT gen_random_uuid(), ci.id, z.zip_code",
        "FROM (VALUES",
    ])

    zip_rows = []
    for state_code, cities in REAL_CITIES.items():
        for city, zip_code in cities:
            zip_rows.append((state_code, city, zip_code))
    for i, (state_code, city, zip_code) in enumerate(zip_rows[:-1]):
        lines.append(f"    ({sql_quote(state_code)}, {sql_quote(city)}, {sql_quote(zip_code)}),")
    last_zip = zip_rows[-1]
    lines.append(f"    ({sql_quote(last_zip[0])}, {sql_quote(last_zip[1])}, {sql_quote(last_zip[2])})")
    lines.extend([
        ") AS z(state_code, city, zip_code)",
        "JOIN app.states st ON st.code = z.state_code",
        "JOIN app.cities ci ON ci.state_id = st.id AND ci.name = z.city",
        "ON CONFLICT (city_id, zip_code) DO NOTHING;",
        "",
    ])

    # Backfill del dato legacy
    lines += [
        "-- Backfill: etnia legacy (string) -> FK al catálogo.",
        "UPDATE app.patient_profiles p",
        "SET ethnicity_id = e.id",
        "FROM app.ethnicities e",
        "WHERE p.ethnicity IS NOT NULL",
        "  AND e.code = CASE p.ethnicity",
    ]
    legacy_codes = list(ETHNICITY_BACKFILL.items())
    for i, (legacy, code) in enumerate(legacy_codes):
        suffix = "" if i < len(legacy_codes) - 1 else " ELSE NULL"
        lines.append(f"    WHEN {sql_quote(legacy)} THEN {sql_quote(code)}{suffix}")
    lines.extend([
        "  END;",
        "",
        "-- Backfill: grupo sanguíneo (el código legacy coincide 1:1 con el catálogo).",
        "UPDATE app.patient_profiles p",
        "SET blood_type_id = b.id",
        "FROM app.blood_types b",
        "WHERE p.blood_type IS NOT NULL AND p.blood_type = b.code;",
        "",
        "-- Backfill: país/estado/ciudad (estados legacy en código ISO-2; las 5",
        "-- ciudades genéricas existen en todos los estados, las reales solo en el suyo).",
        "UPDATE app.patient_profiles p",
        "SET country_id = c.id, state_id = s.id, city_id = ci.id",
        "FROM app.countries c",
        "JOIN app.states s ON s.country_id = c.id",
        "JOIN app.cities ci ON ci.state_id = s.id",
        "WHERE c.code = 'US'",
        "  AND s.code = p.state",
        "  AND ci.name = p.city",
        "  AND p.city IS NOT NULL",
        "  AND p.state IS NOT NULL;",
        "",
        "-- Normalización de vocabularios legacy de estilo de vida.",
    ])
    for column, legacy, canonical in LIFESTYLE_NORMALIZATION:
        lines.append(f"UPDATE app.patient_profiles SET {column} = {sql_quote(canonical)} WHERE {column} = {sql_quote(legacy)};")
    lines.append("")

    return "\n".join(lines)


def main() -> None:
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(build(), encoding="utf-8")
    print(f"Seed escrito en {OUT} ({len(build())} bytes)")


if __name__ == "__main__":
    main()