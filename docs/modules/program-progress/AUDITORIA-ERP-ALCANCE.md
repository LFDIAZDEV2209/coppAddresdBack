# Auditoría ERP — Fase 0

## Alcance por paciente

Inventario 0.11a, 13-11-2026. Base: `a105ba7c89c8d604e0946b6f12a779d27e871f04`;
rama `org/f0-coppAddresdBack`. La clasificación es completa para las familias existentes;
«pendiente» significa aplicar/verificar el guard en 0.11c/0.11d, no ruta sin clasificar.
Las filas «adyacente» documentan APIs que alimentan pestañas clínicas sin ampliar el change
a dashboards de Programa/Adherencia, Comunidad, citas ni laboratorio.

Método: CodeGraph `files` (44 controladores), `search` de `PatientId`, `CycleId`,
`AttachmentId`, `FoodAnalysisId`, `ActorScopedToEnrollmentAsync`, y `callers` de
`CanAccessAsync` y `CanAccessPatientAsync`. Se excluyeron los duplicados `.worktrees/`.
Lectura dirigida del código fuente para atributos HTTP y cuerpos que CodeGraph devolvió
solo como outline o truncados. Referencias: `ProgramRepository`, `NutritionCaseStore`,
`NutritionInboxRepository`, `NutritionPlanWorkflow`, `ProgramActorContext`.

Regla objetivo: permiso de operación **Y** asignación activa; solo rol `Admin` exceptúa
la asignación. `Patients.View`, `Nutrition.Manage`, `ClinicAdmin` y `OrganizationAdmin`
no son excepciones. Recurso inexistente o ajeno: mismo 404. Las listas filtran ANTES de
contar/paginar; no basta filtrar la respuesta. Masivos: conservar ids enviados y conteo
de omitidos sin revelar nombres. Self-service sigue ligado a identidad del JWT.

Hallazgos de base: `ProgramActorContext` permite bypass OrganizationAdmin/ClinicAdmin
y self-service; Nutrición/Bienestar construyen `ViewAll` desde `Patients.View`.
`NutritionReadAccess` exige Manage/Approve y todavía carece de lectura separada.
`Wellness.Manage` mezcla editar rutinas y nutrición antigua: no puede otorgarse al
nutricionista sin separar la autorización de esas operaciones.


### Programa / Perfil 360 / inscripciones

Entidad y resolución: Inscripción → PatientId; entrada patientId directa en overview/controls/scores; recursos derivados → inscripción/paciente.

Fichero: `src/CoppAddresd.Api/Controllers/ProgramController.cs`.

