CREATE TABLE IF NOT EXISTS "farms" (
    "id" SERIAL PRIMARY KEY,
    "name" VARCHAR(255) NOT NULL,
    "address_line" VARCHAR(255) NULL,
    "city" VARCHAR(100) NULL,
    "state" VARCHAR(100) NULL,
    "postal_code" VARCHAR(20) NULL,
    "country" VARCHAR(100) NULL,
    "latitude" NUMERIC(9,6) NOT NULL,
    "longitude" NUMERIC(9,6) NOT NULL,
    "timezone" VARCHAR(64) NOT NULL,
    "created_at" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS "farm_role_types" (
    "id" SERIAL PRIMARY KEY,
    "name" VARCHAR(50) NOT NULL ,
    "created_at" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE UNIQUE INDEX IF NOT EXISTS "ix_farm_role_types_name" ON "farm_role_types"("name");

CREATE TABLE IF NOT EXISTS "user_farms" (
    "id" SERIAL PRIMARY KEY,
    "user_id" INTEGER NOT NULL REFERENCES "users"("id"),
    "farm_id" INTEGER NOT NULL REFERENCES "farms"("id"),
    "farm_role_type_id" integer NOT NULL REFERENCES "farm_role_types"("id"),
    "created_at" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP
);



CREATE INDEX IF NOT EXISTS "ix_user_farms_user_id" ON "user_farms"("user_id");

CREATE INDEX IF NOT EXISTS "ix_user_farms_farm_id" ON "user_farms"("farm_id");

CREATE UNIQUE INDEX IF NOT EXISTS "ix_user_farms_user_id_farm_id" ON "user_farms"("user_id", "farm_id");
