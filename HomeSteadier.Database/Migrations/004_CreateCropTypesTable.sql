CREATE TABLE IF NOT EXISTS "crop_types" (
    "id" SERIAL PRIMARY KEY,
    "name" VARCHAR(100) NOT NULL,
    "genus" VARCHAR(100) NOT NULL,
    "family" VARCHAR(100) NOT NULL
);