| Ruta | Acción / handler | Estado |
| --- | --- | --- |
| `GET /api/v1/program/me/snapshot` | `ProgramController.GetMySnapshot` (125) → `GetSnapshotQuery` | hecho: identidad JWT (self-service) |
| `GET /api/v1/program/enrollments/{id:guid}/snapshot` | `ProgramController.GetEnrollmentSnapshot` (150) → `GetSnapshotQuery` | hecho 0.11c: guard y/o filtro previo a materializar |
| `POST /api/v1/program/tasks/complete` | `ProgramController.CompleteTask` (167) → `CompleteTaskCommand` | hecho: identidad JWT (self-service) |
| `GET /api/v1/program/calendar` | `ProgramController.GetCalendar` (212) → `GetCalendarQuery` | hecho: identidad JWT (self-service) |
| `GET /api/v1/program/path` | `ProgramController.GetPath` (239) → `GetPathQuery` | hecho: identidad JWT (self-service) |
| `POST /api/v1/program/enrollments/me` | `ProgramController.EnrollSelf` (264) → `EnrollPatientCommand` | hecho: identidad JWT (self-service) |
| `POST /api/v1/program/enrollments` | `ProgramController.EnrollPatient` (300) → `EnrollPatientCommand` | hecho 0.11c: guard y/o filtro previo a materializar |
| `POST /api/v1/program/enrollments/{id:guid}/pause` | `ProgramController.PauseEnrollment` (330) → `PauseEnrollmentCommand` | hecho 0.11c: guard y/o filtro previo a materializar |
| `POST /api/v1/program/enrollments/{id:guid}/resume` | `ProgramController.ResumeEnrollment` (353) → `ResumeEnrollmentCommand` | hecho 0.11c: guard y/o filtro previo a materializar |
| `POST /api/v1/program/enrollments/{id:guid}/withdraw` | `ProgramController.WithdrawEnrollment` (370) → `WithdrawEnrollmentCommand` | hecho 0.11c: guard y/o filtro previo a materializar |
| `GET /api/v1/program/enrollments` | `ProgramController.ListEnrollments` (396) → `ListEnrollmentsQuery` | hecho 0.11c: guard y/o filtro previo a materializar |
| `POST /api/v1/program/enrollments/bulk` | `ProgramController.BulkEnroll` (447) → `BulkEnrollPatientsCommand` | hecho 0.11c: guard y/o filtro previo a materializar |
| `GET /api/v1/program/enrollments/export` | `ProgramController.ExportEnrollments` (480) → `ExportEnrollmentsQuery` | hecho 0.11c: guard y/o filtro previo a materializar |
| `GET /api/v1/program/enrollments/{id:guid}` | `ProgramController.GetEnrollment` (540) → `GetEnrollmentQuery` | hecho 0.11c: guard y/o filtro previo a materializar |
| `GET /api/v1/program/enrollments/{id:guid}/xp-ledger` | `ProgramController.GetXpLedger` (558) → `GetXpLedgerQuery` | hecho 0.11c: guard y/o filtro previo a materializar |
| `GET /api/v1/program/enrollments/{id:guid}/baselines` | `ProgramController.GetBaselines` (581) → `GetBaselinesQuery` | hecho 0.11c: guard y/o filtro previo a materializar |
| `POST /api/v1/program/enrollments/{id:guid}/baselines` | `ProgramController.CreateBaseline` (604) → `CreateBaselineCommand` | hecho 0.11c: guard y/o filtro previo a materializar |
| `GET /api/v1/program/enrollments/{id:guid}/content` | `ProgramController.GetProgramContent` (643) → `GetProgramContentQuery` | hecho 0.11c: guard y/o filtro previo a materializar |
| `PUT /api/v1/program/enrollments/{id:guid}/content/week/{weekNumber:int}` | `ProgramController.SetWeekContent` (670) → `SetWeekContentCommand` | hecho 0.11c: guard y/o filtro previo a materializar |
| `PUT /api/v1/program/enrollments/{id:guid}/content/range` | `ProgramController.SetWeekContentRange` (704) → `SetWeekContentRangeCommand` | hecho 0.11c: guard y/o filtro previo a materializar |
| `POST /api/v1/program/content/bulk-assign` | `ProgramController.BulkAssignProgramContent` (736) | hecho 0.11c: guard y/o filtro previo a materializar |
| `GET /api/v1/program/enrollments/{id:guid}/week/{weekNumber:int}` | `ProgramController.GetEnrollmentWeek` (758) → `GetEnrollmentWeekQuery` | hecho 0.11c: guard y/o filtro previo a materializar |
| `PUT /api/v1/program/enrollments/{id:guid}/week/{weekNumber:int}/tasks` | `ProgramController.ReplaceEnrollmentWeekTasks` (794) → `ReplaceEnrollmentWeekTasksCommand` | hecho 0.11c: guard y/o filtro previo a materializar |
| `GET /api/v1/program/templates` | `ProgramController.ListTemplates` (812) → `ListTemplatesQuery` | hecho: catálogo global, sin propietario paciente |
| `GET /api/v1/program/templates/{id:guid}` | `ProgramController.GetTemplate` (823) → `GetTemplateQuery` | hecho: catálogo global, sin propietario paciente |
| `POST /api/v1/program/templates` | `ProgramController.CreateTemplate` (831) → `CreateTemplateCommand` | hecho: catálogo global, sin propietario paciente |
| `PUT /api/v1/program/templates/{id:guid}` | `ProgramController.UpdateTemplate` (855) → `UpdateTemplateCommand` | hecho: catálogo global, sin propietario paciente |
| `POST /api/v1/program/templates/{id:guid}/publish` | `ProgramController.PublishTemplate` (879) → `PublishTemplateCommand` | hecho: catálogo global, sin propietario paciente |
| `POST /api/v1/program/templates/{id:guid}/archive` | `ProgramController.ArchiveTemplate` (887) → `ArchiveTemplateCommand` | hecho: catálogo global, sin propietario paciente |
| `GET /api/v1/program/templates/{id:guid}/weekday-tasks` | `ProgramController.GetWeekdayTasks` (898) → `GetTemplateQuery` | hecho: catálogo global, sin propietario paciente |
| `PUT /api/v1/program/templates/{id:guid}/weekday-tasks` | `ProgramController.ReplaceWeekdayTasks` (910) → `ReplaceWeekdayTasksCommand` | hecho: catálogo global, sin propietario paciente |
| `GET /api/v1/program/adaptations` | `ProgramController.ListAdaptations` (922) → `ListAdaptationsQuery` | hecho 0.11c: guard y/o filtro previo a materializar |
| `GET /api/v1/program/adaptations/{id:guid}` | `ProgramController.GetAdaptation` (934) → `GetAdaptationQuery` | hecho 0.11c: guard y/o filtro previo a materializar |
| `POST /api/v1/program/adaptations/{id:guid}/decide` | `ProgramController.DecideAdaptation` (942) → `DecideAdaptationCommand` | hecho 0.11c: guard y/o filtro previo a materializar |
| `GET /api/v1/program/scores` | `ProgramController.GetScores` (972) → `GetScoresQuery` | hecho: identidad JWT (self-service) |
| `GET /api/v1/program/me/scores-history` | `ProgramController.GetScoresHistory` (1003) → `GetScoresHistoryQuery` | hecho: identidad JWT (self-service) |
| `PUT /api/v1/program/me/league-preferences` | `ProgramController.UpdateLeaguePreferences` (1045) → `UpdateLeaguePreferencesCommand` | hecho: identidad JWT (self-service) |
| `GET /api/v1/program/me/league` | `ProgramController.GetLeague` (1085) → `GetLeagueQuery` | hecho: identidad JWT (self-service) |
| `POST /api/v1/program/me/weight` | `ProgramController.RecordWeight` (1112) → `RecordWeightCommand` | hecho: identidad JWT (self-service) |
| `POST /api/v1/program/me/device-metrics` | `ProgramController.RecordDeviceMetrics` (1151) → `RecordDeviceMetricsCommand` | hecho: identidad JWT (self-service) |
| `GET /api/v1/program/me/metrics-history` | `ProgramController.GetMetricsHistory` (1185) → `GetMetricsHistoryQuery` | hecho: identidad JWT (self-service) |
| `POST /api/v1/program/scores/calculate` | `ProgramController.CalculateScores` (1225) → `CalculateScoresCommand` | hecho 0.11c: guard y/o filtro previo a materializar |
| `GET /api/v1/program/xp-rules` | `ProgramController.ListXpRules` (1256) → `ListXpRulesQuery` | hecho: catálogo global, sin propietario paciente |
| `PUT /api/v1/program/xp-rules/{code}` | `ProgramController.UpdateXpRule` (1269) → `UpdateXpRuleCommand` | hecho: catálogo global, sin propietario paciente |
| `GET /api/v1/program/xp-rules/clinical-pending` | `ProgramController.ListClinicalReviews` (1302) → `ListClinicalReviewsQuery` | hecho 0.11c: guard y/o filtro previo a materializar |
| `POST /api/v1/program/xp-rules/clinical-pending/{id:guid}/decide` | `ProgramController.DecideClinicalReview` (1319) → `DecideClinicalReviewCommand` | hecho 0.11c: guard y/o filtro previo a materializar |
| `POST /api/v1/program/nutrition/log` | `ProgramController.LogNutrition` (1358) → `LogNutritionCommand` | hecho: identidad JWT (self-service) |
| `PUT /api/v1/program/nutrition/log/{mealCode}` | `ProgramController.UpdateNutritionLog` (1405) → `UpdateNutritionIntakeCommand` | hecho: identidad JWT (self-service) |
| `GET /api/v1/program/notifications` | `ProgramController.ListNotifications` (1457) | hecho: identidad JWT (self-service) |
| `GET /api/v1/program/me/notifications` | `ProgramController.ListNotifications` (1458) → `ListNotificationsQuery` | hecho: identidad JWT (self-service) |
| `POST /api/v1/program/notifications/{id:guid}/read` | `ProgramController.MarkNotificationRead` (1483) → `MarkNotificationReadCommand` | hecho: identidad JWT (self-service) |
| `GET /api/v1/program/weaknesses` | `ProgramController.ListWeaknesses` (1512) → `ListWeaknessesQuery` | hecho: identidad JWT (self-service) |
| `GET /api/v1/program/weaknesses/open` | `ProgramController.ListOpenWeaknesses` (1541) → `ListOpenWeaknessesQuery` | hecho 0.11c: guard y/o filtro previo a materializar |
| `POST /api/v1/program/weaknesses/{id:guid}/status` | `ProgramController.UpdateWeaknessStatus` (1557) → `UpdateWeaknessStatusCommand` | hecho 0.11c: guard y/o filtro previo a materializar |
| `GET /api/v1/program/interventions` | `ProgramController.ListInterventions` (1585) → `ListInterventionsQuery` | hecho: identidad JWT (self-service) |
| `POST /api/v1/program/interventions/{id:guid}/accept` | `ProgramController.AcceptIntervention` (1613) → `AcceptInterventionCommand` | hecho: identidad JWT (self-service) |
| `GET /api/v1/program/interventions/open` | `ProgramController.ListOpenInterventions` (1637) → `ListOpenInterventionsQuery` | hecho 0.11c: guard y/o filtro previo a materializar |
| `POST /api/v1/program/interventions/{id:guid}/status` | `ProgramController.UpdateInterventionStatus` (1651) → `UpdateInterventionStatusCommand` | hecho 0.11c: guard y/o filtro previo a materializar |
| `POST /api/v1/program/interventions/{id:guid}/tele-scheduled` | `ProgramController.MarkTeleScheduled` (1677) → `MarkTeleScheduledCommand` | hecho 0.11c: guard y/o filtro previo a materializar |
| `POST /api/v1/program/interventions/{id:guid}/tele-attended` | `ProgramController.MarkTeleAttended` (1689) → `MarkTeleAttendedCommand` | hecho 0.11c: guard y/o filtro previo a materializar |
| `POST /api/v1/program/interventions/{id:guid}/tele-comply` | `ProgramController.MarkTeleComply` (1706) → `MarkTeleComplyCommand` | hecho 0.11c: guard y/o filtro previo a materializar |
| `GET /api/v1/program/erp/dashboard` | `ProgramController.GetErpDashboard` (1719) → `GetErpDashboardQuery` | adyacente: dashboard/mantenimiento fuera del change; sin otorgar Program.View a lectores |
| `GET /api/v1/program/erp/biometria/community` | `ProgramController.GetBiometriaCommunity` (1730) → `GetBiometriaCommunityQuery` | adyacente: dashboard/mantenimiento fuera del change; sin otorgar Program.View a lectores |
| `GET /api/v1/program/erp/biometria/patients` | `ProgramController.ListBiometriaPatients` (1740) → `ListBiometriaPatientsQuery` | adyacente: dashboard/mantenimiento fuera del change; sin otorgar Program.View a lectores |
| `GET /api/v1/program/erp/biometria/patients/{id:guid}` | `ProgramController.GetBiometriaPatient` (1789) → `GetBiometriaPatientQuery` | adyacente: dashboard/mantenimiento fuera del change; sin otorgar Program.View a lectores |
| `GET /api/v1/program/erp/biometria/export/csv` | `ProgramController.ExportBiometriaCsv` (1807) → `StreamBiometriaPatientsExportQuery` | adyacente: dashboard/mantenimiento fuera del change; sin otorgar Program.View a lectores |
| `GET /api/v1/program/erp/today` | `ProgramController.GetErpToday` (1855) → `GetErpTodayQuery` | adyacente: dashboard/mantenimiento fuera del change; sin otorgar Program.View a lectores |
| `GET /api/v1/program/erp/adherencia` | `ProgramController.GetErpAdherencia` (1864) → `GetErpAdherenciaQuery` | adyacente: dashboard/mantenimiento fuera del change; sin otorgar Program.View a lectores |
| `GET /api/v1/program/erp/cofres` | `ProgramController.GetErpCofres` (1885) → `GetErpCofresQuery` | adyacente: dashboard/mantenimiento fuera del change; sin otorgar Program.View a lectores |
| `GET /api/v1/program/erp/patients/{patientId:guid}/overview` | `ProgramController.GetPatientOverview` (1901) → `GetPatientOverviewQuery` | hecho 0.11c: guard y/o filtro previo a materializar |
| `GET /api/v1/program/erp/patients/{patientId:guid}/controls` | `ProgramController.GetPatientControls` (1927) → `GetPatientControlsQuery` | hecho 0.11c: guard y/o filtro previo a materializar |
| `POST /api/v1/program/maintenance/reconcile-streaks` | `ProgramController.ReconcileStreaks` (1958) → `ReconcileStreaksCommand` | adyacente: dashboard/mantenimiento fuera del change; sin otorgar Program.View a lectores |
| `GET /api/v1/program/activity-log` | `ProgramController.GetActivityLog` (1974) → `ListProgramActivityLogQuery` | adyacente: dashboard/mantenimiento fuera del change; sin otorgar Program.View a lectores |


