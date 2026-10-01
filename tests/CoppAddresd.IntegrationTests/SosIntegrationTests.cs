using CoppAddresd.Application.Features.Sos;
using CoppAddresd.Application.Interfaces;
using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Enums;
using CoppAddresd.Infrastructure.Persistence;
using CoppAddresd.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CoppAddresd.IntegrationTests;

/// <summary>
/// Pruebas de integración concurrentes del módulo SOS (task 3.3, change
/// sos-panic-real) contra PostgreSQL real en una BD aislada
/// (<c>COP_TEST_DB_CONNECTION</code>; se salta si no está definida).
/// <list type="bullet">
/// <item>Colisión del índice parcial: dos activaciones concurrentes con
/// claves distintas (una 201-created, la otra ActiveCollision → 409).</item>
/// <item>Doble-tap simultáneo con la misma Idempotency-Key (una crea, la
/// otra resuelve replay sin duplicar outbox).</item>
/// <item>Carreras de transiciones terminales (attend + cancel simultáneos).</item>
/// <item>Dedupe durable: filas de outbox comprometidas con la alerta.</item>
/// </list>
/// </summary>
public sealed class SosIntegrationTests : IAsyncLifetime
{
    private const string EnvVar = "COP_TEST_DB_CONNECTION";

    private readonly string _serverConnectionString = Environment.GetEnvironmentVariable(EnvVar)!;
    private bool _skipped;
    private string _dbName = null!;
    private string _dbConnectionString = null!;
    private AppDbContext _db = null!;

    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(_serverConnectionString))
        {
            _skipped = true;
            return;
        }

        // BD aislada por corrida (mismo patrón que los tests de Telemedicine):
        // crea, aplica migraciones (historial public) y borra al terminar.
        var builder = new Npgsql.NpgsqlConnectionStringBuilder(_serverConnectionString)
        {
            Database = "postgres",
        };
        _dbName = $"coppaddresd_sos_test_{Guid.NewGuid():N}";
        var adminDb = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(builder.ConnectionString).Options
        );
        try
        {
            // TEMPLATE template0: la template1 del compose local arrastra un
            // collation version mismatch (mismo gotcha que los tests Community).
            await adminDb.Database.ExecuteSqlRawAsync(
                $"CREATE DATABASE \"{_dbName}\" TEMPLATE template0",
                CancellationToken.None
            );
        }
        finally
        {
            await adminDb.DisposeAsync();
        }

        _dbConnectionString = new Npgsql.NpgsqlConnectionStringBuilder(_serverConnectionString)
        {
            Database = _dbName,
        }.ConnectionString;

        var migrateDb = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_dbConnectionString).Options
        );
        try
        {
            // El schema `auth` y su tabla `Users` lo posee el Auth Service
            // (producción comparte la BD y Auth migra primero). En una BD
            // fresca se crean stubs mínimos para que las FK de actores
            // (REFERENCES auth."Users" ("Id")) sean válidas.
            await migrateDb.Database.ExecuteSqlRawAsync(
                """
                CREATE SCHEMA IF NOT EXISTS auth;
                CREATE TABLE IF NOT EXISTS auth."Users" (
                    "Id" uuid NOT NULL PRIMARY KEY,
                    "SecurityStamp" text NULL
                );
                """,
                CancellationToken.None
            );
            await migrateDb.Database.MigrateAsync();
        }
        finally
        {
            await migrateDb.DisposeAsync();
        }

        _db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_dbConnectionString).Options
        );
    }

    public async Task DisposeAsync()
    {
        if (_skipped)
        {
            return;
        }

        await _db.DisposeAsync();

        // Limpieza forzada: terminar conexiones y borrar la BD de prueba.
        try
        {
            var builder = new Npgsql.NpgsqlConnectionStringBuilder(_serverConnectionString)
            {
                Database = "postgres",
            };
            await using var conn = new Npgsql.NpgsqlConnection(builder.ConnectionString);
            await conn.OpenAsync();
            await using var drop = new Npgsql.NpgsqlCommand(
                $"DROP DATABASE IF EXISTS \"{_dbName}\" WITH (FORCE)",
                conn
            );
            await drop.ExecuteNonQueryAsync();
        }
        catch (NpgsqlException)
        {
            // Si el drop falla la BD queda aislada y no contamina otras corridas.
        }
    }

    private ISosAlertRepository NewRepository(AppDbContext context) =>
        new SosAlertRepository(context);

    private async Task<Guid> SeedPatientAsync(AppDbContext db, string phone)
    {
        var patient = new PatientProfile
        {
            Id = Guid.NewGuid(),
            FirstName = "Sofía",
            LastName = "Vega",
            Status = "Activo",
            EmergencyContact = System.Text.Json.JsonSerializer.Serialize(
                new
                {
                    name = "Ana",
                    relationship = "Familiar",
                    phone,
                    email = "ana@test.local",
                }
            ),
        };
        db.PatientProfiles.Add(patient);
        await db.SaveChangesAsync();
        return patient.Id;
    }

    private static SosAlert NewAlert(Guid patientId, string key, string e164) =>
        new()
        {
            Id = Guid.NewGuid(),
            PatientId = patientId,
            IdempotencyKey = key,
            PayloadHash = SosSupport.ComputePayloadHash(null, null, null, null),
            Status = SosAlertStatus.Activa,
            DestinationPhoneE164 = e164,
        };

    [Fact]
    public async Task ActivacionConcurrente_MismasClavesDistintas_UnaGanaElIndiceParcial()
    {
        // Colisión del índice parcial (task 3.3): dos activaciones del mismo
        // paciente con claves distintas en paralelo real (conexiones separadas).
        var patientId = await SeedPatientAsync(_db, "+573053924819");

        var gate = new ManualResetEventSlim(false);
        var task1 = Task.Run(async () =>
        {
            await using var db = new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_dbConnectionString).Options
            );
            var repository = NewRepository(db);
            gate.Wait();
            return await repository.AddWithOutboxAsync(
                NewAlert(patientId, Guid.NewGuid().ToString(), "+573053924819"),
                [],
                CancellationToken.None
            );
        });
        var task2 = Task.Run(async () =>
        {
            await using var db = new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_dbConnectionString).Options
            );
            var repository = NewRepository(db);
            gate.Wait();
            return await repository.AddWithOutboxAsync(
                NewAlert(patientId, Guid.NewGuid().ToString(), "+573053924819"),
                [],
                CancellationToken.None
            );
        });

        // Ambas activaciones suelten la barrera al mismo tiempo: la carrera
        // real golpea el índice parcial de PostgreSQL.
        await Task.Yield();
        gate.Set();
        var results = await Task.WhenAll(task1, task2);

        var created = results.Count(r => r.Result == SosCreateResult.Created);
        var collisions = results.Count(r => r.Result == SosCreateResult.ActiveCollision);

        // Exactamente UNA creó; la otra resolvió con la alerta existente.
        Assert.Equal(1, created);
        Assert.Equal(1, collisions);
        Assert.NotNull(
            results.First(r => r.Result == SosCreateResult.ActiveCollision).ConflictingAlert
        );

        await using var verification = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_dbConnectionString).Options
        );
        var actives = await verification
            .SosAlerts.Where(a => a.PatientId == patientId && a.Status == SosAlertStatus.Activa)
            .ToListAsync();
        Assert.Single(actives);
    }

    [Fact]
    public async Task DobleTapSimultaneo_MismaIdempotencyKey_CreaUnoYResuelveReplay()
    {
        var patientId = await SeedPatientAsync(_db, "+573053924819");
        var key = Guid.NewGuid().ToString();
        var alert = NewAlert(patientId, key, "+573053924819");
        var repository = NewRepository(_db);

        var first = await repository.AddWithOutboxAsync(
            alert,
            [$"sos:sms:{alert.Id}"],
            CancellationToken.None
        );
        Assert.Equal(SosCreateResult.Created, first.Result);

        // El segundo tap con la MISMA clave: la unicidad (patient, key) lo
        // devuelve como replay (el handler resuelve 200 con la original).
        var secondReader = NewRepository(
            new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_dbConnectionString).Options
            )
        );
        var existing = await secondReader.GetByPatientAndKeyAsync(
            patientId,
            key,
            CancellationToken.None
        );

        Assert.NotNull(existing);
        Assert.Equal(alert.Id, existing!.Id);
        var total = await _db.SosAlerts.CountAsync(a => a.PatientId == patientId);
        Assert.Equal(1, total);
    }

    [Fact]
    public async Task DobleTapSimultaneo_MismaClave_CarreralRealNoDuplicaFilas()
    {
        var patientId = await SeedPatientAsync(_db, "+573053924819");
        var key = Guid.NewGuid().ToString();

        var gate = new ManualResetEventSlim(false);
        var task1 = Task.Run(async () =>
        {
            await using var db = new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_dbConnectionString).Options
            );
            var repository = NewRepository(db);
            gate.Wait();
            return await repository.AddWithOutboxAsync(
                NewAlert(patientId, key, "+573053924819"),
                [],
                CancellationToken.None
            );
        });
        var task2 = Task.Run(async () =>
        {
            await using var db = new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_dbConnectionString).Options
            );
            var repository = NewRepository(db);
            gate.Wait();
            return await repository.AddWithOutboxAsync(
                NewAlert(patientId, key, "+573053924819"),
                [],
                CancellationToken.None
            );
        });

        await Task.Yield();
        gate.Set();
        var results = await Task.WhenAll(task1, task2);

        // Una crea, la otra colisiona por idempotencia (nada de 500).
        Assert.Contains(results, r => r.Result == SosCreateResult.Created);
        Assert.Contains(results, r => r.Result == SosCreateResult.IdempotencyCollision);

        await using var verification = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_dbConnectionString).Options
        );
        var alerts = await verification
            .SosAlerts.Where(a => a.PatientId == patientId)
            .ToListAsync();
        Assert.Single(alerts); // la misma fila para ambos (sin duplicar outbox ni SMS)
    }

    [Fact]
    public async Task AtenderYCancelarSimultaneos_UnaTerminalGanaYLaOtraQueda409()
    {
        var patientId = await SeedPatientAsync(_db, "+573053924819");
        var repository = NewRepository(_db);
        var alert = NewAlert(patientId, Guid.NewGuid().ToString(), "+573053924819");
        await repository.AddWithOutboxAsync(alert, [], CancellationToken.None);

        var gate = new ManualResetEventSlim(false);
        var cancelDb = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_dbConnectionString).Options
        );
        var attendDb = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_dbConnectionString).Options
        );

        // (task 3.3) Carrera attend + cancel con compare-and-set real: solo UNA
        // transición terminal se aplica (la otra ve 0 filas afectadas → 409).
        var attendTask = Task.Run(async () =>
        {
            var r = new SosAlertRepository(attendDb);
            gate.Wait();
            return await r.AttendAsync(alert.Id, Guid.NewGuid(), CancellationToken.None)
                ? SosTransitionOutcome.Transited
                : SosTransitionOutcome.NotActive;
        });
        var cancelTask = Task.Run(async () =>
        {
            var r = new SosAlertRepository(cancelDb);
            gate.Wait();
            return await r.CancelAsync(alert.Id, Guid.NewGuid(), CancellationToken.None)
                ? SosTransitionOutcome.Transited
                : SosTransitionOutcome.NotActive;
        });

        await Task.Yield();
        gate.Set();
        var outcomes = await Task.WhenAll(attendTask, cancelTask);

        // Estados terminales consistentes: exactamente una transición aplicada.
        var applied = outcomes.Count(o => o == SosTransitionOutcome.Transited);
        Assert.Equal(1, applied);

        await using var verification = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_dbConnectionString).Options
        );
        var final = await verification.SosAlerts.SingleAsync(a => a.Id == alert.Id);
        Assert.NotEqual(SosAlertStatus.Activa, final.Status);
        Assert.Single(new[] { final.AttendedAt, final.CancelledAt }.Where(t => t is not null));
    }

    [Fact]
    public async Task OutboxDurable_DedupeFilasComprometidasConLaAlerta()
    {
        // (task 3.3, REQ-SOS-03/04) Las filas de dedupe viven en la MISMA
        // transacción que la alerta: la creación NUNCA deja canales sin outbox.
        var patientId = await SeedPatientAsync(_db, "+573053924819");
        var repository = NewRepository(_db);
        var staffUserId = Guid.NewGuid();
        var alert = NewAlert(patientId, Guid.NewGuid().ToString(), "+573053924819");

        await repository.AddWithOutboxAsync(
            alert,
            [
                $"sos:sms:{alert.Id}",
                $"sos:voice:{alert.Id}",
                $"sos:push:{alert.Id}:{staffUserId}",
            ],
            CancellationToken.None
        );

        var smsRow = await _db.NotificationDedupeKeys.SingleAsync(k =>
            k.DedupeKey == $"sos:sms:{alert.Id}"
        );
        var voiceRow = await _db.NotificationDedupeKeys.SingleAsync(k =>
            k.DedupeKey == $"sos:voice:{alert.Id}"
        );
        var pushRow = await _db.NotificationDedupeKeys.SingleAsync(k =>
            k.DedupeKey == $"sos:push:{alert.Id}:{staffUserId}"
        );

        Assert.Equal(SosChannelStatus.Pendiente.ToString().ToLowerInvariant(), smsRow.SmsStatus);
        Assert.Equal(SosChannelStatus.Pendiente.ToString().ToLowerInvariant(), voiceRow.VoiceStatus);
        Assert.Equal(SosChannelStatus.Pendiente.ToString().ToLowerInvariant(), pushRow.PushStatus);
    }

    [Fact]
    public async Task ScopeStaff_AsignacionDirectaYContextoClinico()
    {
        // (D5) Validación real del scope sobre PostgreSQL: asignación directa
        // activa → true; clínica coincidente → true; contexto ajeno → false.
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Code = $"ORG-{Guid.NewGuid():N}"[..16],
            Name = $"Org {Guid.NewGuid():N}",
        };
        var clinic = new Clinic
        {
            Id = Guid.NewGuid(),
            Name = $"Clínica {Guid.NewGuid():N}",
            OrganizationId = organization.Id,
        };
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            FirstName = "Prof",
            MiddleName = "",
            LastName = "Prueba",
            Email = $"prof.{Guid.NewGuid():N}@test.local",
            UserId = Guid.NewGuid(),
        };

        // El FK de erp.employees.user_id apunta a auth."Users" (stub del
        // fixture): siembra el usuario antes del empleado (parametrizado —
        // nunca interpolar valores en SQL).
        await _db.Database.ExecuteSqlRawAsync(
            "INSERT INTO auth.\"Users\" (\"Id\") VALUES ({0})",
            [employee.UserId],
            CancellationToken.None
        );
        var professional = new Professional { Id = Guid.NewGuid(), EmployeeId = employee.Id };
        // Organización ajena real (FK): el scope por clínica de otra org → false.
        var otherOrganization = new Organization
        {
            Id = Guid.NewGuid(),
            Code = $"ORG-{Guid.NewGuid():N}"[..16],
            Name = $"Otra Org {Guid.NewGuid():N}",
        };
        var otherClinic = new Clinic
        {
            Id = Guid.NewGuid(),
            Name = $"Otra {Guid.NewGuid():N}",
            OrganizationId = otherOrganization.Id,
        };

        _db.Organizations.AddRange(organization, otherOrganization);
        _db.Clinics.AddRange(clinic, otherClinic);
        _db.Employees.Add(employee);
        _db.Professionals.Add(professional);
        await _db.SaveChangesAsync();

        var patientId = await SeedPatientAsync(_db, "+573053924819");
        var patient = await _db.PatientProfiles.SingleAsync(p => p.Id == patientId);
        patient.ClinicId = clinic.Id;
        await _db.SaveChangesAsync();

        var repository = NewRepository(_db);

        // Sin asignación y sin contexto: false.
        Assert.False(
            await repository.IsStaffScopedToPatientAsync(patientId, professional.Id, null, null)
        );

        // Con asignación directa activa: true.
        _db.PatientProfessionalAssignments.Add(
            new PatientProfessionalAssignment
            {
                PatientId = patientId,
                ProfessionalId = professional.Id,
                Status = "Active",
                RelationshipType = "Assigned",
            }
        );
        await _db.SaveChangesAsync();
        Assert.True(
            await repository.IsStaffScopedToPatientAsync(patientId, professional.Id, null, null)
        );

        // Con clínica del contexto activo: true (aunque sin asignación... la
        // asignación existe aquí, se prueba el branch con otro profesional).
        Assert.True(
            await repository.IsStaffScopedToPatientAsync(patientId, Guid.NewGuid(), clinic.Id, null)
        );
        Assert.False(
            await repository.IsStaffScopedToPatientAsync(
                patientId,
                Guid.NewGuid(),
                otherClinic.Id,
                null
            )
        );
    }

    [Fact]
    public async Task ListadoStaff_ResolucionDeScope_UnionDistintaYDenegacion()
    {
        // (D5, bandeja staff) ResolveScopedPatientIdsAsync: unión de asignación
        // directa + pacientes de la clínica activa + de la organización activa,
        // con DISTINCT; sin nada → lista vacía (denegar).
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Code = $"ORG-{Guid.NewGuid():N}"[..16],
            Name = $"Org {Guid.NewGuid():N}",
        };
        var clinic = new Clinic
        {
            Id = Guid.NewGuid(),
            Name = $"Clínica {Guid.NewGuid():N}",
            OrganizationId = organization.Id,
        };
        var otherOrganization = new Organization
        {
            Id = Guid.NewGuid(),
            Code = $"ORG-{Guid.NewGuid():N}"[..16],
            Name = $"Otra Org {Guid.NewGuid():N}",
        };

        _db.Organizations.AddRange(organization, otherOrganization);
        _db.Clinics.Add(clinic);
        await _db.SaveChangesAsync();

        // Dos pacientes: uno en la clínica del scope, otro en otra organización.
        var reachablePatient = await SeedPatientAsync(_db, "+573053924819");
        var unreachablePatient = await SeedPatientAsync(_db, "+573053925000");
        var reachable = await _db.PatientProfiles.SingleAsync(p => p.Id == reachablePatient);
        var unreachable = await _db.PatientProfiles.SingleAsync(p => p.Id == unreachablePatient);
        reachable.ClinicId = clinic.Id;
        unreachable.ClinicId = (
            await _db.Clinics.AddAsync(
                new Clinic
                {
                    Id = Guid.NewGuid(),
                    Name = $"Ajena {Guid.NewGuid():N}",
                    OrganizationId = otherOrganization.Id,
                }
            )
        ).Entity.Id;
        await _db.SaveChangesAsync();

        var repository = NewRepository(_db);

        // Sin asignación, sin clínica, sin organización → lista vacía (denegar).
        var empty = await repository.ResolveScopedPatientIdsAsync(null, null, null);
        Assert.Empty(empty);

        // Por organización: solo el paciente de la clínica de esa org.
        var byOrg = await repository.ResolveScopedPatientIdsAsync(null, null, organization.Id);
        Assert.Contains(reachablePatient, byOrg);
        Assert.DoesNotContain(unreachablePatient, byOrg);

        // Por clínica: igual resultado (DISTINCT entre ramas).
        var byClinic = await repository.ResolveScopedPatientIdsAsync(null, clinic.Id, null);
        Assert.Contains(reachablePatient, byClinic);
        Assert.DoesNotContain(unreachablePatient, byClinic);

        // Con asignación directa a un paciente UNREACHABLE por clínica: la
        // unión lo incluye y el DISTINCT evita duplicados del paciente común.
        var directEmployee = new Employee
        {
            Id = Guid.NewGuid(),
            OrganizationId = organization.Id,
            FirstName = "Directo",
            MiddleName = "",
            LastName = "Prueba",
            Email = $"directo.{Guid.NewGuid():N}@test.local",
        };
        _db.Employees.Add(directEmployee);
        _db.Professionals.Add(
            new Professional { Id = Guid.NewGuid(), EmployeeId = directEmployee.Id }
        );
        await _db.SaveChangesAsync();
        _db.PatientProfessionalAssignments.Add(
            new PatientProfessionalAssignment
            {
                PatientId = unreachablePatient,
                ProfessionalId = (
                    await _db.Professionals.SingleAsync(p => p.EmployeeId == directEmployee.Id)
                ).Id,
                Status = "Active",
                RelationshipType = "Assigned",
            }
        );
        await _db.SaveChangesAsync();

        var union = await repository.ResolveScopedPatientIdsAsync(
            (await _db.Professionals.SingleAsync(p => p.EmployeeId == directEmployee.Id)).Id,
            clinic.Id,
            null
        );
        Assert.Contains(reachablePatient, union);
        Assert.Contains(unreachablePatient, union); // por asignación directa
        Assert.Equal(union.Count, union.Distinct().Count()); // DISTINCT garantizado
    }
}
