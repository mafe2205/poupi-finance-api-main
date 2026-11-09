-- =====================================================================
-- Incremental migration for databases already created from init.sql.
-- Safe to run once against an existing AppFinances database.
-- =====================================================================

-- Processing time of each receipt, kept apart from UPDATED_AT (which the
-- confirmation overwrites later):
--   CREATED_AT      -> OCR_STARTED_AT   time waiting in the background queue
--   OCR_STARTED_AT  -> OCR_FINISHED_AT  preprocessing + OCR + item parsing
-- OCR_FINISHED_AT stays NULL when the reading failed (STATUS = erro).
ALTER TABLE DOCUMENTS
    ADD COLUMN IF NOT EXISTS OCR_STARTED_AT TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS OCR_FINISHED_AT TIMESTAMPTZ;
