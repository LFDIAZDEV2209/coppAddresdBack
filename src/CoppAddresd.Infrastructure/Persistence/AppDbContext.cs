using CoppAddresd.Domain.Entities;
using CoppAddresd.Domain.Entities.FoodAi;
using CoppAddresd.Domain.Entities.HealthTests;
using CoppAddresd.Domain.Entities.ProgramProgress;
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
    public DbSet<DeviceToken> DeviceTokens => Set<DeviceToken>();
    public DbSet<PatientProfessionalAssignment> PatientProfessionalAssignments =>
        Set<PatientProfessionalAssignment>();
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
    public DbSet<ProfessionalTypeSpecialty> ProfessionalTypeSpecialties =>
        Set<ProfessionalTypeSpecialty>();
    public DbSet<ProfessionalLicense> ProfessionalLicenses => Set<ProfessionalLicense>();

    public DbSet<MediaItem> MediaItems => Set<MediaItem>();
    public DbSet<Document> Documents => Set<Document>();
    public DbSet<DocumentCategory> DocumentCategories => Set<DocumentCategory>();
    public DbSet<ClinicalDocumentType> ClinicalDocumentTypes => Set<ClinicalDocumentType>();

    public DbSet<AgentType> AgentTypes => Set<AgentType>();
    public DbSet<AgentTypeVersion> AgentTypeVersions => Set<AgentTypeVersion>();
    public DbSet<KnowledgeBase> KnowledgeBases => Set<KnowledgeBase>();
    public DbSet<AgentDocument> AgentDocuments => Set<AgentDocument>();
    public DbSet<AgentInstance> AgentInstances => Set<AgentInstance>();

    public DbSet<Product> Products => Set<Product>();
    public DbSet<InventoryEntry> InventoryEntries => Set<InventoryEntry>();
    public DbSet<InventoryEntryLine> InventoryEntryLines => Set<InventoryEntryLine>();
    public DbSet<InventoryExit> InventoryExits => Set<InventoryExit>();
    public DbSet<InventoryExitLine> InventoryExitLines => Set<InventoryExitLine>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<StoreItem> StoreItems => Set<StoreItem>();
    public DbSet<LegalDocument> LegalDocuments => Set<LegalDocument>();
    public DbSet<LegalDocumentVersion> LegalDocumentVersions => Set<LegalDocumentVersion>();

    // Wellness — Centro de Bienestar
    public DbSet<NutritionPlan> NutritionPlans => Set<NutritionPlan>();
    public DbSet<NutritionPlanDay> NutritionPlanDays => Set<NutritionPlanDay>();
    public DbSet<ExerciseRoutine> ExerciseRoutines => Set<ExerciseRoutine>();
    public DbSet<RoutineExercise> RoutineExercises => Set<RoutineExercise>();
    public DbSet<RoutineAssignment> RoutineAssignments => Set<RoutineAssignment>();
    public DbSet<NutritionPlanAssignment> NutritionPlanAssignments =>
        Set<NutritionPlanAssignment>();

    // Clinical Measurements — Mediciones clínicas
    public DbSet<UnitOfMeasure> UnitOfMeasures => Set<UnitOfMeasure>();
    public DbSet<MeasurementMetric> MeasurementMetrics => Set<MeasurementMetric>();
    public DbSet<MeasurementReferenceRange> MeasurementReferenceRanges =>
        Set<MeasurementReferenceRange>();
    public DbSet<Encounter> Encounters => Set<Encounter>();
    public DbSet<ClinicalMeasurement> ClinicalMeasurements => Set<ClinicalMeasurement>();
    public DbSet<PlanSafetyRule> PlanSafetyRules => Set<PlanSafetyRule>();