### Bienestar antiguo

Entidad y resolución: Asignación → PatientId; plan/rutina personalizados → PatientId; validar también origen/destino al clonar/asignar.

Fichero: `src/CoppAddresd.Api/Controllers/WellnessController.cs`.

| Ruta | Acción / handler | Estado |
| --- | --- | --- |
| `GET /api/v1/wellness/nutrition-plans` | `WellnessController.ListNutritionPlans` (24) → `ListNutritionPlansQuery` | hecho 0.11c: guard y/o filtro previo a materializar |
| `GET /api/v1/wellness/nutrition-plans/{id:guid}` | `WellnessController.GetNutritionPlan` (42) → `GetNutritionPlanQuery` | hecho 0.11c: guard y/o filtro previo a materializar |
| `POST /api/v1/wellness/nutrition-plans` | `WellnessController.CreateNutritionPlan` (51) → `CreateNutritionPlanCommand` | hecho 0.11c: guard y/o filtro previo a materializar |
| `PUT /api/v1/wellness/nutrition-plans/{id:guid}` | `WellnessController.UpdateNutritionPlan` (62) → `GetNutritionPlanQuery, UpdateNutritionPlanCommand` | hecho 0.11c: guard y/o filtro previo a materializar |
| `DELETE /api/v1/wellness/nutrition-plans/{id:guid}` | `WellnessController.DeleteNutritionPlan` (74) → `GetNutritionPlanQuery, DeleteNutritionPlanCommand` | hecho 0.11c: guard y/o filtro previo a materializar |
| `POST /api/v1/wellness/nutrition-plans/{sourcePlanId:guid}/clone` | `WellnessController.CloneNutritionPlan` (85) → `GetNutritionPlanQuery, CloneNutritionPlanCommand` | hecho 0.11c: guard y/o filtro previo a materializar |
| `GET /api/v1/wellness/exercise-routines` | `WellnessController.ListExerciseRoutines` (102) → `ListExerciseRoutinesQuery` | hecho 0.11c: catálogo global; creación con paciente exige guard |
| `GET /api/v1/wellness/exercise-routines/{id:guid}` | `WellnessController.GetExerciseRoutine` (113) → `GetExerciseRoutineQuery` | hecho 0.11c: catálogo global; creación con paciente exige guard |
| `POST /api/v1/wellness/exercise-routines` | `WellnessController.CreateExerciseRoutine` (121) → `CreateExerciseRoutineCommand` | hecho 0.11c: catálogo global; creación con paciente exige guard |
| `PUT /api/v1/wellness/exercise-routines/{id:guid}` | `WellnessController.UpdateExerciseRoutine` (131) → `UpdateExerciseRoutineCommand` | hecho 0.11c: catálogo global; creación con paciente exige guard |
| `DELETE /api/v1/wellness/exercise-routines/{id:guid}` | `WellnessController.DeleteExerciseRoutine` (140) → `DeleteExerciseRoutineCommand` | hecho 0.11c: catálogo global; creación con paciente exige guard |
| `GET /api/v1/wellness/routine-assignments` | `WellnessController.ListRoutineAssignments` (150) → `ListRoutineAssignmentsQuery` | hecho 0.11c: guard y/o filtro previo a materializar |
| `GET /api/v1/wellness/routine-assignments/{id:guid}` | `WellnessController.GetRoutineAssignment` (167) → `GetRoutineAssignmentQuery` | hecho 0.11c: guard y/o filtro previo a materializar |
| `GET /api/v1/wellness/patients/{patientId:guid}/routine-assignments` | `WellnessController.ListAssignmentsByPatient` (176) → `ListAssignmentsByPatientQuery` | hecho 0.11c: guard y/o filtro previo a materializar |
| `POST /api/v1/wellness/routine-assignments` | `WellnessController.CreateRoutineAssignment` (185) → `CreateRoutineAssignmentCommand` | hecho 0.11c: guard y/o filtro previo a materializar |
| `PUT /api/v1/wellness/routine-assignments/{id:guid}` | `WellnessController.UpdateRoutineAssignment` (195) → `GetRoutineAssignmentQuery, UpdateRoutineAssignmentCommand` | hecho 0.11c: guard y/o filtro previo a materializar |
| `DELETE /api/v1/wellness/routine-assignments/{id:guid}` | `WellnessController.DeleteRoutineAssignment` (206) → `GetRoutineAssignmentQuery, DeleteRoutineAssignmentCommand` | hecho 0.11c: guard y/o filtro previo a materializar |
| `GET /api/v1/wellness/nutrition-plan-assignments` | `WellnessController.ListNutritionPlanAssignments` (218) → `GetNutritionPlanQuery, ListNutritionPlanAssignmentsQuery` | hecho 0.11c: guard y/o filtro previo a materializar |
| `GET /api/v1/wellness/nutrition-plan-assignments/{id:guid}` | `WellnessController.GetNutritionPlanAssignment` (241) → `GetNutritionPlanAssignmentQuery` | hecho 0.11c: guard y/o filtro previo a materializar |
| `GET /api/v1/wellness/patients/{patientId:guid}/nutrition-plan-assignments` | `WellnessController.ListPlanAssignmentsByPatient` (250) → `ListPlanAssignmentsByPatientQuery` | hecho 0.11c: guard y/o filtro previo a materializar |
| `POST /api/v1/wellness/nutrition-plan-assignments` | `WellnessController.CreateNutritionPlanAssignment` (259) → `CreateNutritionPlanAssignmentCommand` | hecho 0.11c: guard y/o filtro previo a materializar |
| `PUT /api/v1/wellness/nutrition-plan-assignments/{id:guid}` | `WellnessController.UpdateNutritionPlanAssignment` (269) → `GetNutritionPlanAssignmentQuery, UpdateNutritionPlanAssignmentCommand` | hecho 0.11c: guard y/o filtro previo a materializar |
| `DELETE /api/v1/wellness/nutrition-plan-assignments/{id:guid}` | `WellnessController.DeleteNutritionPlanAssignment` (280) → `GetNutritionPlanAssignmentQuery, DeleteNutritionPlanAssignmentCommand` | hecho 0.11c: guard y/o filtro previo a materializar |
| `POST /api/v1/wellness/plans/generate` | `WellnessController.GeneratePlan` (297) → `GeneratePlanCommand` | hecho 0.11c: guard y/o filtro previo a materializar |


