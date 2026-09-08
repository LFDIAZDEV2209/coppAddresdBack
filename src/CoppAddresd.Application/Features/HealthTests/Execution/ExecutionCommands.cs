using CoppAddresd.Application.Features.HealthTests.Events;
using CoppAddresd.Application.Features.HealthTests.Scoring;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using MediatR;

namespace CoppAddresd.Application.Features.HealthTests.Execution;

// --- Start Evaluation (asignación → InProgress + evaluación Started) ---

public record StartEvaluationCommand(Guid AssignmentId) : IRequest<HealthTestEvaluationDto>;

public sealed class StartEvaluationCommandHandler(IHealthTestRepository repository)
    : IRequestHandler<StartEvaluationCommand, HealthTestEvaluationDto>
{
    public async Task<HealthTestEvaluationDto> Handle(
        StartEvaluationCommand request,
        CancellationToken ct
    )
    {
        var assignment = await repository.GetAssignmentByIdAsync(request.AssignmentId, ct);
        if (assignment is null)
        {
            throw new InvalidOperationException("La asignación no existe.");
        }

        if (assignment.Status == HealthTestAssignmentStatus.completed)
        {
            throw new InvalidOperationException("La asignación ya está completada.");
        }

        if (assignment.Status == HealthTestAssignmentStatus.cancelled)
        {
            throw new InvalidOperationException("La asignación está cancelada.");
        }

        // Reutiliza la evaluación Started existente si la hay.
        var existingEvaluationId = await repository.GetEvaluationByAssignmentAsync(
            assignment.Id,
            ct
        );
        if (existingEvaluationId is { } existingId)
        {
            var existing = await repository.GetEvaluationByIdAsync(existingId, ct);
            if (existing is not null && existing.Status == HealthTestEvaluationStatus.started)
            {
                return HealthTestEvaluationDto.FromEntity(existing);
            }
        }

        var evaluation = new HealthTestEvaluation
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignment.Id,
            PatientId = assignment.PatientId,
            VersionId = assignment.VersionId,
            Status = HealthTestEvaluationStatus.started,
            StartedAt = DateTime.UtcNow,
        };
        await repository.AddEvaluationAsync(evaluation, ct);

        assignment.Status = HealthTestAssignmentStatus.in_progress;
        assignment.StartedAt = evaluation.StartedAt;
        await repository.UpdateAssignmentAsync(assignment, ct);

        return HealthTestEvaluationDto.FromEntity(evaluation);
    }
}

// --- Submit Evaluation (evaluación + respuestas + scoring + indicadores + alertas en transacción) ---

public record SubmitAnswerInput(Guid QuestionId, Guid? AnswerOptionId, string? ValueText);

public record SubmitEvaluationRequest(IReadOnlyList<SubmitAnswerInput> Answers);

public record SubmitEvaluationCommand(Guid AssignmentId, SubmitEvaluationRequest Request)
    : IRequest<HealthTestEvaluationDto>;

