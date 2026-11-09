-- =====================================================================
-- Incremental migration for databases already created from init.sql.
-- Safe to run once against an existing AppFinances database.
-- =====================================================================

-- Resending the same photo (flaky mobile network) must not create a second
-- document: the app sends a key generated when the photo is taken, and the
-- upload returns the existing document when the key was already used.
ALTER TABLE DOCUMENTS
    ADD COLUMN IF NOT EXISTS IDEMPOTENCY_KEY TEXT;

CREATE UNIQUE INDEX IF NOT EXISTS UX_DOCUMENTS_USER_IDEMPOTENCY_KEY
    ON DOCUMENTS (USER_ID, IDEMPOTENCY_KEY)
    WHERE IDEMPOTENCY_KEY IS NOT NULL;

-- FALSE only when the category came from the automatic classifier and the
-- user accepted it unchanged; the reclassification routine only touches
-- those rows. Every existing row was categorized by the user: TRUE.
ALTER TABLE TRANSACTIONS
    ADD COLUMN IF NOT EXISTS CATEGORY_CONFIRMED_MANUALLY BOOLEAN NOT NULL DEFAULT TRUE;

-- An insight whose numbers changed (or that no longer applies) after a
-- transaction was created, edited or deleted is kept, marked as outdated,
-- and a new one is generated. Keeping the old row preserves ViewedAt and
-- the ActionTaken window.
--   FACTS            numbers sent to the LLM, one per line; compared to
--                    detect that the analysis changed.
--   OUTDATED_AT      when it stopped reflecting the user's data.
--   OUTDATED_REASON  dados_alterados | nao_se_aplica.
ALTER TABLE BEHAVIORAL_INSIGHTS
    ADD COLUMN IF NOT EXISTS FACTS TEXT,
    ADD COLUMN IF NOT EXISTS OUTDATED_AT TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS OUTDATED_REASON TEXT;

-- The dedupe key now only applies to current insights, so the replacement
-- of an outdated insight can reuse its rule/entity/period.
DROP INDEX IF EXISTS UX_BEHAVIORAL_INSIGHTS_RULE_DEDUPE;
CREATE UNIQUE INDEX UX_BEHAVIORAL_INSIGHTS_RULE_DEDUPE
    ON BEHAVIORAL_INSIGHTS (USER_ID, RULE_CODE, COALESCE(RELATED_ENTITY_ID, 0), COALESCE(CATEGORY_ID, 0), REFERENCE_DATE)
    WHERE RULE_CODE IS NOT NULL AND OUTDATED_AT IS NULL;