### Nutrición / prescripciones

Entidad y resolución: patientId de ruta Y propietario de prescriptionId; CycleId pertenece a la prescripción.

Fichero: `src/CoppAddresd.Api/Controllers/NutritionController.cs`.

| Ruta | Acción / handler | Estado |
| --- | --- | --- |
| `GET /api/v1/nutrition/patients/{patientId:guid}/prescriptions` | `NutritionController.List` (17) → `ListNutritionCasesQuery` | hecho 0.11d: guard, alcance autenticado y/o proyección sensible |
| `GET /api/v1/nutrition/patients/{patientId:guid}/prescriptions/{id:guid}` | `NutritionController.Get` (20) → `GetNutritionCaseQuery` | hecho 0.11d: guard, alcance autenticado y/o proyección sensible |
| `GET /api/v1/nutrition/patients/{patientId:guid}/prescriptions/{id:guid}/history` | `NutritionController.History` (23) → `NutritionHistoryQuery` | hecho 0.11d: guard, alcance autenticado y/o proyección sensible |
| `POST /api/v1/nutrition/patients/{patientId:guid}/prescriptions` | `NutritionController.Generate` (26) → `NutritionCaseCommand` | hecho 0.11d: guard, alcance autenticado y/o proyección sensible |
| `POST /api/v1/nutrition/patients/{patientId:guid}/prescriptions/{id:guid}/{verb}` | `NutritionController.Act` (33) → `NutritionCaseCommand` | hecho 0.11d: guard, alcance autenticado y/o proyección sensible |


