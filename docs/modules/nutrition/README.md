# Módulo: Nutrición — plan alimentario self-service (Fase 8)

Lectura propia del paciente para la APP móvil (`aud=app`): plan de
alimentación activo asignado desde el ERP, con días y comidas. El paciente se
deriva SIEMPRE del JWT (`ICurrentContext.UserId` →
`patient_profiles.user_id` en el handler, anti-IDOR): sin perfil → 404, nunca
datos ajenos. Sin permisos ERP (convención `me/*`).

## Endpoint

| Método y ruta                                    | Respuesta                                                                                                                |
| ------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------ |
| `GET /api/v1/me/nutrition-plan` (sin parámetros) | `200` con `MyNutritionPlanDto`; `401` sin JWT; `404` sin perfil o sin plan activo asignado (mensaje propio, no es error) |

## Resolución del plan activo

La asignación (`app.nutrition_plan_assignments`) es la fuente de verdad del
plan asignado — el comando de asignación del ERP no toca la fila del plan, por
lo que consultar `nutrition_plans` por `PatientId` directo omitiría planes
asignados. Misma regla que `ProgramContentResolver` (SPEC §6.10): de las
asignaciones con `Status == Active` y ventana vigente (hoy UTC), gana el
`StartDate` más reciente; luego se carga el plan con sus días ordenados
(`DayNumber`, `SortOrder`). Ventana vencida o plan huérfano → `null` → 404.

## Contrato `MyNutritionPlanDto`

`id, name, targetCondition?, durationDays, dailyCalorieTarget?, dailyProteinTarget?,
dailyCarbsTarget?, dailyFatTarget?, dailyFiberTarget?, dailyWaterMl, allergens?,
mealTiming?, status ("Active"), days[]`. Cada día: `dayNumber, dailyWaterMl,
meals[]`. Cada comida: `mealType` (nombre del enum de dominio:
`Desayuno`/`Almuerzo`/`Cena`/`Snack`, mismo contrato que el ERP — los códigos
`des/alm/mer/cen` pertenecen al log de ingesta, no al plan), `description?,
foods?, calories?, proteinG?, carbsG?, fatG?, fiberG?, waterMl?, notes?,
sortOrder`. `dailyWaterMl` del plan = el del primer día (convención: todas las
filas del día llevan el mismo valor; default 2000 de la columna).

### Ejemplo de respuesta (200)

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "Plan control glucémico",
  "targetCondition": "Diabetes",
  "durationDays": 7,
  "dailyCalorieTarget": 1800,
  "dailyProteinTarget": 90,
  "dailyCarbsTarget": 200,
  "dailyFatTarget": 60,
  "dailyFiberTarget": 25,
  "dailyWaterMl": 2500,
  "allergens": "Frutos secos",
  "mealTiming": "7:00, 12:00, 19:00",
  "status": "Active",
  "days": [
    {
      "dayNumber": 1,
      "dailyWaterMl": 2500,
      "meals": [
        {
          "mealType": "Desayuno",
          "description": "Avena con frutos rojos",
          "foods": "Avena 40g, leche descremada 200ml",
          "calories": 350,
          "proteinG": 12.5,
          "carbsG": 55.0,
          "fatG": 8.0,
          "fiberG": 6.0,
          "waterMl": 500,
          "notes": null,
          "sortOrder": 1
        }
      ]
    }
  ]
}
```

## Food AI — trazabilidad de `foodAnalysisId` (verificado, sin cambios)

`POST /api/v1/program/nutrition/log` ya persiste `Intake.FoodAnalysisId`
cuando viene informado: el controlador lo pasa verbatim al comando
(`ProgramController.cs` → `LogNutritionCommand.Intake`) y el repositorio lo
guarda en `app.nutrition_intake_logs.food_analysis_id` en el insert
(`ProgramRepository.cs`: creación y re-activación) y en el update, con chequeo
de ownership D6 contra `foodai.food_analyses` (la violación de FK es el
backstop). Sin `Intake` el shape anterior sigue intacto.

## Archivos

- `src/CoppAddresd.Application/Features/Nutrition/Queries/GetMyNutritionPlan/`
  (DTOs + query + handler)
- `GET /api/v1/me/nutrition-plan` en
  `src/CoppAddresd.Api/Controllers/MyPatientProfileController.cs`
- Tests: `tests/CoppAddresd.UnitTests/Nutrition/GetMyNutritionPlanQueryHandlerTests.cs`
