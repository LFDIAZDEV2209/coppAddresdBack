using CoppAddresd.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoppAddresd.Infrastructure.Configurations;

public sealed class NutritionFoodItemConfiguration : IEntityTypeConfiguration<NutritionFoodItem>
{
    public void Configure(EntityTypeBuilder<NutritionFoodItem> b)
    {
        b.ToTable("nutrition_food_items", "app", t =>
        {
            t.HasCheckConstraint("ck_nutrition_food_group", "food_group IN ('C','P','F','V')");
            t.HasCheckConstraint("ck_nutrition_food_nutrients", "calories_100 >= 0 AND carbs_100 >= 0 AND protein_100 >= 0 AND fat_100 >= 0 AND fiber_100 >= 0 AND calories_100 < 'Infinity'::float8 AND carbs_100 < 'Infinity'::float8 AND protein_100 < 'Infinity'::float8 AND fat_100 < 'Infinity'::float8 AND fiber_100 < 'Infinity'::float8");
            t.HasCheckConstraint("ck_nutrition_food_unit", "(unit_singular IS NULL AND unit_plural IS NULL AND grams_per_unit IS NULL AND unit_step IS NULL) OR (unit_singular IS NOT NULL AND unit_plural IS NOT NULL AND grams_per_unit IS NOT NULL AND unit_step IS NOT NULL AND grams_per_unit > 0 AND grams_per_unit < 'Infinity'::float8 AND unit_step > 0 AND unit_step < 'Infinity'::float8)");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id").HasMaxLength(40);
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
        b.Property(x => x.Icon).HasColumnName("icon").HasMaxLength(16);
        b.Property(x => x.FoodGroup).HasColumnName("food_group").HasMaxLength(1);
        b.Property(x => x.Calories100).HasColumnName("calories_100");
        b.Property(x => x.Carbs100).HasColumnName("carbs_100");
        b.Property(x => x.Protein100).HasColumnName("protein_100");
        b.Property(x => x.Fat100).HasColumnName("fat_100");
        b.Property(x => x.Fiber100).HasColumnName("fiber_100");
        b.Property(x => x.UnitSingular).HasColumnName("unit_singular").HasMaxLength(50);
        b.Property(x => x.UnitPlural).HasColumnName("unit_plural").HasMaxLength(50);
        b.Property(x => x.GramsPerUnit).HasColumnName("grams_per_unit");
        b.Property(x => x.UnitStep).HasColumnName("unit_step");
    }
}

public sealed class NutritionPrescriptionConfiguration : IEntityTypeConfiguration<NutritionPrescription>
{
    public void Configure(EntityTypeBuilder<NutritionPrescription> b)
    {
        b.ToTable("nutrition_prescriptions", "app", t =>
        {
            t.HasCheckConstraint("ck_nutrition_prescription_state", "state IN ('borrador_ia','en_aprobacion','aprobado','devuelto')");
            t.HasCheckConstraint("ck_nutrition_prescription_version", "version > 0 AND revision > 0");
            t.HasCheckConstraint("ck_nutrition_prescription_dates", "(start_date IS NULL AND end_date IS NULL) OR (start_date IS NOT NULL AND end_date IS NOT NULL AND end_date >= start_date AND end_date - start_date < 30)");
            t.HasCheckConstraint("ck_nutrition_prescription_approval", "state <> 'aprobado' OR (start_date IS NOT NULL AND end_date IS NOT NULL AND approved_by IS NOT NULL AND approved_at IS NOT NULL)");
            t.HasCheckConstraint("ck_nutrition_prescription_publication", "NOT is_published OR state = 'aprobado'");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PatientId).HasColumnName("patient_id");
        b.Property(x => x.CycleId).HasColumnName("cycle_id");
        b.Property(x => x.Version).HasColumnName("version");
        b.Property(x => x.Revision).HasColumnName("revision").IsConcurrencyToken();
        b.Property(x => x.State).HasColumnName("state").HasMaxLength(30);
        b.Property(x => x.Provisional).HasColumnName("provisional");
        b.Property(x => x.IsPublished).HasColumnName("is_published");
        b.Property(x => x.MenusOutdated).HasColumnName("menus_outdated");
        b.Property(x => x.StartDate).HasColumnName("start_date").HasColumnType("date");
        b.Property(x => x.EndDate).HasColumnName("end_date").HasColumnType("date");
        b.Property(x => x.CalorieTarget).HasColumnName("calorie_target");
        b.Property(x => x.CarbsTarget).HasColumnName("carbs_target");
        b.Property(x => x.ProteinTarget).HasColumnName("protein_target");
        b.Property(x => x.FatTarget).HasColumnName("fat_target");
        b.Property(x => x.FiberTarget).HasColumnName("fiber_target");
        b.Property(x => x.WaterMl).HasColumnName("water_ml");
        b.Property(x => x.Instructions).HasColumnName("instructions");
        b.Property(x => x.InstructionsEn).HasColumnName("instructions_en");
        b.Property(x => x.ReturnComment).HasColumnName("return_comment");
        b.Property(x => x.CreatedBy).HasColumnName("created_by");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz");
        b.Property(x => x.UpdatedBy).HasColumnName("updated_by");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz");
        b.Property(x => x.ApprovedBy).HasColumnName("approved_by");
        b.Property(x => x.ApprovedAt).HasColumnName("approved_at").HasColumnType("timestamptz");
        b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.PatientId, x.State, x.StartDate, x.EndDate }).HasDatabaseName("ix_nutrition_prescriptions_patient_state_validity");
        b.HasIndex(x => new { x.State, x.EndDate }).HasDatabaseName("ix_nutrition_prescriptions_state_end");
        b.HasIndex(x => new { x.CycleId, x.Version }).IsUnique().HasDatabaseName("uq_nutrition_prescriptions_cycle_version");
        b.HasIndex(x => x.CycleId).IsUnique().HasFilter("is_published = TRUE").HasDatabaseName("uq_nutrition_prescriptions_published_cycle");
    }
}

public sealed class NutritionPrescriptionMealConfiguration : IEntityTypeConfiguration<NutritionPrescriptionMeal>
{
    public void Configure(EntityTypeBuilder<NutritionPrescriptionMeal> b)
    {
        b.ToTable("nutrition_prescription_meals", "app", t =>
        {
            t.HasCheckConstraint("ck_nutrition_meal_day", "day_number BETWEEN 1 AND 30");
            t.HasCheckConstraint("ck_nutrition_meal_type", "(meal_type = 'des' AND scheduled_time = TIME '07:00') OR (meal_type = 'alm' AND scheduled_time = TIME '12:00') OR (meal_type = 'mer' AND scheduled_time = TIME '15:30') OR (meal_type = 'cen' AND scheduled_time = TIME '19:00')");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PrescriptionId).HasColumnName("prescription_id");
        b.Property(x => x.DayNumber).HasColumnName("day_number");
        b.Property(x => x.MealType).HasColumnName("meal_type").HasMaxLength(3);
        b.Property(x => x.ScheduledTime).HasColumnName("scheduled_time");
        b.Property(x => x.MenuName).HasColumnName("menu_name").HasMaxLength(200);
        b.Property(x => x.Notes).HasColumnName("notes");
        b.Property(x => x.WaterMl).HasColumnName("water_ml");
        b.HasOne(x => x.Prescription).WithMany(x => x.Meals).HasForeignKey(x => x.PrescriptionId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.PrescriptionId, x.DayNumber, x.MealType }).IsUnique().HasDatabaseName("uq_nutrition_meals_prescription_day_type");
    }
}

public sealed class NutritionPrescriptionItemConfiguration : IEntityTypeConfiguration<NutritionPrescriptionItem>
{
    public void Configure(EntityTypeBuilder<NutritionPrescriptionItem> b)
    {
        b.ToTable("nutrition_prescription_items", "app", t =>
            t.HasCheckConstraint("ck_nutrition_item_grams", "grams > 0 AND grams < 'Infinity'::float8"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.MealId).HasColumnName("meal_id");
        b.Property(x => x.FoodItemId).HasColumnName("food_item_id").HasMaxLength(40);
        b.Property(x => x.Grams).HasColumnName("grams");
        b.Property(x => x.SortOrder).HasColumnName("sort_order");
        b.Property(x => x.Notes).HasColumnName("notes");
        b.HasOne(x => x.Meal).WithMany(x => x.Items).HasForeignKey(x => x.MealId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.FoodItem).WithMany().HasForeignKey(x => x.FoodItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.MealId, x.SortOrder }).HasDatabaseName("ix_nutrition_items_meal_order");
        b.HasIndex(x => x.FoodItemId).HasDatabaseName("ix_nutrition_items_food");
    }
}