### Nutrición / listas e IA

Entidad y resolución: patientId de ruta; bandeja filtra por asignación antes de contar y paginar.

Fichero: `src/CoppAddresd.Api/Controllers/NutritionReadController.cs`.

| Ruta | Acción / handler | Estado |
| --- | --- | --- |
| `GET /api/v1/nutrition/inbox` | `NutritionReadController.Inbox` (18) → `NutritionInboxQuery` | hecho 0.11d: guard, alcance autenticado y/o proyección sensible |
| `GET /api/v1/nutrition/foods` | `NutritionReadController.Foods` (25) → `NutritionFoodCatalogQuery` | hecho: catálogo global; requiere permiso lectura nutrición |
| `POST /api/v1/nutrition/patients/{patientId:guid}/draft-generation` | `NutritionReadController.DraftGeneration` (31) → `NutritionDraftGenerationCommand` | hecho 0.11d: guard, alcance autenticado y/o proyección sensible |
| `GET /api/v1/nutrition/patients/{patientId:guid}/workspace` | `NutritionReadController.Workspace` (46) → `NutritionWorkspaceQuery` | hecho 0.11d: guard, alcance autenticado y/o proyección sensible |


### Nutrición / revisión antigua

Entidad y resolución: Plan → PatientId; cada plan del lote se evalúa por separado.

Fichero: `src/CoppAddresd.Api/Controllers/NutritionPlanReviewController.cs`.

| Ruta | Acción / handler | Estado |
| --- | --- | --- |
| `GET /api/v1/me/nutrition-plan/status` | `NutritionPlanReviewController.Status` (15) | hecho: identidad JWT (self-service) |
| `POST /api/v1/wellness/nutrition-plans/{id:guid}/approve` | `NutritionPlanReviewController.Approve` (22) | hecho 0.11d: guard, alcance autenticado y/o proyección sensible |
| `POST /api/v1/wellness/nutrition-plans/approve` | `NutritionPlanReviewController.ApproveBatch` (27) | hecho 0.11d: guard, alcance autenticado y/o proyección sensible |
| `GET /api/v1/wellness/nutrition-plans/{id:guid}/history` | `NutritionPlanReviewController.History` (32) | hecho 0.11d: guard, alcance autenticado y/o proyección sensible |


### Análisis de alimentos

Entidad y resolución: AnalysisId → foodai.food_analyses.UserId → patient_profiles.UserId; feedback conserva ownership del paciente.

Fichero: `src/CoppAddresd.Api/Controllers/FoodAiController.cs`.

| Ruta | Acción / handler | Estado |
| --- | --- | --- |
| `GET /api/v1/foodai/health` | `FoodAiController.Health` (57) | hecho: ingesta propia/catálogo/probe, sin lectura clínica ajena |
| `POST /api/v1/foodai/analyze` | `FoodAiController.Analyze` (76) → `AnalyzeFoodImageCommand` | hecho: ingesta propia/catálogo/probe, sin lectura clínica ajena |
| `GET /api/v1/foodai/nutrition/{foodKey}` | `FoodAiController.Nutrition` (189) → `GetFoodNutritionQuery` | hecho: ingesta propia/catálogo/probe, sin lectura clínica ajena |
| `GET /api/v1/foodai/analyses/{analysisId:guid}` | `FoodAiController.GetAnalysis` (212) | hecho 0.11d: guard, alcance autenticado y/o proyección sensible |
| `POST /api/v1/foodai/analyses/{analysisId:guid}/feedback` | `FoodAiController.PostFeedback` (240) | hecho: ownership JWT |


### Fotos / objetos firmados

Entidad y resolución: ImageKey → food_analyses.UserId → patient_profiles; documentos archivados → Document.PatientId; autorizar ANTES de firmar.

Fichero: `src/CoppAddresd.Api/Controllers/StorageController.cs`.

| Ruta | Acción / handler | Estado |
| --- | --- | --- |
| `PUT /api/v1/storage/{**key}` | `StorageController.Put` (29) | hecho 0.11d: guard, alcance autenticado y/o proyección sensible |
| `GET /api/v1/storage/sign` | `StorageController.Sign` (60) | hecho 0.11d: guard, alcance autenticado y/o proyección sensible |
| `GET /api/v1/storage/{**key}` | `StorageController.Get` (103) | hecho 0.11d: guard, alcance autenticado y/o proyección sensible |


### Controles / inscripciones

Entidad y resolución: EnrollmentId → program_enrollments.PatientId; listados/conteos filtran en consulta.

Fichero: `src/CoppAddresd.Api/Controllers/ProgramControlsController.cs`.

