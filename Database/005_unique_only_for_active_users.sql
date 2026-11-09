-- =====================================================================
-- Incremental migration for databases already created from init.sql.
-- Safe to run once against an existing AppFinances database.
-- =====================================================================

-- Deleting a user is a soft delete (ACTIVE = false), so the row stays in
-- the table. Plain UNIQUE constraints kept blocking a new registration
-- with the email, phone or username of a deleted user; the duplicates now
-- only count among active users.

-- 1) USERNAME lives in AUTHENTICATION, and a partial index can't look at
--    USERS.ACTIVE, so AUTHENTICATION gets its own ACTIVE flag, kept in sync
--    by UserRepository.DeleteAsync.
ALTER TABLE AUTHENTICATION
    ADD COLUMN IF NOT EXISTS ACTIVE BOOLEAN NOT NULL DEFAULT TRUE;

UPDATE AUTHENTICATION a
SET ACTIVE = u.ACTIVE
FROM USERS u
WHERE u.ID = a.USER_ID;

-- 2) Swap the table-wide UNIQUE constraints for unique indexes over the
--    active rows only.
ALTER TABLE USERS DROP CONSTRAINT IF EXISTS USERS_EMAIL_KEY;
ALTER TABLE USERS DROP CONSTRAINT IF EXISTS USERS_PHONE_KEY;
ALTER TABLE AUTHENTICATION DROP CONSTRAINT IF EXISTS AUTHENTICATION_USERNAME_KEY;

CREATE UNIQUE INDEX UX_USERS_EMAIL_ACTIVE ON USERS(EMAIL) WHERE ACTIVE;
CREATE UNIQUE INDEX UX_USERS_PHONE_ACTIVE ON USERS(PHONE) WHERE ACTIVE;
CREATE UNIQUE INDEX UX_AUTHENTICATION_USERNAME_ACTIVE ON AUTHENTICATION(USERNAME) WHERE ACTIVE;
