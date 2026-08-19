using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CoppAddresd.Infrastructure.Persistence;

/// <summary>
/// DbContext principal. El activity log se mapea al schema <c>audit</c>,
/// los perfiles de la app móvil al schema <c>app</c>, las entidades del
/// ERP al schema <c>erp</c> y el módulo de agentes al schema <c>agents</c>.
/// El schema <c>public</c> queda reservado.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<PatientProfile> PatientProfiles => Set<PatientProfile>();
    public DbSet<Insurer> Insurers => Set<Insurer>();
    public DbSet<Allergen> Allergens => Set<Allergen>();
    public DbSet<Icd10Code> Icd10Codes => Set<Icd10Code>();
    public DbSet<Medication> Medications => Set<Medication>();
    public DbSet<PatientDiagnosis> PatientDiagnoses => Set<PatientDiagnosis>();
    public DbSet<PatientMedication> PatientMedications => Set<PatientMedication>();
    public DbSet<PatientAllergy> PatientAllergies => Set<PatientAllergy>();
    public DbSet<VitalSign> VitalSigns => Set<VitalSign>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<State> States => Set<State>();
    public DbSet<City> Cities => Set<City>();
    public DbSet<PostalCode> PostalCodes => Set<PostalCode>();
    public DbSet<BloodType> BloodTypes => Set<BloodType>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<Ethnicity> Ethnicities => Set<Ethnicity>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<Clinic> Clinics => Set<Clinic>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<ProfessionalType> ProfessionalTypes => Set<ProfessionalType>();
    public DbSet<Specialty> Specialties => Set<Specialty>();
    public DbSet<Professional> Professionals => Set<Professional>();
    public DbSet<EmployeeClinic> EmployeeClinics => Set<EmployeeClinic>();
    public DbSet<ProfessionalLocation> ProfessionalLocations => Set<ProfessionalLocation>();
    public DbSet<ProfessionalSpecialty> ProfessionalSpecialties => Set<ProfessionalSpecialty>();
    public DbSet<ProfessionalTypeSpecialty> ProfessionalTypeSpecialties => Set<ProfessionalTypeSpecialty>();
    public DbSet<ProfessionalLicense> ProfessionalLicenses => Set<ProfessionalLicense>();

    public DbSet<MediaItem> MediaItems => Set<MediaItem>();

    public DbSet<AgentType> AgentTypes => Set<AgentType>();
    public DbSet<AgentTypeVersion> AgentTypeVersions => Set<AgentTypeVersion>();
    public DbSet<KnowledgeBase> KnowledgeBases => Set<KnowledgeBase>();
    public DbSet<AgentDocument> AgentDocuments => Set<AgentDocument>();
    public DbSet<AgentInstance> AgentInstances => Set<AgentInstance>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
