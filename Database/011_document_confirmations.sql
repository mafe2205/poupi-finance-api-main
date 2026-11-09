-- =====================================================================
-- Incremental migration for databases already created from init.sql.
-- Safe to run once against an existing AppFinances database.
-- =====================================================================

-- What the OCR suggested next to what the user confirmed, one row per
-- confirmed document, so the OCR accuracy can be measured field by field.
-- SUGGESTED_* is NULL when the OCR could not read that field.
-- SUGGESTED_CATEGORY_REASON stores CategoryClassificationReason ordinal.
-- ITEMS_CONFIRMED is FALSE when the confirmation did not send the items
-- (the user did not review them): DOCUMENT_CONFIRMED_ITEMS stays empty.
CREATE TABLE IF NOT EXISTS DOCUMENT_CONFIRMATIONS (
    ID                              BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    DOCUMENT_ID                     BIGINT NOT NULL UNIQUE REFERENCES DOCUMENTS(ID),
    TRANSACTION_ID                  BIGINT NOT NULL REFERENCES TRANSACTIONS(ID),
    SUGGESTED_DESCRIPTION           TEXT,
    CONFIRMED_DESCRIPTION           TEXT NOT NULL,
    SUGGESTED_PAYMENT_VALUE_CENTS   INTEGER,
    CONFIRMED_PAYMENT_VALUE_CENTS   INTEGER NOT NULL,
    SUGGESTED_TRANSACTION_DATE      DATE,
    CONFIRMED_TRANSACTION_DATE      DATE NOT NULL,
    SUGGESTED_CATEGORY_ID           BIGINT REFERENCES CATEGORIES(ID),
    SUGGESTED_CATEGORY_REASON       INTEGER NOT NULL,
    CONFIRMED_CATEGORY_ID           BIGINT NOT NULL REFERENCES CATEGORIES(ID),
    SUGGESTED_ITEMS_TOTAL_CENTS     INTEGER,
    ITEMS_CONFIRMED                 BOOLEAN NOT NULL,
    CREATED_AT                      TIMESTAMPTZ NOT NULL DEFAULT now(),
    UPDATED_AT                      TIMESTAMPTZ NOT NULL DEFAULT now()
);

-- Items as confirmed by the user. The suggested ones stay in DOCUMENT_ITEMS.
-- SUGGESTED_ITEM_ID points at the DOCUMENT_ITEMS row the user kept or
-- corrected; NULL means the user added an item the OCR did not read.
-- Suggested items missing here were removed by the user.
CREATE TABLE IF NOT EXISTS DOCUMENT_CONFIRMED_ITEMS (
    ID                  BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    DOCUMENT_ID         BIGINT NOT NULL REFERENCES DOCUMENTS(ID),
    SUGGESTED_ITEM_ID   BIGINT REFERENCES DOCUMENT_ITEMS(ID) ON DELETE SET NULL,
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

CREATE INDEX IF NOT EXISTS IX_DOCUMENT_CONFIRMED_ITEMS_DOCUMENT_ID ON DOCUMENT_CONFIRMED_ITEMS(DOCUMENT_ID);
