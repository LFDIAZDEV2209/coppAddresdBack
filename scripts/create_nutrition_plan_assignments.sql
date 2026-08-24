-- Create nutrition_plan_assignments table
CREATE TABLE IF NOT EXISTS app.nutrition_plan_assignments (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    patient_id uuid NOT NULL,
    plan_id uuid NOT NULL,
    start_date date NOT NULL,
    end_date date,
    status integer NOT NULL DEFAULT 1,
    notes text,
    created_by uuid,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz
);

-- Foreign keys
ALTER TABLE app.nutrition_plan_assignments
    ADD CONSTRAINT fk_nutrition_plan_assignments_patient
    FOREIGN KEY (patient_id) REFERENCES app.patient_profiles(id) ON DELETE CASCADE;

ALTER TABLE app.nutrition_plan_assignments
    ADD CONSTRAINT fk_nutrition_plan_assignments_plan
    FOREIGN KEY (plan_id) REFERENCES app.nutrition_plans(id) ON DELETE CASCADE;

-- Indexes
CREATE INDEX IF NOT EXISTS ix_nutrition_plan_assignments_patient_id
    ON app.nutrition_plan_assignments (patient_id);

CREATE INDEX IF NOT EXISTS ix_nutrition_plan_assignments_plan_id
    ON app.nutrition_plan_assignments (plan_id);

CREATE INDEX IF NOT EXISTS ix_nutrition_plan_assignments_status
    ON app.nutrition_plan_assignments (status);

CREATE INDEX IF NOT EXISTS ix_nutrition_plan_assignments_patient_status
    ON app.nutrition_plan_assignments (patient_id, status);

-- Verify
SELECT table_name FROM information_schema.tables 
WHERE table_schema = 'app' AND table_name = 'nutrition_plan_assignments';
