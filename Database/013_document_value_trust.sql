-- =====================================================================
-- Incremental migration for databases already created from init.sql.
-- Safe to run once against an existing AppFinances database.
-- =====================================================================

-- Lowest OCR confidence (0-1) among the digits of the payment value found
-- in the text. TRUST_OCR averages the whole image and stays high even when
-- one digit of the value was misread; this one looks only at the value.
-- NULL when no value was found or the OCR gave no per-character confidence.
ALTER TABLE DOCUMENTS
    ADD COLUMN IF NOT EXISTS VALUE_TRUST_OCR NUMERIC(5,4);