public sealed class SubmitEvaluationCommandHandler(
    IHealthTestRepository repository,
    ScoreStrategyRegistry scoringRegistry,
    ScoreRangeEngine rangeEngine,
    IndicatorEngine indicatorEngine,
    AlertEngine alertEngine,
    IHealthTestMetricsQueue? metricsQueue = null
) : IRequestHandler<SubmitEvaluationCommand, HealthTestEvaluationDto>
{
    public async Task<HealthTestEvaluationDto> Handle(
        SubmitEvaluationCommand request,
        CancellationToken ct
    )
    {
        var result = await repository.ExecuteInTransactionAsync(
            async () =>
            {
                var assignment = await repository.GetAssignmentByIdAsync(request.AssignmentId, ct);
                if (assignment is null)
                {
                    throw new InvalidOperationException("La asignación no existe.");
                }

                if (assignment.Status == HealthTestAssignmentStatus.completed)
                {
                    throw new InvalidOperationException("La asignación ya está completada.");
                }

                // La evaluación vigente de la asignación (Started), o se crea una.
                var evaluation = await repository.GetEvaluationWithDetailsAsync(
                    (await FindEvaluationByAssignmentAsync(assignment.Id, ct)) ?? Guid.Empty,
                    ct
                );
                if (evaluation is null)
                {
                    evaluation = new HealthTestEvaluation
                    {
                        Id = Guid.NewGuid(),
                        AssignmentId = assignment.Id,
                        PatientId = assignment.PatientId,
                        VersionId = assignment.VersionId,
                        Status = HealthTestEvaluationStatus.started,
                        StartedAt = DateTime.UtcNow,
                    };
                    await repository.AddEvaluationAsync(evaluation, ct);
                }

                // Respuestas: se reemplazan (una pregunta → una respuesta vigente).
                var existingResponses = await repository.ListResponsesByEvaluationAsync(
                    evaluation.Id,
                    ct
                );
                // Respuestas: se reemplazan (una pregunta → una respuesta vigente).
                if (existingResponses.Count > 0)
                {
                    await repository.DeleteResponsesByEvaluationAsync(evaluation.Id, ct);
                }

                var newResponses = request
                    .Request.Answers.Where(a => a.QuestionId != Guid.Empty)
                    .Select(a => new HealthTestResponse
                    {
                        Id = Guid.NewGuid(),
                        EvaluationId = evaluation.Id,
                        QuestionId = a.QuestionId,
                        AnswerOptionId = a.AnswerOptionId,
                        ValueText = string.IsNullOrWhiteSpace(a.ValueText)
                            ? null
                            : a.ValueText.Trim(),
                        CreatedAt = DateTime.UtcNow,
                    })
                    .ToList();
                await repository.AddResponsesRangeAsync(newResponses, ct);

                // Scoring según la estrategia de la versión.
                var version =
                    await repository.GetVersionWithDetailsAsync(assignment.VersionId, ct)
                    ?? throw new InvalidOperationException("La versión del test no existe.");

                var scoreQuestions = version
                    .Questions.OrderBy(q => q.SortOrder)
                    .Select(q => new ScoreQuestion(
                        q.Id,
                        q.Code,
                        q.Section,
                        q.Type,
                        q.ScoringDirection,
                        q.Options.OrderBy(o => o.SortOrder)
                            .Select(o => new ScoreOption(o.Id, o.ScoreValue))
                            .ToList()
                    ))
                    .ToList();
                var scoreAnswers = newResponses
                    .Select(r => new ScoreAnswer(r.QuestionId, r.AnswerOptionId, r.ValueText))
                    .ToList();

                var strategy = scoringRegistry.Resolve(version.ScoringStrategy);
                var output = strategy.Calculate(scoreQuestions, scoreAnswers);

                // Resultados: total + subescalas + clasificación por rangos.
                var results = new List<HealthTestResult>();
                var totalClassification = rangeEngine.Classify(
                    output.Score,
                    version.ScoreRanges.ToList()
                );
                results.Add(
                    new HealthTestResult
                    {
                        Id = Guid.NewGuid(),
                        EvaluationId = evaluation.Id,
                        ResultType = HealthTestResultType.score,
                        // El código del score es el código del INSTRUMENTO (ej: "orp")
                        // para que las reglas de alerta lo referencien naturalmente
                        // ({"when":{"code":"orp",...}}).
                        Code = version.Instrument?.Code ?? "score_total",
                        Label = version.Instrument?.Name ?? "Score total",
                        Value = output.Score,
                        Qualifier = totalClassification.Label,
                        Severity = totalClassification.Severity,
                        CreatedAt = DateTime.UtcNow,
                    }
                );

                foreach (var subscale in output.Subscales)
                {
                    var classification = rangeEngine.Classify(
                        subscale.Value,
                        version.ScoreRanges.ToList()
                    );
                    results.Add(
                        new HealthTestResult
                        {
                            Id = Guid.NewGuid(),
                            EvaluationId = evaluation.Id,
                            ResultType = HealthTestResultType.subscale,
                            Code = subscale.Code,
                            Label = subscale.Code,
                            Value = subscale.Value,
                            Qualifier = classification.Label,
                            Severity = classification.Severity,
                            CreatedAt = DateTime.UtcNow,
                        }
                    );
                }

                // Indicadores derivados.
                var indicatorDefs = await repository.ListActiveIndicatorDefsAsync(ct);
                foreach (var def in indicatorDefs)
                {
                    var computation = indicatorEngine.Parse(def.Computation);
                    var value = indicatorEngine.Calculate(computation, results);
                    if (value.HasValue)
                    {
                        var classification = rangeEngine.Classify(
                            value.Value,
                            version.ScoreRanges.ToList()
                        );
                        results.Add(
                            new HealthTestResult
                            {
                                Id = Guid.NewGuid(),
                                EvaluationId = evaluation.Id,
                                ResultType = HealthTestResultType.indicator,
                                Code = def.Code,
                                Label = def.Name,
                                Value = value.Value,
                                Qualifier = classification.Label,
                                Severity = classification.Severity,
                                CreatedAt = DateTime.UtcNow,
                            }
                        );
                    }
                }

                await repository.AddResultsRangeAsync(results, ct);

                // Alertas (sin duplicados por regla+resultado).
                var rules = await repository.ListActiveAlertRulesAsync(ct);
                var drafts = alertEngine.Evaluate(assignment.PatientId, results, rules);
                foreach (var draft in drafts)
                {
                    var exists = await repository.AlertExistsForResultRuleAsync(
                        draft.ResultId,
                        draft.RuleId,
                        ct
                    );
                    if (!exists)
                    {
                        await repository.AddAlertAsync(
                            new HealthTestAlert
                            {
                                Id = Guid.NewGuid(),
                                PatientId = draft.PatientId,
                                ResultId = draft.ResultId,
                                RuleId = draft.RuleId,
                                Severity = draft.Severity,
                                Title = draft.Title,
                                Body = draft.Body,
                                Status = HealthTestAlertStatus.active,
                                CreatedAt = DateTime.UtcNow,
                            },
                            ct
                        );
                    }
                }

                // Cierra la evaluación y la asignación.
                evaluation.Status = HealthTestEvaluationStatus.completed;
                evaluation.CompletedAt = DateTime.UtcNow;
                evaluation.Score = output.Score;
                evaluation.ScorePercentage =
                    output.MaxScore > 0
                        ? Math.Round(output.Score / output.MaxScore * 100m, 2)
                        : null;
                await repository.UpdateEvaluationAsync(evaluation, ct);

                assignment.Status = HealthTestAssignmentStatus.completed;
                assignment.CompletedAt = evaluation.CompletedAt;
                await repository.UpdateAssignmentAsync(assignment, ct);

                // Si la asignación pertenece a una batería, se marca completed si
                // todas sus asignaciones hijas están completadas.
                await UpdateBatteryAssignmentStatusAsync(assignment, ct);

                // Pre-agregación CQRS en background (0ms overhead)
                if (metricsQueue != null)
                {
                    await metricsQueue.EnqueueAsync(new HealthTestCompletedMetricEvent(
                        evaluation.Id,
                        assignment.PatientId,
                        null,
                        DateOnly.FromDateTime(evaluation.CompletedAt ?? DateTime.UtcNow),
                        version.Instrument?.Code,
                        totalClassification.Severity,
                        drafts.Count
                    ));
                }

                return HealthTestEvaluationDto.FromEntity(
                    (await repository.GetEvaluationWithDetailsAsync(evaluation.Id, ct))!
                );
            },
            ct
        );

        return result;
    }

    private async Task<Guid?> FindEvaluationByAssignmentAsync(
        Guid assignmentId,
        CancellationToken ct
    ) => await repository.GetEvaluationByAssignmentAsync(assignmentId, ct);

    private async Task UpdateBatteryAssignmentStatusAsync(
        HealthTestAssignment assignment,
        CancellationToken ct
    )
    {
        if (assignment.BatteryAssignmentId is not { } batteryAssignmentId)
        {
            return;
        }

        var siblings = await repository.ListAssignmentsByBatteryAssignmentAsync(
            batteryAssignmentId,
            ct
        );
        if (
            siblings.Count > 0
            && siblings.All(s => s.Status == HealthTestAssignmentStatus.completed)
        )
        {
            var batteryAssignment = await repository.GetBatteryAssignmentByIdAsync(
                batteryAssignmentId,
                ct
            );
            if (batteryAssignment is not null)
            {
                batteryAssignment.Status = HealthTestAssignmentStatus.completed;
                batteryAssignment.CompletedAt = DateTime.UtcNow;
                await repository.UpdateBatteryAssignmentAsync(batteryAssignment, ct);
            }
        }
    }
}

