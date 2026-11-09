-- =====================================================================
-- Incremental migration for databases already created from init.sql.
-- Safe to run once against an existing AppFinances database.
-- =====================================================================

-- Deleting a document (receipt) no longer removes the row: it only turns
-- ACTIVE off and stamps DELETED_AT, same as the other financial records.
-- The OCR text, its items and the stored image are kept.
ALTER TABLE DOCUMENTS
    ADD COLUMN IF NOT EXISTS ACTIVE BOOLEAN NOT NULL DEFAULT TRUE,
    ADD COLUMN IF NOT EXISTS DELETED_AT TIMESTAMPTZ;

-- The idempotency key only blocks active documents, so the same photo can
-- be sent again after its document was deleted.
DROP INDEX IF EXISTS UX_DOCUMENTS_USER_IDEMPOTENCY_KEY;
CREATE UNIQUE INDEX UX_DOCUMENTS_USER_IDEMPOTENCY_KEY
    ON DOCUMENTS (USER_ID, IDEMPOTENCY_KEY)
    WHERE IDEMPOTENCY_KEY IS NOT NULL AND ACTIVE;
