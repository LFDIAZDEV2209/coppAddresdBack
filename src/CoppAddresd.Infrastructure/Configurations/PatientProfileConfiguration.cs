using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

/// <summary>
/// Configuración del perfil de paciente en el schema <c>app</c>.
/// El FK hacia auth.users se crea por SQL en la migración (fuera del modelo EF).
/// </summary>
public sealed class PatientProfileConfiguration : IEntityTypeConfiguration<PatientProfile>
{
    public void Configure(EntityTypeBuilder<PatientProfile> builder)
    {
        builder.ToTable("patient_profiles", "app");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id)
            .HasColumnName("id")
            .HasDefaultValueSql("gen_random_uuid()");

        builder.Property(x => x.UserId)
            .HasColumnName("user_id");

        builder.Property(x => x.MedicalRecordNumber)
            .HasColumnName("medical_record_number")
            .HasMaxLength(50);

        builder.Property(x => x.FirstName)
            .HasColumnName("first_name")
            .HasMaxLength(100);

        builder.Property(x => x.MiddleName)
            .HasColumnName("middle_name")
            .HasMaxLength(100);

        builder.Property(x => x.LastName)
            .HasColumnName("last_name")
            .HasMaxLength(100);

        builder.Property(x => x.DocumentTypeId)
            .HasColumnName("document_type_id");

        builder.Property(x => x.DocumentNumber)
            .HasColumnName("document_number")
            .HasMaxLength(50);

        builder.Property(x => x.DateOfBirth)
            .HasColumnName("date_of_birth")
            .HasColumnType("date");

        builder.Property(x => x.Gender)
            .HasColumnName("gender")
            .HasMaxLength(10);

        builder.Property(x => x.EthnicityId)
            .HasColumnName("ethnicity_id");

        builder.Property(x => x.BloodTypeId)
            .HasColumnName("blood_type_id");

        builder.Property(x => x.PhoneCountryCode)
            .HasColumnName("phone_country_code")
            .HasMaxLength(10);

        builder.Property(x => x.PhoneNumber)
            .HasColumnName("phone_number")
            .HasMaxLength(20);

        builder.Property(x => x.Email)
            .HasColumnName("email")
            .HasMaxLength(320);

        builder.Property(x => x.Address)
            .HasColumnName("address")
            .HasMaxLength(200);

        builder.Property(x => x.CityId)
            .HasColumnName("city_id");

        builder.Property(x => x.StateId)
            .HasColumnName("state_id");

        builder.Property(x => x.CountryId)
            .HasColumnName("country_id");

        builder.Property(x => x.PostalCode)
            .HasColumnName("postal_code")
            .HasMaxLength(10);

        builder.Property(x => x.EmergencyContact)
            .HasColumnName("emergency_contact")
            .HasMaxLength(30);

        builder.Property(x => x.InsurerId)
            .HasColumnName("insurer_id");

        builder.Property(x => x.MemberId)
            .HasColumnName("member_id")
            .HasMaxLength(50);

        builder.Property(x => x.MaritalStatus)
            .HasColumnName("marital_status")
            .HasMaxLength(30);

        builder.Property(x => x.SmokingStatus)
            .HasColumnName("smoking_status")
            .HasMaxLength(30);

        builder.Property(x => x.AlcoholStatus)
            .HasColumnName("alcohol_status")
            .HasMaxLength(30);

        builder.Property(x => x.ExerciseLevel)
            .HasColumnName("exercise_level")
            .HasMaxLength(30);

        builder.Property(x => x.Disability)
            .HasColumnName("disability")
            .HasMaxLength(30);

        builder.Property(x => x.HospitalizationHistory)
            .HasColumnName("hospitalization_history")
            .HasMaxLength(30);

        builder.Property(x => x.SurgeryHistory)
            .HasColumnName("surgery_history")
            .HasMaxLength(100);

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasDefaultValue("Activo");

        builder.Property(x => x.Notes)
            .HasColumnName("notes");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamptz")
            .HasDefaultValueSql("now()");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamptz");

        // Un paciente puede tener una cuenta (relación 1:1 con auth.users) o
        // ser solo del directorio clínico (sin usuario).
        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("ix_patient_profiles_user_id")
            .IsUnique();

        builder.HasIndex(x => x.MedicalRecordNumber)
            .HasDatabaseName("ix_patient_profiles_medical_record_number")
            .IsUnique();

        builder.HasIndex(x => x.Status)
            .HasDatabaseName("ix_patient_profiles_status");

        builder.HasOne(x => x.Insurer)
            .WithMany(x => x.Patients)
            .HasForeignKey(x => x.InsurerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.DocumentType)
            .WithMany()
            .HasForeignKey(x => x.DocumentTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Ethnicity)
            .WithMany()
            .HasForeignKey(x => x.EthnicityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.BloodType)
            .WithMany()
            .HasForeignKey(x => x.BloodTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Country)
            .WithMany()
            .HasForeignKey(x => x.CountryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.State)
            .WithMany()
            .HasForeignKey(x => x.StateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.City)
            .WithMany()
            .HasForeignKey(x => x.CityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Diagnoses)
            .WithOne(x => x.Patient)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Medications)
            .WithOne(x => x.Patient)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Allergies)
            .WithOne(x => x.Patient)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.VitalSigns)
            .WithOne(x => x.Patient)
            .HasForeignKey(x => x.PatientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}