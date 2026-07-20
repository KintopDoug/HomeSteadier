-- Clerk becomes the identity provider and owner of credentials.
-- The local "users" row is now a synced mirror keyed by clerk_user_id (populated
-- via Clerk webhooks); the local password column is removed. First/last name are
-- relaxed to nullable since OAuth sign-ups may not supply them.

ALTER TABLE "users" ADD COLUMN IF NOT EXISTS "clerk_user_id" VARCHAR(255);

CREATE UNIQUE INDEX IF NOT EXISTS "ix_users_clerk_user_id" ON "users"("clerk_user_id");

ALTER TABLE "users" ALTER COLUMN "first_name" DROP NOT NULL;
ALTER TABLE "users" ALTER COLUMN "last_name" DROP NOT NULL;

ALTER TABLE "users" DROP COLUMN IF EXISTS "password";
