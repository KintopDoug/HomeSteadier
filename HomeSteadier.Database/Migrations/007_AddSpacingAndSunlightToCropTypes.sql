ALTER TABLE "crop_types"
    ADD COLUMN IF NOT EXISTS "spacing_inches" INTEGER NULL,
    ADD COLUMN IF NOT EXISTS "sunlight_requirement_hours" NUMERIC(6,2) NULL;
