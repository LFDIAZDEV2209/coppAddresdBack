using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;

namespace CoppAddresd.Application.Interfaces;

/// <summary>
/// Repositorio del módulo Tests de Salud. Operaciones de persistencia para
/// catálogo versionado (instrumentos, versiones, preguntas, opciones, rangos),
/// baterías, asignaciones, evaluaciones/respuestas/resultados, indicadores,
/// reglas de alerta y alertas. Sin lógica de negocio (skill repository-pattern).
/// </summary>
public interface IHealthTestRepository
{
    // --- Instrumentos ---
    Task<HealthTestInstrument?> GetInstrumentByIdAsync(Guid id, CancellationToken ct = default);
    Task<HealthTestInstrument?> GetInstrumentByCodeAsync(
        string code,
        CancellationToken ct = default
    );
    Task<(IReadOnlyList<HealthTestInstrument> Items, int Total)> ListInstrumentsAsync(
        string? search,
        string? category,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default
    );
    Task<HealthTestInstrument> AddInstrumentAsync(
        HealthTestInstrument instrument,
        CancellationToken ct = default
    );
    Task UpdateInstrumentAsync(HealthTestInstrument instrument, CancellationToken ct = default);

    // --- Versiones ---
    Task<HealthTestVersion?> GetVersionByIdAsync(Guid id, CancellationToken ct = default);
    Task<HealthTestVersion?> GetVersionWithDetailsAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<HealthTestVersion>> ListVersionsByInstrumentAsync(
        Guid instrumentId,
        CancellationToken ct = default
    );
    Task<HealthTestVersion> AddVersionAsync(
        HealthTestVersion version,
        CancellationToken ct = default
    );
    Task UpdateVersionAsync(HealthTestVersion version, CancellationToken ct = default);
    Task<int> GetNextVersionNumberAsync(Guid instrumentId, CancellationToken ct = default);

