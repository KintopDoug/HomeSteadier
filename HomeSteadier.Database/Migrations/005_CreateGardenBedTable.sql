CREATE TABLE IF NOT EXISTS "garden_bed" (
    "id" SERIAL PRIMARY KEY,
    "name" VARCHAR(255) NOT NULL,
    "length" NUMERIC(6,2) NOT NULL,
    "width" NUMERIC(6,2) NOT NULL,
    "sunlight_hours" NUMERIC(6,2) NOT NULL
);
