# Simulación local de narrativa de asignación

Fuente de verdad: `../ai-service/app/schemas/program_narrative.py` y su ruta
`app/api/routes/program_narrative.py`, leídas el 2026-11-04. No se buscó ni publicó
un mock remoto: el contrato canónico está en el workspace y el ensayo es local.

## Ejecución

En una terminal local:

```powershell
postman simulation run postman/simulations/program-assignment-narrative.sim.yaml --port 4047 --output ndjson --no-history
```

En otra terminal, desde el repo:

```powershell
$env:COP_PROGRAM_NARRATIVE_SIM_URL='http://127.0.0.1:4047'
dotnet test tests/CoppAddresd.IntegrationTests -c Release --filter FullyQualifiedName~ProgramNarrativeHttpIntegrationTests
Remove-Item Env:COP_PROGRAM_NARRATIVE_SIM_URL
```

Detener la simulación con Ctrl+C. El test selecciona `narrative` mediante
`x-mock-slug` y usa una clave explícitamente ficticia, `simulation-only-key`.
No se necesita API, PostgreSQL, credenciales reales ni un modelo LLM.

## Contrato y alcance

Única ruta: POST `/internal/program/assignment-narrative`. Valida objetos
profile_snapshot/proposal, lenguaje es/en y source_refs obligatorio (hasta 100
strings no vacíos, sin espacio inicial y de hasta 500 caracteres); rechaza
campos extra. La respuesta 200 es exclusivamente `{narratives:{es,en}}`.
El header de prueba `x-mock-response-code:503` simula indisponibilidad después
de validar el request. Otras rutas responden 501. Sin reenvío a hosts reales:
esta simulación usa una dirección loopback y enrutamiento por header.

El mock comprueba este subconjunto del schema, sin ejecutar Pydantic ni validar
semántica clínica, prompt o proveedor. Los textos son ficticios. No sustituye un
smoke posterior contra ai-service real. Reiniciar tras editar el handler.

## Evidencia local 2026-11-04

Cliente .NET real: 1/1 test HTTP aprobado. Observados 2 requests con respuesta
200 (fuentes presentes y array vacío) y 5 respuestas 503 durante reintentos y
apertura del circuito. ModelName null, ambos idiomas leídos desde narratives.
Unit: 1329 aprobados, 103 omitidos, 0 fallos; build Release verde. Las pruebas
unitarias cubren extracción/dedupe/límites de fuentes y rechazo del formato
plano antiguo o de una traducción ausente/vacía. No se llama al servicio real.