    // --- Preguntas y opciones ---
    Task<HealthTestQuestion?> GetQuestionByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<HealthTestQuestion>> ListQuestionsByVersionAsync(
        Guid versionId,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<HealthTestAnswerOption>> ListOptionsByQuestionAsync(
        Guid questionId,
        CancellationToken ct = default
    );
    Task AddQuestionsRangeAsync(
        IEnumerable<HealthTestQuestion> questions,
        CancellationToken ct = default
    );

    // --- Rangos ---
    Task<IReadOnlyList<HealthTestScoreRange>> ListRangesByVersionAsync(
        Guid versionId,
        CancellationToken ct = default
    );
    Task AddRangesRangeAsync(
        IEnumerable<HealthTestScoreRange> ranges,
        CancellationToken ct = default
    );

    // --- Baterías ---
    Task<HealthTestBattery?> GetBatteryByIdAsync(Guid id, CancellationToken ct = default);
    Task<HealthTestBattery?> GetBatteryWithItemsAsync(Guid id, CancellationToken ct = default);
    Task<HealthTestBattery?> GetAutoAssignBatteryAsync(CancellationToken ct = default);
    Task<(IReadOnlyList<HealthTestBattery> Items, int Total)> ListBatteriesAsync(
        string? search,
        bool? isActive,
        int page,
        int pageSize,
        CancellationToken ct = default
    );
    Task<HealthTestBattery> AddBatteryAsync(
        HealthTestBattery battery,
        CancellationToken ct = default
    );
    Task UpdateBatteryAsync(HealthTestBattery battery, CancellationToken ct = default);
    Task AddBatteryItemsRangeAsync(
        IEnumerable<HealthTestBatteryItem> items,
        CancellationToken ct = default
    );
    Task DeleteBatteryItemsByBatteryIdAsync(Guid batteryId, CancellationToken ct = default);

    // --- Asignaciones ---
    Task<HealthTestAssignment?> GetAssignmentByIdAsync(Guid id, CancellationToken ct = default);
    Task<HealthTestAssignment?> GetAssignmentWithDetailsAsync(
        Guid id,
        CancellationToken ct = default
    );
    Task<(IReadOnlyList<HealthTestAssignment> Items, int Total)> ListAssignmentsAsync(
        Guid? patientId,
        Guid? versionId,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default
    );
    Task<(
        IReadOnlyList<HealthTestAssignment> Items,
        int Total
    )> ListPendingAssignmentsForProfessionalAsync(
        IReadOnlyCollection<Guid> patientIds,
        string? status,
        int page,
        int pageSize,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<HealthTestAssignment>> ListAssignmentsByPatientAsync(
        Guid patientId,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<HealthTestAssignment>> ListActiveAssignmentsByPatientAsync(
        Guid patientId,
        CancellationToken ct = default
    );
    Task<HealthTestAssignment> AddAssignmentAsync(
        HealthTestAssignment assignment,
        CancellationToken ct = default
    );
    Task AddAssignmentsRangeAsync(
        IEnumerable<HealthTestAssignment> assignments,
        CancellationToken ct = default
    );
    Task UpdateAssignmentAsync(HealthTestAssignment assignment, CancellationToken ct = default);

    // --- Asignaciones de batería ---
    Task<HealthTestBatteryAssignment?> GetBatteryAssignmentByIdAsync(
        Guid id,
        CancellationToken ct = default
    );
    Task<HealthTestBatteryAssignment> AddBatteryAssignmentAsync(
        HealthTestBatteryAssignment assignment,
        CancellationToken ct = default
    );
    Task UpdateBatteryAssignmentAsync(
        HealthTestBatteryAssignment assignment,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<HealthTestBatteryAssignment>> ListBatteryAssignmentsByPatientAsync(
        Guid patientId,
        CancellationToken ct = default
    );

    // --- Evaluaciones ---
    Task<HealthTestEvaluation?> GetEvaluationByIdAsync(Guid id, CancellationToken ct = default);
    Task<HealthTestEvaluation?> GetEvaluationWithDetailsAsync(
        Guid id,
        CancellationToken ct = default
    );
    Task<Guid?> GetEvaluationByAssignmentAsync(Guid assignmentId, CancellationToken ct = default);
    Task<IReadOnlyList<HealthTestEvaluation>> ListEvaluationsByPatientAsync(
        Guid patientId,
        CancellationToken ct = default
    );
    Task<(IReadOnlyList<HealthTestEvaluation> Items, int Total)> ListEvaluationsByPatientPageAsync(
        Guid patientId,
        string? status,
        DateTime? from,
        DateTime? to,
        string? category,
        int page,
        int pageSize,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<HealthTestEvaluation>> ListEvaluationsByInstrumentAsync(
        Guid patientId,
        Guid instrumentId,
        CancellationToken ct = default
    );
    Task<HealthTestEvaluation> AddEvaluationAsync(
        HealthTestEvaluation evaluation,
        CancellationToken ct = default
    );
    Task UpdateEvaluationAsync(HealthTestEvaluation evaluation, CancellationToken ct = default);

    // --- Respuestas y resultados ---
    Task AddResponsesRangeAsync(
        IEnumerable<HealthTestResponse> responses,
        CancellationToken ct = default
    );
    Task DeleteResponsesByEvaluationAsync(Guid evaluationId, CancellationToken ct = default);
    Task<IReadOnlyList<HealthTestResponse>> ListResponsesByEvaluationAsync(
        Guid evaluationId,
        CancellationToken ct = default
    );
    Task AddResultsRangeAsync(
        IEnumerable<HealthTestResult> results,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<HealthTestResult>> ListResultsByEvaluationAsync(
        Guid evaluationId,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<HealthTestResult>> ListResultsByPatientAsync(
        Guid patientId,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<HealthTestAssignment>> ListAssignmentsByBatteryAssignmentAsync(
        Guid batteryAssignmentId,
        CancellationToken ct = default
    );

    // --- Indicadores y alertas ---
    Task<HealthTestIndicatorDef?> GetIndicatorDefByCodeAsync(
        string code,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<HealthTestIndicatorDef>> ListActiveIndicatorDefsAsync(
        CancellationToken ct = default
    );
    Task<IReadOnlyList<HealthTestAlertRule>> ListActiveAlertRulesAsync(
        CancellationToken ct = default
    );
    Task<HealthTestAlertRule?> GetAlertRuleByCodeAsync(string code, CancellationToken ct = default);
    Task<HealthTestAlert> AddAlertAsync(HealthTestAlert alert, CancellationToken ct = default);
    Task<(IReadOnlyList<HealthTestAlert> Items, int Total)> ListAlertsAsync(
        Guid? patientId,
        string? status,
        string? severity,
        int page,
        int pageSize,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<HealthTestAlert>> ListAlertsByPatientAsync(
        Guid patientId,
        CancellationToken ct = default
    );
    Task<HealthTestAlert?> GetAlertByIdAsync(Guid id, CancellationToken ct = default);
    Task UpdateAlertAsync(HealthTestAlert alert, CancellationToken ct = default);
    Task<bool> AlertExistsForResultRuleAsync(
        Guid resultId,
        Guid ruleId,
        CancellationToken ct = default
    );

    // --- Pacientes ---
    Task<IReadOnlyList<Guid>> GetPatientIdsForProfessionalAsync(
        Guid professionalId,
        CancellationToken ct = default
    );
    Task<bool> PatientBelongsToProfessionalAsync(
        Guid patientId,
        Guid professionalId,
        CancellationToken ct = default
    );
    Task<bool> PatientExistsAsync(Guid patientId, CancellationToken ct = default);
    Task<PatientProfile?> GetPatientByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<int> CountPatientsAsync(CancellationToken ct = default);
    Task<int> CountAssignmentsByStatusAsync(
        HealthTestAssignmentStatus status,
        CancellationToken ct = default
    );
    Task<int> CountAssignmentsByStatusForPatientsAsync(
        HealthTestAssignmentStatus status,
        IReadOnlyCollection<Guid> patientIds,
        CancellationToken ct = default
    );
    Task<int> CountEvaluationsBySeverityAsync(
        HealthTestSeverity severity,
        CancellationToken ct = default
    );
    Task<int> CountEvaluationsBySeverityForPatientsAsync(
        HealthTestSeverity severity,
        IReadOnlyCollection<Guid> patientIds,
        CancellationToken ct = default
    );
    Task<int> CountAlertsByStatusAsync(
        HealthTestAlertStatus status,
        CancellationToken ct = default
    );
    Task<int> CountAlertsByStatusForPatientsAsync(
        HealthTestAlertStatus status,
        IReadOnlyCollection<Guid> patientIds,
        CancellationToken ct = default
    );
    Task AddCommentAsync(HealthTestComment comment, CancellationToken ct = default);
    Task<IReadOnlyList<HealthTestComment>> ListCommentsByEvaluationAsync(
        Guid evaluationId,
        CancellationToken ct = default
    );
    Task<IReadOnlyList<HealthTestAssignment>> ListAssignmentsWithPatientDataAsync(
        Guid? professionalId,
        CancellationToken ct = default
    );
    Task<IReadOnlyDictionary<Guid, int>> ListActiveAlertCountsByPatientAsync(
        CancellationToken ct = default
    );
    Task<IReadOnlyDictionary<Guid, string>> ListProfessionalNamesByPatientAsync(
        CancellationToken ct = default
    );
    Task<IReadOnlyDictionary<Guid, string>> ListClinicNamesByIdsAsync(
        IEnumerable<Guid> clinicIds,
        CancellationToken ct = default
    );
    Task<IReadOnlyDictionary<Guid, string>> ListInsurerNamesByIdsAsync(
        IEnumerable<Guid> insurerIds,
        CancellationToken ct = default
    );

    // --- Geo / mapa ---
    Task<CoppAddresd.Application.Features.HealthTests.HealthTestsGeoDto> GetGeoAsync(
        CancellationToken ct = default
    );

    // --- Transacción multi-paso (submit) ---
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task ExecuteInTransactionAsync(Func<Task> action, CancellationToken ct = default);
    Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct = default);
}
