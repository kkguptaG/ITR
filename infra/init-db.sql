-- =============================================================================
-- TallyG Tax Portal — Postgres bootstrap.
--
-- Runs ONCE, as the superuser, the first time the postgres data directory is
-- initialized (mounted into /docker-entrypoint-initdb.d). It only prepares
-- extensions; the application schema itself is created by EF Core migrations
-- when the API boots (Database:Provider=Postgres => db.Database.Migrate()).
--
-- pgcrypto gives us gen_random_uuid() and crypto primitives referenced by the
-- security design (Ch 6). Safe to keep idempotent via IF NOT EXISTS.
-- =============================================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- Case-insensitive text (handy for email/PAN lookups). Harmless if unused.
CREATE EXTENSION IF NOT EXISTS citext;