// --- Abandon Evaluation ---

public record AbandonEvaluationCommand(Guid AssignmentId) : IRequest<HealthTestEvaluationDto?>;

public sealed class AbandonEvaluationCommandHandler(IHealthTestRepository repository)
    : IRequestHandler<AbandonEvaluationCommand, HealthTestEvaluationDto?>
{
    public async Task<HealthTestEvaluationDto?> Handle(
        AbandonEvaluationCommand request,
        CancellationToken ct
    )
    {
        var assignment = await repository.GetAssignmentByIdAsync(request.AssignmentId, ct);
        if (assignment is null)
        {
            return null;
        }

        var evaluationId = await repository.GetEvaluationByAssignmentAsync(assignment.Id, ct);
        var evaluation = evaluationId is null
            ? null
            : await repository.GetEvaluationByIdAsync(evaluationId.Value, ct);
        if (evaluation is null)
        {
            return null;
        }

        evaluation.Status = HealthTestEvaluationStatus.abandoned;
        await repository.UpdateEvaluationAsync(evaluation, ct);

        assignment.Status = HealthTestAssignmentStatus.pending;
        assignment.StartedAt = null;
        await repository.UpdateAssignmentAsync(assignment, ct);

        return HealthTestEvaluationDto.FromEntity(evaluation);
    }
}

