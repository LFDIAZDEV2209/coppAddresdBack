# Permisos de Bienestar

La matriz canónica es `openspec/changes/organizar-experiencia-bienestar/ARQUITECTURA.md`
§10.1. `WellbeingRolePermissions` traduce sus capacidades al seed y reconcilia las
concesiones de los tres roles funcionales existentes en cada arranque de Auth.

- Director clínico: edición del programa y de nutrición, publicación, fotos y lectura
  de notas internas. La edición de notas internas corresponde al nutricionista.
- Nutricionista: `NutritionReviewer`; lectura de Perfil 360, biblioteca y rutinas,
  edición/publicación de nutrición, fotos, notas y revisión diaria.
- Coordinación: `CareCoordinator`; lectura sin fotos, notas ni IA.
- Admin: acceso completo salvo edición de notas internas (fila V de §10.1);
  única excepción al alcance por asignación.

`Program.Profile360View` permite leer el perfil y sus inscripciones sin otorgar
`Program.View` (dashboards fuera de este change). `Nutrition.View`, `RecordsView`,
`PhotosView`, `NotesView`, `NotesManage` y `ReviewManage` separan la lectura clínica
y los datos sensibles de la edición/publicación. `Program.AiView/AiManage` separan
consulta y ejecución de IA. Las capacidades aún sin endpoint preparan su autorización;
no certifican que esas funcionalidades futuras estén implementadas.

No se concede `Wellness.Manage` al nutricionista porque incluye edición de rutinas.
Las operaciones nutricionales antiguas deberán exigir `Nutrition.Manage` en 0.11d.
Las políticas y el alcance se aplican en 0.11b–0.11d; el seed por sí solo no los sustituye.