// Food AI — Nutrición (schema foodai)
    public DbSet<Food> Foods => Set<Food>();
    public DbSet<FoodNutrition> FoodNutritionEntries => Set<FoodNutrition>();
    public DbSet<FoodAlias> FoodAliases => Set<FoodAlias>();

    // Food AI — Análisis y feedback (schema foodai)
    public DbSet<FoodAnalysis> FoodAnalyses => Set<FoodAnalysis>();
    public DbSet<FoodAnalysisItem> FoodAnalysisItems => Set<FoodAnalysisItem>();
    public DbSet<FoodAnalysisFeedback> FoodAnalysisFeedbacks => Set<FoodAnalysisFeedback>();

    // Health Tests — Tests de Salud (catálogo versionado + ejecución)
    public DbSet<HealthTestInstrument> HealthTestInstruments => Set<HealthTestInstrument>();
    public DbSet<HealthTestVersion> HealthTestVersions => Set<HealthTestVersion>();
    public DbSet<HealthTestQuestion> HealthTestQuestions => Set<HealthTestQuestion>();
    public DbSet<HealthTestAnswerOption> HealthTestAnswerOptions => Set<HealthTestAnswerOption>();
    public DbSet<HealthTestScoreRange> HealthTestScoreRanges => Set<HealthTestScoreRange>();
    public DbSet<HealthTestBattery> HealthTestBatteries => Set<HealthTestBattery>();
    public DbSet<HealthTestBatteryItem> HealthTestBatteryItems => Set<HealthTestBatteryItem>();
    public DbSet<HealthTestBatteryAssignment> HealthTestBatteryAssignments =>
        Set<HealthTestBatteryAssignment>();
    public DbSet<HealthTestAssignment> HealthTestAssignments => Set<HealthTestAssignment>();
    public DbSet<HealthTestEvaluation> HealthTestEvaluations => Set<HealthTestEvaluation>();
    public DbSet<HealthTestResponse> HealthTestResponses => Set<HealthTestResponse>();
    public DbSet<HealthTestResult> HealthTestResults => Set<HealthTestResult>();
    public DbSet<HealthTestIndicatorDef> HealthTestIndicatorDefs => Set<HealthTestIndicatorDef>();
    public DbSet<HealthTestAlertRule> HealthTestAlertRules => Set<HealthTestAlertRule>();
    public DbSet<HealthTestAlert> HealthTestAlerts => Set<HealthTestAlert>();
    public DbSet<HealthTestComment> HealthTestComments => Set<HealthTestComment>();

    // Program Progress — Módulo de progreso
    public DbSet<ProgramTemplate> ProgramTemplates => Set<ProgramTemplate>();
    public DbSet<WeeklyDayTemplate> WeeklyDayTemplates => Set<WeeklyDayTemplate>();
    public DbSet<ProgramEnrollment> ProgramEnrollments => Set<ProgramEnrollment>();
    public DbSet<ProgramWeek> ProgramWeeks => Set<ProgramWeek>();
    public DbSet<DailyCheckIn> DailyCheckIns => Set<DailyCheckIn>();
    public DbSet<TaskCompletion> TaskCompletions => Set<TaskCompletion>();
    public DbSet<XpLedgerEntry> XpLedgerEntries => Set<XpLedgerEntry>();
    public DbSet<StreakState> StreakStates => Set<StreakState>();
    public DbSet<StreakFreeze> StreakFreezes => Set<StreakFreeze>();
    public DbSet<AdaptationRecommendation> AdaptationRecommendations =>
        Set<AdaptationRecommendation>();
    public DbSet<EmotionalRecord> EmotionalRecords => Set<EmotionalRecord>();
    public DbSet<XpRule> XpRules => Set<XpRule>();

    // Program Progress — Scores
    public DbSet<HealthScoreWeight> HealthScoreWeights => Set<HealthScoreWeight>();
    public DbSet<ClinicalBaseline> ClinicalBaselines => Set<ClinicalBaseline>();
    public DbSet<HealthScore> HealthScores => Set<HealthScore>();
    public DbSet<TransformationScore> TransformationScores => Set<TransformationScore>();

    // Program Progress — Revisiones clínicas de XP (SPEC §15)
    public DbSet<ClinicalXpReview> ClinicalXpReviews => Set<ClinicalXpReview>();

    // Program Progress — Hábitos de alimentación/hidratación (SPEC §18)
    public DbSet<HabitTemplate> HabitTemplates => Set<HabitTemplate>();
    public DbSet<HabitCheck> HabitChecks => Set<HabitCheck>();

    // Program Progress — Intake nutricional enriquecido (SPEC nutrition-intake-adherence)
    public DbSet<NutritionIntakeLog> NutritionIntakeLogs => Set<NutritionIntakeLog>();

    // Program Progress — Notificaciones gamificadas (SPEC §20)
    public DbSet<AppNotification> AppNotifications => Set<AppNotification>();

    // SOS — Alertas de emergencia (schema sos)
    public DbSet<SosAlert> SosAlerts => Set<SosAlert>();

    // Program Progress — Debilidades del paciente (SPEC §21, "Paso 7c")
    public DbSet<Weakness> Weaknesses => Set<Weakness>();

    // Program Progress — Intervenciones derivadas de debilidades (SPEC §22, "Paso 7d")
    public DbSet<Intervention> Interventions => Set<Intervention>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
