-- UserStore looks users up via upper(email), matching ASP.NET Identity's default
-- UpperInvariantLookupNormalizer, but no index exists on that expression and the
-- existing UNIQUE constraint is case-sensitive — two accounts differing only by case
-- can be created under a race. ix_users_email (plain btree) has never actually been
-- usable for this lookup, so it's replaced rather than left alongside the new one.
DROP INDEX IF EXISTS "ix_users_email";

CREATE UNIQUE INDEX IF NOT EXISTS "ix_users_email_upper" ON "users" (upper("email"));
