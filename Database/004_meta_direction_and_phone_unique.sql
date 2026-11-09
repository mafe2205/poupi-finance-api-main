-- =====================================================================
-- Incremental migration for databases already created from init.sql.
-- Safe to run once against an existing AppFinances database.
-- =====================================================================

-- 1) Lets a transaction linked to the Metas category (META_ID) say whether
--    it's a deposit toward the goal or a withdrawal out of it, since that
--    category has a single fixed Type (Saida) and can't tell them apart on
--    its own. 0 = deposito, 1 = retirada (GoalContributionDirection ordinal).
ALTER TABLE TRANSACTIONS
    ADD COLUMN IF NOT EXISTS META_DIRECTION INTEGER;

ALTER TABLE TRANSACTIONS
    ADD CONSTRAINT TRANSACTIONS_META_ID_DIRECTION_CHECK CHECK ((META_ID IS NULL) = (META_DIRECTION IS NULL));

-- 2) PAYMENT_VALUE_CENTS was never validated as positive; a negative value
--    could silently flip which way a transaction moves the account balance.
-- If any existing rows are <= 0, fix/remove them before running this — the
-- ALTER TABLE below fails otherwise.
ALTER TABLE TRANSACTIONS
    ADD CONSTRAINT TRANSACTIONS_PAYMENT_VALUE_CENTS_POSITIVE_CHECK CHECK (PAYMENT_VALUE_CENTS > 0);

-- 3) Two users could otherwise register with the same phone number; email
--    and username already reject duplicates via a UNIQUE constraint.
-- If any existing rows already share a phone number (or are all NULL,
-- which never conflicts under UNIQUE), fix/deduplicate them first.
ALTER TABLE USERS
    ADD CONSTRAINT USERS_PHONE_KEY UNIQUE (PHONE);
