using CoppAddresd.Application.Interfaces;
using MediatR;

namespace CoppAddresd.Application.Features.ProgramProgress.Queries.ExportEnrollments;

/// <summary>
/// Fila del exporte CSV de inscripciones (B14, T-30). Plano: no lleva JSON —
/// el controller la serializa a CSV con streaming (<c>IAsyncEnumerable</c>),
/// sin bufferar el resultado completo.
/// </summary>
public sealed record EnrollmentExportRow(
    Guid EnrollmentId,
    Guid PatientId,
    string? PatientName,
    string? DocumentNumber,
    string Status,
    string Timezone,
    DateOnly StartLocalDate,
    int CurrentWeekNumber,
    int TotalWeeks,
    int XpBalance,
    int StreakCurrent,
    int StreakLongest,
    int FreezesRemaining,
    DateTime CreatedAt);

/// <summary>
/// Stream del exporte CSV de inscripciones (B14, T-30). Filtros opcionales por
/// clínica del paciente y ventana de creación; <paramref name="ScopedPatientIds"/>
/// aplica el scoping del actor (T-81): lista vacía = denegar (stream sin filas).
/// </summary>
public sealed record ExportEnrollmentsQuery(
    Guid? ClinicId = null,
    DateTime? From = null,
    DateTime? To = null,
    IReadOnlyList<Guid>? ScopedPatientIds = null) : IStreamRequest<EnrollmentExportRow>;

public sealed class ExportEnrollmentsStreamHandler(
    IProgramRepository repository)
    : IStreamRequestHandler<ExportEnrollmentsQuery, EnrollmentExportRow>
{
    public IAsyncEnumerable<EnrollmentExportRow> Handle(
        ExportEnrollmentsQuery request, CancellationToken ct)
        => repository.StreamEnrollmentsForExportAsync(
            request.ClinicId, request.From, request.To, request.ScopedPatientIds, ct);
}