| Ruta | Acción / handler | Estado |
| --- | --- | --- |
| `POST /api/v1/program-controls/run` | `ProgramControlsController.Run` (44) | hecho 0.11c: guard y/o filtro previo a materializar |
| `POST /api/v1/program-controls/force` | `ProgramControlsController.ForceSend` (65) | hecho 0.11c: guard y/o filtro previo a materializar |
| `GET /api/v1/program-controls/list` | `ProgramControlsController.List` (104) | hecho 0.11c: guard y/o filtro previo a materializar |
| `DELETE /api/v1/program-controls/list` | `ProgramControlsController.Delete` (124) | hecho 0.11c: guard y/o filtro previo a materializar |
| `GET /api/v1/program-controls/templates` | `ProgramControlsController.GetTemplates` (141) | hecho: catálogo global |


### Pacientes / listas del selector

Entidad y resolución: Id → patient_profiles; asignaciones activas de patient_professionals; filtro previo a total/página.

Fichero: `src/CoppAddresd.Api/Controllers/PatientsController.cs`.

| Ruta | Acción / handler | Estado |
| --- | --- | --- |
| `GET /api/v1/patients/stats` | `PatientsController.Stats` (20) → `GetPatientsStatsQuery` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/patients/dashboard` | `PatientsController.Dashboard` (40) → `GetPatientsDashboardQuery` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/patients/clinical-board` | `PatientsController.ClinicalBoard` (69) → `GetClinicalBoardQuery` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/patients` | `PatientsController.List` (107) → `ListPatientsQuery` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/patients/scheduling` | `PatientsController.ListForScheduling` (153) → `ListPatientsQuery` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/patients/{id:guid}` | `PatientsController.GetById` (221) → `GetPatientQuery` | adyacente: control existente; verificar consumidor |
| `POST /api/v1/patients` | `PatientsController.Create` (239) → `CreatePatientCommand, AutoAssignInitialBatteryCommand` | adyacente: control existente; verificar consumidor |
| `POST /api/v1/patients/bulk` | `PatientsController.Bulk` (305) → `BulkCreatePatientsCommand, AutoAssignInitialBatteryCommand` | adyacente: control existente; verificar consumidor |
| `PUT /api/v1/patients/{id:guid}` | `PatientsController.Update` (341) → `UpdatePatientCommand` | adyacente: control existente; verificar consumidor |
| `PATCH /api/v1/patients/{id:guid}/status` | `PatientsController.UpdateStatus` (406) → `UpdatePatientStatusCommand` | adyacente: control existente; verificar consumidor |
| `DELETE /api/v1/patients/{id:guid}` | `PatientsController.Delete` (430) → `DeletePatientCommand` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/patients/{id:guid}/professionals` | `PatientsController.Assignments` (450) → `ListPatientAssignmentsQuery` | adyacente: control existente; verificar consumidor |
| `POST /api/v1/patients/{id:guid}/professionals` | `PatientsController.Assign` (468) → `AssignPatientProfessionalCommand` | adyacente: control existente; verificar consumidor |
| `DELETE /api/v1/patients/{id:guid}/professionals/{professionalId:guid}` | `PatientsController.Remove` (502) → `RemovePatientProfessionalCommand` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/patients/{id:guid}/measurements` | `PatientsController.Measurements` (527) → `ListPatientMeasurementsQuery, GetPatientQuery, PatientIsAssignedQuery` | adyacente: control existente; verificar consumidor |


### Tests usados por Perfil 360

Entidad y resolución: Paciente directo; evaluación/resultado/asignación/alerta → PatientId; verificar relación del recurso con paciente.

Fichero: `src/CoppAddresd.Api/Controllers/HealthTestsController.cs`.

| Ruta | Acción / handler | Estado |
| --- | --- | --- |
| `GET /api/v1/health-tests` | `HealthTestsController.ListInstruments` (39) → `ListInstrumentsQuery` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/health-tests/{id:guid}` | `HealthTestsController.GetInstrument` (58) → `GetInstrumentQuery` | adyacente: control existente; verificar consumidor |
| `POST /api/v1/health-tests` | `HealthTestsController.CreateInstrument` (71) → `CreateInstrumentCommand` | adyacente: control existente; verificar consumidor |
| `PUT /api/v1/health-tests/{id:guid}` | `HealthTestsController.UpdateInstrument` (78) → `UpdateInstrumentCommand` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/health-tests/{id:guid}/versions` | `HealthTestsController.ListVersions` (94) → `ListVersionsQuery` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/health-tests/versions/{versionId:guid}` | `HealthTestsController.GetVersion` (101) → `GetVersionQuery` | adyacente: control existente; verificar consumidor |
| `POST /api/v1/health-tests/{id:guid}/versions` | `HealthTestsController.CreateVersion` (112) → `CreateVersionCommand` | adyacente: control existente; verificar consumidor |
| `POST /api/v1/health-tests/versions/{versionId:guid}/publish` | `HealthTestsController.PublishVersion` (120) → `PublishVersionCommand` | adyacente: control existente; verificar consumidor |
| `POST /api/v1/health-tests/versions/{versionId:guid}/retire` | `HealthTestsController.RetireVersion` (131) → `RetireVersionCommand` | adyacente: control existente; verificar consumidor |
| `POST /api/v1/health-tests/versions/{versionId:guid}/clone` | `HealthTestsController.CloneVersion` (142) → `CloneVersionCommand` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/health-tests/alert-rules` | `HealthTestsController.ListAlertRules` (155) → `ListAlertRulesQuery` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/health-tests/versions/{versionId:guid}/questions` | `HealthTestsController.ListQuestions` (160) → `ListQuestionsQuery` | adyacente: control existente; verificar consumidor |
| `PUT /api/v1/health-tests/versions/{versionId:guid}/questions` | `HealthTestsController.SaveDraftQuestion` (167) → `SaveDraftQuestionCommand` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/health-tests/batteries` | `HealthTestsController.ListBatteries` (174) → `ListBatteriesQuery` | adyacente: control existente; verificar consumidor |
| `POST /api/v1/health-tests/batteries` | `HealthTestsController.CreateBattery` (184) → `CreateBatteryCommand` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/health-tests/assignments` | `HealthTestsController.ListAssignments` (193) → `ListPendingAssignmentsForProfessionalQuery, ListAssignmentsQuery` | adyacente: control existente; verificar consumidor |
| `POST /api/v1/health-tests/assignments` | `HealthTestsController.AssignTest` (239) → `AssignTestCommand` | adyacente: control existente; verificar consumidor |
| `POST /api/v1/health-tests/batteries/{batteryId:guid}/assign` | `HealthTestsController.AssignBattery` (263) → `AssignBatteryCommand` | adyacente: control existente; verificar consumidor |
| `POST /api/v1/health-tests/assignments/{id:guid}/cancel` | `HealthTestsController.CancelAssignment` (282) → `CancelAssignmentCommand` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/health-tests/master` | `HealthTestsController.GetMasterRows` (305) → `GetMasterRowsQuery` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/health-tests/patients/{patientId:guid}/evaluations` | `HealthTestsController.ListPatientEvaluations` (323) → `ListEvaluationsByPatientQuery` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/health-tests/patients/{patientId:guid}/evaluations/{evaluationId:guid}` | `HealthTestsController.GetPatientEvaluation` (367) → `GetEvaluationDetailQuery` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/health-tests/patients/{patientId:guid}/evaluations/{evaluationId:guid}/comments` | `HealthTestsController.ListEvaluationComments` (392) → `GetEvaluationCommentsQuery` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/health-tests/patients/{patientId:guid}/results` | `HealthTestsController.ListPatientResults` (416) → `ListResultsByPatientQuery` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/health-tests/reminders` | `HealthTestsController.ListReminders` (439) → `GetHealthTestRemindersQuery` | adyacente: control existente; verificar consumidor |
| `POST /api/v1/health-tests/patients/{patientId:guid}/reminders` | `HealthTestsController.SendReminder` (447) → `SendHealthTestReminderCommand` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/health-tests/indicators` | `HealthTestsController.ListIndicators` (460) | adyacente: control existente; verificar consumidor |
| `GET /api/v1/health-tests/alerts` | `HealthTestsController.ListAlerts` (468) → `ListAlertsForProfessionalQuery, ListAlertsQuery` | adyacente: control existente; verificar consumidor |
| `POST /api/v1/health-tests/alerts/{id:guid}/review` | `HealthTestsController.ReviewAlert` (513) → `TransitionAlertCommand` | adyacente: control existente; verificar consumidor |
| `POST /api/v1/health-tests/alerts/{id:guid}/resolve` | `HealthTestsController.ResolveAlert` (524) → `TransitionAlertCommand` | adyacente: control existente; verificar consumidor |
| `POST /api/v1/health-tests/alerts/{id:guid}/close` | `HealthTestsController.CloseAlert` (535) → `TransitionAlertCommand` | adyacente: control existente; verificar consumidor |
| `POST /api/v1/health-tests/alerts/{id:guid}/reopen` | `HealthTestsController.ReopenAlert` (546) → `TransitionAlertCommand` | adyacente: control existente; verificar consumidor |
| `POST /api/v1/health-tests/comments` | `HealthTestsController.AddComment` (557) → `AddCommentCommand` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/health-tests/notification-templates` | `HealthTestsController.ListNotificationTemplates` (567) → `ListNotificationTemplatesQuery` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/health-tests/notification-templates/{id:guid}` | `HealthTestsController.GetNotificationTemplate` (594) → `GetNotificationTemplateQuery` | adyacente: control existente; verificar consumidor |
| `POST /api/v1/health-tests/notification-templates` | `HealthTestsController.CreateNotificationTemplate` (606) → `CreateNotificationTemplateCommand` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/health-tests/notification-templates/{id:guid}/preview` | `HealthTestsController.PreviewNotificationTemplate` (623) → `PreviewNotificationTemplateQuery` | adyacente: control existente; verificar consumidor |
| `PUT /api/v1/health-tests/notification-templates/{id:guid}` | `HealthTestsController.UpdateNotificationTemplate` (658) → `UpdateNotificationTemplateCommand` | adyacente: control existente; verificar consumidor |
| `DELETE /api/v1/health-tests/notification-templates/{id:guid}` | `HealthTestsController.DeleteNotificationTemplate` (676) → `SetNotificationTemplateActiveCommand` | adyacente: control existente; verificar consumidor |
| `POST /api/v1/health-tests/notification-templates/{id:guid}/activate` | `HealthTestsController.SetNotificationTemplateActive` (691) | adyacente: control existente; verificar consumidor |
| `POST /api/v1/health-tests/notification-templates/{id:guid}/deactivate` | `HealthTestsController.SetNotificationTemplateActive` (692) → `SetNotificationTemplateActiveCommand` | adyacente: control existente; verificar consumidor |
| `POST /api/v1/health-tests/notification-templates/{id:guid}/clone` | `HealthTestsController.CloneNotificationTemplate` (708) → `CloneNotificationTemplateCommand` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/health-tests/notification-templates/{id:guid}/versions` | `HealthTestsController.ListNotificationTemplateVersions` (726) → `ListNotificationTemplateVersionsQuery` | adyacente: control existente; verificar consumidor |
| `POST /api/v1/health-tests/notification-templates/{id:guid}/versions/{version:int}/restore` | `HealthTestsController.RestoreNotificationTemplateVersion` (734) → `RestoreNotificationTemplateVersionCommand` | adyacente: control existente; verificar consumidor |
| `POST /api/v1/health-tests/notification-templates/{id:guid}/test` | `HealthTestsController.SendTestNotification` (750) → `SendTestNotificationCommand` | adyacente: control existente; verificar consumidor |
| `POST /api/v1/health-tests/alerts/notify` | `HealthTestsController.NotifyAlerts` (770) → `NotifyAlertsCommand` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/health-tests/notifications` | `HealthTestsController.ListNotifications` (787) → `ListNotificationsQuery` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/health-tests/notifications/charts` | `HealthTestsController.GetNotificationCharts` (844) → `GetNotificationChartsQuery` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/health-tests/geo` | `HealthTestsController.GetGeo` (863) → `GetHealthTestsGeoQuery` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/health-tests/coverage-trend` | `HealthTestsController.GetCoverageTrend` (882) → `GetHealthTestCoverageTrendQuery` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/health-tests/stats` | `HealthTestsController.GetStats` (893) → `GetHealthTestStatsQuery` | adyacente: control existente; verificar consumidor |


