-- =====================================================================
-- Incremental migration for databases already created from init.sql.
-- Safe to run once against an existing AppFinances database.
-- =====================================================================

-- Paying a credit card statement used to only stamp PAYMENT_DATE: no account
-- balance changed. Each payment is now a row here, taken out of the balance
-- of BANK_ACCOUNT_ID, and a statement can be paid in parts. The statement
-- turns "paga" (STATUS = 2) when the payments reach its total; PAYMENT_DATE
-- is then the date of the payment that completed it.
CREATE TABLE IF NOT EXISTS BANK_STATEMENT_PAYMENTS (
    ID                  BIGINT GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    BANK_STATEMENT_ID   BIGINT NOT NULL REFERENCES BANK_STATEMENTS(ID),
    BANK_ACCOUNT_ID     BIGINT NOT NULL REFERENCES BANK_ACCOUNTS(ID),
    AMOUNT_CENTS        INTEGER NOT NULL CHECK (AMOUNT_CENTS > 0),
    PAYMENT_DATE        DATE NOT NULL,
    CREATED_AT          TIMESTAMPTZ NOT NULL DEFAULT now(),
    UPDATED_AT          TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS IX_BANK_STATEMENT_PAYMENTS_STATEMENT_ID ON BANK_STATEMENT_PAYMENTS(BANK_STATEMENT_ID);

-- Statements already marked as paid have no payment row: nothing was taken
-- out of any balance for them, and none is taken now. Their remaining value
-- is reported as zero because STATUS is "paga".
