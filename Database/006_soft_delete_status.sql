-- =====================================================================
-- Incremental migration for databases already created from init.sql.
-- Safe to run once against an existing AppFinances database.
-- =====================================================================

-- Deleting a bank account, card, category, goal or transaction no longer
-- removes the row: it only turns ACTIVE off and stamps DELETED_AT, same as
-- USERS. Inactive rows are hidden from the API but keep the history of the
-- transactions that point at them.

ALTER TABLE BANK_ACCOUNTS
    ADD COLUMN IF NOT EXISTS ACTIVE BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS DELETED_AT TIMESTAMPTZ;

ALTER TABLE CARDS
    ADD COLUMN IF NOT EXISTS ACTIVE BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS DELETED_AT TIMESTAMPTZ;

ALTER TABLE CATEGORIES
    ADD COLUMN IF NOT EXISTS ACTIVE BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS DELETED_AT TIMESTAMPTZ;

ALTER TABLE GOALS
    ADD COLUMN IF NOT EXISTS ACTIVE BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS DELETED_AT TIMESTAMPTZ;

-- TRANSACTIONS already had DELETED_AT; ACTIVE mirrors it.
ALTER TABLE TRANSACTIONS
    ADD COLUMN IF NOT EXISTS ACTIVE BOOLEAN NOT NULL DEFAULT TRUE;

UPDATE TRANSACTIONS SET ACTIVE = FALSE WHERE DELETED_AT IS NOT NULL;