### Documentos usados por Perfil 360

Entidad y resolución: Document.PatientId; alcance y permiso antes de URL de lectura.

Fichero: `src/CoppAddresd.Api/Controllers/DocumentsController.cs`.

| Ruta | Acción / handler | Estado |
| --- | --- | --- |
| `GET /api/v1/documents` | `DocumentsController.List` (29) → `ListDocumentsQuery` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/documents/catalog` | `DocumentsController.Catalog` (48) → `ListDocumentCatalogQuery` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/documents/{id:guid}` | `DocumentsController.GetById` (57) → `GetDocumentQuery` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/documents/{id:guid}/versions` | `DocumentsController.GetVersions` (70) → `GetDocumentQuery, ListDocumentVersionsQuery` | adyacente: control existente; verificar consumidor |
| `GET /api/v1/documents/{id:guid}/download` | `DocumentsController.Download` (90) → `GetDocumentQuery` | adyacente: control existente; verificar consumidor |
| `POST /api/v1/documents/upload-intent` | `DocumentsController.CreateUploadIntent` (129) → `CreateDocumentUploadIntentCommand` | adyacente: control existente; verificar consumidor |
| `POST /api/v1/documents` | `DocumentsController.Create` (152) → `GetDocumentQuery, CreateDocumentCommand` | adyacente: control existente; verificar consumidor |
| `PUT /api/v1/documents/{id:guid}` | `DocumentsController.Update` (194) → `GetDocumentQuery, UpdateDocumentCommand` | adyacente: control existente; verificar consumidor |
| `DELETE /api/v1/documents/{id:guid}` | `DocumentsController.Delete` (217) → `GetDocumentQuery, DeleteDocumentCommand, GetPatientQuery` | adyacente: control existente; verificar consumidor |


### Laboratorio usado por Perfil 360

Entidad y resolución: Lote/documento/medición → paciente; rutas me resuelven JWT.

Fichero: `src/CoppAddresd.Api/Controllers/LabExamController.cs`.

| Ruta | Acción / handler | Estado |
| --- | --- | --- |
| `POST /api/v1/lab-exams` | `LabExamController.Upload` (39) → `UploadLabExamCommand` | adyacente: control existente; verificar consumidor |


### Recursos sin endpoint propio en la base

| Entidad / lectura | Cómo llega al paciente | Estado |
| --- | --- | --- |
| `NutritionPrescription.CycleId` | Prescripción → PatientId; `NutritionCases` valida patientId e id conjuntamente | Clasificado: todavía no hay tabla `nutrition_cycles` ni rutas `cycles/{id}`; Fase 3 deberá usar guard |
| `attachmentId` de nutrición | No existe entidad ni endpoint en esta base (CodeGraph); futuro ciclo → paciente | Clasificado: Fase 3, guard antes de firma |
| `NutritionIntakeLog.FoodAnalysisId` | Registro → PatientId; AnalysisId → UserId → mismo paciente | Pendiente 0.11d: mantener validación de referencia en `LogNutritionAsync`/`UpdateNutritionIntakeAsync` |
| `foodai/*` / `FoodAnalysis.ImageKey` | Clave → análisis → UserId → perfil paciente | Pendiente 0.11d: cerrar firma genérica y lectura Bearer de fotos ajenas |
| Resumen/sugerencias IA Perfil 360 | No existe endpoint dedicado en esta base; actual `wellness/plans/generate` sí está inventariado | Clasificado: futuras rutas deberán usar guard y permiso específico |
| Consultas de huecos / impacto / copiar a semanas nuevas | No existen todavía; `content/range` y `content/bulk-assign` son los precursores existentes | Clasificado: guard por inscripción y política por paciente, tareas Fase 1 |
| Worker nutricional automático | `NutritionSystemActor`/reserva → patientId; proceso de sistema, nunca actor del cliente | Clasificado: conservar ejecución interna; no convertir actor HTTP en sistema |

No hay rutas sin clasificar en las familias de §10.3. Las filas pendientes son trabajo de
protección, no evidencia de cierre de 0.11c/0.11d. Los estados se actualizarán únicamente
con pruebas verdes; este inventario no certifica que el backend base sea seguro.

### Evidencia 0.11c

`PatientScopeRouteTests` verifica cada entrada anotada con paciente propio, ajeno, Admin y sin permiso;
`BulkEnrollmentScopeTests` verifica lote mixto sin despachar al ajeno. Los listados de Programa
y controles componen el guard antes de contar/paginar. Las comprobaciones antiguas de Bienestar
ya no convierten `Patients.View` en excepción de alcance. Los jobs globales de controles
(run/reset) quedan reservados a Admin. La generación de planes por IA comprueba el paciente.

### Evidencia 0.11d

`NutritionAuthorizationTests`: JWT/audiencia, lectura separada, scope ajeno, permiso global
sin Admin denegado. `NutritionSensitiveAccessTests`: fotos antes de firmar, TTL 300 s,
claves de imagen/máscara ocultas a coordinación, notas y snapshots internos redactados.
`WellbeingGuard_RealSqlFiltersInboxCountsLegacyListsAndEnrollments_AndResolvesForeignResources`:
PostgreSQL real, total/página de bandeja, listas antiguas e inscripciones, ciclo y análisis
ajenos, Admin. El alcance del guard prevalece sobre ViewAll heredado de un actor anterior.
La publicación automática técnica existente permanece dentro del ámbito interno reservado.
Las rutas futuras de adjuntos siguen pendientes de sus fases de implementación.

| Vía indirecta inventariada al verificar 0.11d | Resolución | Estado |
| --- | --- | --- |
| `DocumentsController.Download`, `MyDocumentsController.Download`, contenido/medios del programa con clave de foto prestada | La firma local y lectura/presign genéricos Local/S3 rechazan `foodai/` y sus alias antes de acceder al proveedor | Hecho: `FoodPhotoStorageProviderTests` |
