using CoppAddresd.Application.Features.Patients;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Enums.HealthTests;
using CoppAddresd.Infrastructure.Persistence;
using CoppAddresd.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.IntegrationTests;

/// <summary>
/// Integración del dashboard general de pacientes, el tablero clínico y el
/// toggle de estado con PostgreSQL real (COP_TEST_DB_CONNECTION). Todo el
/// fixture vive en una transacción que se revierte; si la variable no está
/// definida, los tests se saltan.
/// </summary>
public sealed class PatientDashboardIntegrationTests : IAsyncLifetime
{
    private const string EnvVar = "COP_TEST_DB_CONNECTION";

    private readonly string _connectionString = Environment.GetEnvironmentVariable(EnvVar)!;
    private bool _skipped;
    private AppDbContext _db = null!;
    private Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction _transaction = null!;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            _skipped = true;
            return;
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString)
            .Options;
        _db = new AppDbContext(options);
        _transaction = await _db.Database.BeginTransactionAsync();
    }

    public async Task DisposeAsync()
    {
        if (_skipped)
        {
            return;
        }

        await _transaction.RollbackAsync();
        await _db.DisposeAsync();
    }

    private bool IsSkipped => _skipped;

    private async Task<(Guid ClinicId, Guid OrganizationId, Guid StateId)> SeedClinicAsync()
    {
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Code = $"ORG-{Guid.NewGuid():N}"[..16],
            Name = $"Org {Guid.NewGuid():N}",
        };
        _db.Organizations.Add(organization);
        await _db.SaveChangesAsync();

        var clinic = new Clinic
        {
            Id = Guid.NewGuid(),
            Name = $"Clínica {Guid.NewGuid():N}",
            OrganizationId = organization.Id,
        };
        _db.Clinics.Add(clinic);

        var state = await _db.States.AsNoTracking().FirstAsync();
        await _db.SaveChangesAsync();

        return (clinic.Id, organization.Id, state.Id);
    }

    private PatientProfile NewPatient(
        Guid clinicId,
        string firstName,
        Guid? stateId = null,
        string? gender = null
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            FirstName = firstName,
            LastName = "Prueba",
            Status = "Activo",
            ClinicId = clinicId,
            StateId = stateId,
            Gender = gender,
            CreatedAt = DateTime.UtcNow.AddDays(-1),
        };

    [Fact]
    public async Task Dashboard_ConClinica_AgregaDemografiaEstadosYFiltroPorEstado()
    {
        if (IsSkipped)
            return;

        var (clinicId, _, stateId) = await SeedClinicAsync();
        _db.PatientProfiles.AddRange(
            NewPatient(clinicId, "Ana", stateId, "Femenino"),
            NewPatient(clinicId, "Luis", stateId, "Masculino"),
            NewPatient(clinicId, "Sara", null, "Femenino")
        );
        await _db.SaveChangesAsync();

        var repository = new PatientDashboardRepository(_db, new PatientRepository(_db));

        var dashboard = await repository.GetDashboardAsync(
            clinicId,
            null,
            null,
            12,
            DateTime.UtcNow,
            CancellationToken.None
        );

        Assert.Equal(3, dashboard.Kpis.Total);
        Assert.Contains(dashboard.GenderDistribution, g => g.Value == "Femenino" && g.Count == 2);
        var stateCode = await _db
            .States.Where(s => s.Id == stateId)
            .Select(s => s.Code)
            .FirstAsync();
        Assert.Contains(
            dashboard.States,
            s => s.Code == stateCode && s.Count == 2 && s.Percentage == Math.Round(2 * 100m / 2, 1)
        );
        Assert.Equal(12, dashboard.NewPatientsByMonth.Count);

        // Filtro por estado: solo los dos pacientes de ese estado en demografía.
        var filtered = await repository.GetDashboardAsync(
            clinicId,
            null,
            stateCode,
            6,
            DateTime.UtcNow,
            CancellationToken.None
        );

        Assert.Equal(2, filtered.GenderDistribution.Sum(g => g.Count));
        Assert.Equal(6, filtered.NewPatientsByMonth.Count);
        // La geografía permanece completa para poder cambiar de selección.
        Assert.Equal(2, filtered.States.Sum(s => s.Count));
        // Alcance propio: sin top de profesionales (redundante).
        Assert.Empty(filtered.TopProfessionals);
    }

    [Fact]
    public async Task Board_CalculaRiesgoSeguimientoYAlertasSinConsultasPorFila()
    {
        if (IsSkipped)
            return;

        var (clinicId, _, _) = await SeedClinicAsync();
        var withData = NewPatient(clinicId, "ConDatos");
        var overdue = NewPatient(clinicId, "Vencido");
        var empty = NewPatient(clinicId, "SinDatos");
        _db.PatientProfiles.AddRange(withData, overdue, empty);
        await _db.SaveChangesAsync();

        var versionId = await _db.HealthTestVersions.AsNoTracking().Select(v => v.Id).FirstAsync();

        // Paciente 1: evaluación completada con severidad alta + alerta activa.
        var assignment1 = new HealthTestAssignment
        {
            Id = Guid.NewGuid(),
            PatientId = withData.Id,
            VersionId = versionId,
            Status = HealthTestAssignmentStatus.pending,
            DueDate = DateTime.UtcNow.AddDays(3),
        };
        var assignment2 = new HealthTestAssignment
        {
            Id = Guid.NewGuid(),
            PatientId = overdue.Id,
            VersionId = versionId,
            Status = HealthTestAssignmentStatus.pending,
            DueDate = DateTime.UtcNow.AddDays(-2),
        };
        _db.HealthTestAssignments.AddRange(assignment1, assignment2);

        var evaluation = new HealthTestEvaluation
        {
            Id = Guid.NewGuid(),
            AssignmentId = assignment1.Id,
            PatientId = withData.Id,
            VersionId = versionId,
            Status = HealthTestEvaluationStatus.completed,
            CompletedAt = DateTime.UtcNow.AddDays(-1),
            Score = 80,
            ScorePercentage = 80,
        };
        _db.HealthTestEvaluations.Add(evaluation);
        _db.HealthTestResults.Add(
            new HealthTestResult
            {
                Id = Guid.NewGuid(),
                EvaluationId = evaluation.Id,
                ResultType = HealthTestResultType.score,
                Code = "score",
                Label = "Score",
                Value = 80,
                Severity = HealthTestSeverity.high,
            }
        );
        _db.HealthTestAlerts.Add(
            new HealthTestAlert
            {
                Id = Guid.NewGuid(),
                PatientId = withData.Id,
                Severity = HealthTestSeverity.high,
                Status = HealthTestAlertStatus.active,
                Title = "Alerta de prueba",
            }
        );
        await _db.SaveChangesAsync();

        var repository = new PatientDashboardRepository(_db, new PatientRepository(_db));

        var (items, total) = await repository.GetClinicalBoardAsync(
            1,
            50,
            null,
            null,
            null,
            null,
            clinicId,
            null,
            DateTime.UtcNow,
            CancellationToken.None
        );

        Assert.Equal(3, total);
        var clinical = Assert.Single(items, i => i.PatientId == withData.Id);
        Assert.Equal("high", clinical.RiskLevel);
        Assert.Equal(1, clinical.ActiveAlertCount);
        Assert.Equal("high", clinical.MaxAlertSeverity);
        Assert.NotNull(clinical.LastEvaluationAt);
        Assert.NotNull(clinical.LastEvaluationInstrument);
        Assert.Equal(ClinicalBoardFollowUp.OnTrack, clinical.FollowUpState);

        var overdueItem = Assert.Single(items, i => i.PatientId == overdue.Id);
        Assert.Equal(ClinicalBoardFollowUp.Overdue, overdueItem.FollowUpState);
        Assert.Null(overdueItem.RiskLevel);

        var emptyItem = Assert.Single(items, i => i.PatientId == empty.Id);
        Assert.Equal(ClinicalBoardFollowUp.Unassigned, emptyItem.FollowUpState);
        Assert.Null(emptyItem.RiskLevel);
        Assert.Null(emptyItem.LastEvaluationAt);

        // Filtros SQL: riesgo alto solo devuelve al paciente con evaluación high.
        var (highRisk, highTotal) = await repository.GetClinicalBoardAsync(
            1,
            50,
            null,
            ClinicalBoardFilters.RiskHigh,
            null,
            null,
            clinicId,
            null,
            DateTime.UtcNow,
            CancellationToken.None
        );
        Assert.Equal(1, highTotal);
        Assert.Equal(withData.Id, Assert.Single(highRisk).PatientId);

        // Filtro de vencidos.
        var (overdueRows, overdueTotal) = await repository.GetClinicalBoardAsync(
            1,
            50,
            null,
            null,
            null,
            ClinicalBoardFilters.FollowUpOverdue,
            clinicId,
            null,
            DateTime.UtcNow,
            CancellationToken.None
        );
        Assert.Equal(1, overdueTotal);
        Assert.Equal(overdue.Id, Assert.Single(overdueRows).PatientId);

        // Filtro de alertas activas.
        var (alertRows, alertTotal) = await repository.GetClinicalBoardAsync(
            1,
            50,
            null,
            null,
            true,
            null,
            clinicId,
            null,
            DateTime.UtcNow,
            CancellationToken.None
        );
        Assert.Equal(1, alertTotal);
        Assert.Equal(withData.Id, Assert.Single(alertRows).PatientId);
    }

    [Fact]
    public async Task Status_ActualizaSoloEstadoYConservaColeccionesHijas()
    {
        if (IsSkipped)
            return;

        var (clinicId, _, _) = await SeedClinicAsync();
        var icd10 = await _db.Icd10Codes.AsNoTracking().FirstAsync();
        var patient = NewPatient(clinicId, "Toggle");
        patient.Diagnoses.Add(
            new PatientDiagnosis
            {
                Id = Guid.NewGuid(),
                PatientId = patient.Id,
                Icd10CodeId = icd10.Id,
                IsPrimary = true,
                CreatedAt = DateTime.UtcNow,
            }
        );
        _db.PatientProfiles.Add(patient);
        await _db.SaveChangesAsync();

        var repository = new PatientRepository(_db);

        var snapshot = await repository.GetStatusSnapshotAsync(patient.Id, CancellationToken.None);
        Assert.NotNull(snapshot);
        Assert.Equal("Activo", snapshot!.Status);

        var updated = await repository.UpdateStatusAsync(
            patient.Id,
            "Inactivo",
            null,
            CancellationToken.None
        );
        Assert.True(updated);

        // El toggle no toca las colecciones hijas (nunca borra diagnósticos).
        var diagnoses = await _db
            .PatientDiagnoses.AsNoTracking()
            .CountAsync(d => d.PatientId == patient.Id);
        Assert.Equal(1, diagnoses);
        var status = await _db
            .PatientProfiles.AsNoTracking()
            .Where(p => p.Id == patient.Id)
            .Select(p => p.Status)
            .FirstAsync();
        Assert.Equal("Inactivo", status);

        Assert.Null(
            await repository.GetStatusSnapshotAsync(Guid.NewGuid(), CancellationToken.None)
        );
    }

    [Fact]
    public async Task Listado_IncluyeEstadoYDiagnosticoPrincipal()
    {
        if (IsSkipped)
            return;

        var (clinicId, _, stateId) = await SeedClinicAsync();
        var icd10 = await _db.Icd10Codes.AsNoTracking().FirstAsync();
        var patient = NewPatient(clinicId, "Directorio", stateId);
        patient.Diagnoses.Add(
            new PatientDiagnosis
            {
                Id = Guid.NewGuid(),
                PatientId = patient.Id,
                Icd10CodeId = icd10.Id,
                IsPrimary = true,
                CreatedAt = DateTime.UtcNow,
            }
        );
        _db.PatientProfiles.Add(patient);
        await _db.SaveChangesAsync();

        var repository = new PatientRepository(_db);

        var (items, total) = await repository.ListAsync(
            1,
            20,
            "Directorio",
            null,
            null,
            clinicId,
            null,
            null,
            null,
            null,
            CancellationToken.None
        );

        Assert.Equal(1, total);
        var item = Assert.Single(items);
        Assert.NotNull(item.State);
        Assert.Single(item.Diagnoses);

        var dto = PatientListItemDto.FromEntity(item, new Dictionary<Guid, string>());
        Assert.Equal(item.State!.Code, dto.StateCode);
        Assert.Equal(icd10.Code, dto.PrimaryDiagnosisCode);

        // Filtro por estado (selección del mapa): solo pacientes del estado.
        var (filtered, filteredTotal) = await repository.ListAsync(
            1,
            20,
            null,
            null,
            null,
            clinicId,
            null,
            null,
            null,
            item.State.Code,
            CancellationToken.None
        );
        Assert.Equal(1, filteredTotal);
        Assert.Equal(patient.Id, Assert.Single(filtered).Id);
    }
}