// --- Consultas de resultados por paciente e indicadores poblacionales ---

public record ListEvaluationsByPatientQuery(
    Guid PatientId,
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    DateTime? From = null,
    DateTime? To = null,
    string? Category = null
) : IRequest<PaginatedHealthTestsResult<HealthTestEvaluationDto>>;

public sealed class ListEvaluationsByPatientQueryHandler(IHealthTestRepository repository)
    : IRequestHandler<
        ListEvaluationsByPatientQuery,
        PaginatedHealthTestsResult<HealthTestEvaluationDto>
    >
{
    public async Task<PaginatedHealthTestsResult<HealthTestEvaluationDto>> Handle(
        ListEvaluationsByPatientQuery request,
        CancellationToken ct
    )
    {
        var (items, total) = await repository.ListEvaluationsByPatientPageAsync(
            request.PatientId,
            request.Status,
            request.From,
            request.To,
            request.Category,
            request.Page,
            request.PageSize,
            ct
        );
        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        return new PaginatedHealthTestsResult<HealthTestEvaluationDto>(
            items.Select(HealthTestEvaluationDto.FromEntity).ToList(),
            total,
            Math.Max(1, request.Page),
            pageSize,
            (int)Math.Ceiling(total / (double)pageSize)
        );
    }
}

public record ListResultsByPatientQuery(Guid PatientId)
    : IRequest<IReadOnlyList<HealthTestResultDto>>;

public sealed class ListResultsByPatientQueryHandler(IHealthTestRepository repository)
    : IRequestHandler<ListResultsByPatientQuery, IReadOnlyList<HealthTestResultDto>>
{
    public async Task<IReadOnlyList<HealthTestResultDto>> Handle(
        ListResultsByPatientQuery request,
        CancellationToken ct
    )
    {
        var results = await repository.ListResultsByPatientAsync(request.PatientId, ct);
        return results.Select(HealthTestResultDto.FromEntity).ToList();
    }
}

// --- Detalle de evaluación y comentarios (hub del paciente / ERP) ---

public record GetEvaluationDetailQuery(Guid PatientId, Guid EvaluationId)
    : IRequest<HealthTestEvaluationDetailDto?>;

public sealed class GetEvaluationDetailQueryHandler(IHealthTestRepository repository)
    : IRequestHandler<GetEvaluationDetailQuery, HealthTestEvaluationDetailDto?>
{
    public async Task<HealthTestEvaluationDetailDto?> Handle(
        GetEvaluationDetailQuery request,
        CancellationToken ct
    )
    {
        var evaluation = await repository.GetEvaluationWithDetailsAsync(request.EvaluationId, ct);
        if (evaluation is null || evaluation.PatientId != request.PatientId)
        {
            return null;
        }

        var instrumentId = evaluation.Version?.InstrumentId;
        var attempts = instrumentId is null
            ? []
            : (
                await repository.ListEvaluationsByInstrumentAsync(
                    request.PatientId,
                    instrumentId.Value,
                    ct
                )
            )
                .Select(HealthTestAttemptDto.FromEntity)
                .ToList();

        var attemptNumber = attempts.Select(a => a.Id).ToList().IndexOf(evaluation.Id) + 1;

        var comments = await repository.ListCommentsByEvaluationAsync(evaluation.Id, ct);

        return HealthTestEvaluationDetailDto.FromEntity(
            evaluation,
            attemptNumber,
            comments,
            attempts
        );
    }
}

public record GetEvaluationCommentsQuery(Guid PatientId, Guid EvaluationId)
    : IRequest<IReadOnlyList<HealthTestCommentDto>>;

public sealed class GetEvaluationCommentsQueryHandler(IHealthTestRepository repository)
    : IRequestHandler<GetEvaluationCommentsQuery, IReadOnlyList<HealthTestCommentDto>>
{
    public async Task<IReadOnlyList<HealthTestCommentDto>> Handle(
        GetEvaluationCommentsQuery request,
        CancellationToken ct
    )
    {
        var evaluation = await repository.GetEvaluationByIdAsync(request.EvaluationId, ct);
        if (evaluation is null || evaluation.PatientId != request.PatientId)
        {
            return [];
        }

        return (await repository.ListCommentsByEvaluationAsync(request.EvaluationId, ct))
            .Select(HealthTestCommentDto.FromEntity)
            .ToList();
    }
}
