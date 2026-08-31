CREATE TABLE IF NOT EXISTS "farm_invitations" (
    "id" SERIAL PRIMARY KEY,
    "farm_id" INTEGER NOT NULL REFERENCES "farms"("id") ON DELETE CASCADE,
    "farm_role_type_id" INTEGER NOT NULL REFERENCES "farm_role_types"("id"),
    "email" VARCHAR(255) NOT NULL,
    "invited_by_user_id" INTEGER NOT NULL REFERENCES "users"("id"),
    "token_hash" VARCHAR(128) NOT NULL,
    "expires_at" TIMESTAMPTZ NOT NULL,
    "created_at" TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "accepted_at" TIMESTAMPTZ NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "ix_farm_invitations_token_hash" ON "farm_invitations"("token_hash");

CREATE INDEX IF NOT EXISTS "ix_farm_invitations_farm_id_email" ON "farm_invitations"("farm_id", "email");
