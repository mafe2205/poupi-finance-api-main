-- =====================================================================
-- Incremental migration for databases already created from init.sql.
-- Safe to run once against an existing AppFinances database.
-- =====================================================================

-- The store key also identifies Pix recipients ("cpf:" plus the six digits
-- the masked CPF shows), not only CNPJs.
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_name = 'document_confirmations' AND column_name = 'establishment_cnpj') THEN
        ALTER TABLE DOCUMENT_CONFIRMATIONS RENAME COLUMN ESTABLISHMENT_CNPJ TO ESTABLISHMENT_KEY;
    END IF;
END $$;

ALTER TABLE DOCUMENT_CONFIRMATIONS
    ADD COLUMN IF NOT EXISTS ESTABLISHMENT_KEY TEXT;

-- Main activity (CNAE) of the store CNPJ, looked up when the OCR finishes.
ALTER TABLE DOCUMENTS
    ADD COLUMN IF NOT EXISTS ESTABLISHMENT_CNAE INTEGER;

-- Cache of the public registry lookup: each CNPJ is looked up once.
CREATE TABLE IF NOT EXISTS CNPJ_ACTIVITIES (
    CNPJ                TEXT PRIMARY KEY,
    CNAE                INTEGER,
    DESCRIPTION         TEXT,
    FETCHED_AT          TIMESTAMPTZ NOT NULL DEFAULT now()
);
