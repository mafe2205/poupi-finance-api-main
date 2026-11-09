-- =====================================================================
-- Incremental migration for databases already created from init.sql.
-- Safe to run once against an existing AppFinances database.
-- =====================================================================

-- Names of the CNPJ in the public registry: the trade name (nome
-- fantasia) and the legal name (razao social). The rows cached before this
-- migration have no names and are looked up again.
ALTER TABLE CNPJ_ACTIVITIES
    ADD COLUMN IF NOT EXISTS TRADE_NAME TEXT,
    ADD COLUMN IF NOT EXISTS LEGAL_NAME TEXT;

DELETE FROM CNPJ_ACTIVITIES WHERE CNAE IS NOT NULL AND TRADE_NAME IS NULL AND LEGAL_NAME IS NULL;

-- Registry name of the store CNPJ (trade name, or legal name without one),
-- saved when the OCR finishes. Used as the establishment when the OCR did
-- not read it and the user has no confirmed name for that store.
ALTER TABLE DOCUMENTS
    ADD COLUMN IF NOT EXISTS ESTABLISHMENT_REGISTRY_NAME TEXT;

-- Where the suggested establishment came from (DocumentDescriptionSource
-- ordinal: 0 OCR, 1 user history, 2 CNPJ registry); NULL when there was
-- none. Keeps the OCR accuracy measurable apart from the filled names.
ALTER TABLE DOCUMENT_CONFIRMATIONS
    ADD COLUMN IF NOT EXISTS SUGGESTED_DESCRIPTION_SOURCE INTEGER;
