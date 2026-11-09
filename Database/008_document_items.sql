-- =====================================================================
-- Incremental migration for databases already created from init.sql.
-- Safe to run once against an existing AppFinances database.
-- =====================================================================

-- Each line item read by the OCR from a receipt (cupom fiscal / NFC-e).
-- Filled by DocumentUploadService right after the OCR runs; documents
-- processed before this migration have no items.
--   QUANTITY          may be fractional (e.g. 0,532 KG).
--   UNIT_PRICE_CENTS  price per unit, rounded to cents.
--   TOTAL_CENTS       line total as printed, or QUANTITY x UNIT_PRICE when
--                     the OCR did not read it.
CREATE TABLE IF NOT EXISTS DOCUMENT_ITEMS (
    ID                  BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    DOCUMENT_ID         BIGINT NOT NULL REFERENCES DOCUMENTS(ID) ON DELETE CASCADE,
    LINE_NUMBER         INTEGER NOT NULL,
    CODE                TEXT,
    DESCRIPTION         TEXT NOT NULL,
    QUANTITY            NUMERIC(12,3) NOT NULL,
    UNIT                TEXT,
    UNIT_PRICE_CENTS    INTEGER NOT NULL,
    TOTAL_CENTS         INTEGER NOT NULL,
    CREATED_AT          TIMESTAMPTZ NOT NULL DEFAULT now(),
    UPDATED_AT          TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS IX_DOCUMENT_ITEMS_DOCUMENT_ID ON DOCUMENT_ITEMS(DOCUMENT_ID);
