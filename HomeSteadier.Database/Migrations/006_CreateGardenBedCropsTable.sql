CREATE TABLE IF NOT EXISTS "garden_bed_crops" (
    "id" SERIAL PRIMARY KEY,
    "garden_bed_id" INTEGER NOT NULL REFERENCES "garden_bed"("id") ON DELETE CASCADE,
    "crop_type_id" INTEGER NOT NULL REFERENCES "crop_types"("id") ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS "ix_garden_bed_crops_garden_bed_id" ON "garden_bed_crops"("garden_bed_id");

CREATE INDEX IF NOT EXISTS "ix_garden_bed_crops_crop_type_id" ON "garden_bed_crops"("crop_type_id");
