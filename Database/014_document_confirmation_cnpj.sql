-- =====================================================================
-- Incremental migration for databases already created from init.sql.
-- Safe to run once against an existing AppFinances database.
-- =====================================================================

-- Store CNPJ read in the receipt header (14 digits, check digits valid),
-- NULL when not found. With the transaction category it is the user history
-- the classifier uses for the next receipt of the same store. Rows confirmed
-- before this migration stay NULL and are matched by the store name only.
ALTER TABLE DOCUMENT_CONFIRMATIONS
    ADD COLUMN IF NOT EXISTS ESTABLISHMENT_CNPJ TEXT;
