-- =====================================================================
-- Incremental migration for databases already created from init.sql.
-- Safe to run once against an existing AppFinances database.
-- =====================================================================

-- The new insight rules (regras_insights.pdf) are about goals, cards,
-- accounts and the whole cash flow, not only categories, so an insight
-- needs to say which rule produced it and which entity it is about.
-- Both columns are nullable: insights created before this migration, or
-- through POST /api/BehavioralInsights, have neither.
--   RULE_CODE          InsightRuleCodes value (e.g. META_ATRASADA).
--   RELATED_ENTITY_ID  goal, card, bank account or transaction id,
--                      depending on the rule.
ALTER TABLE BEHAVIORAL_INSIGHTS
    ADD COLUMN IF NOT EXISTS RULE_CODE TEXT,
    ADD COLUMN IF NOT EXISTS RELATED_ENTITY_ID BIGINT;

-- Dedupe key: UserId + RuleCode + RelatedEntityId + CategoryId + reference
-- period. The jobs already skip duplicates in code; the index protects
-- against two runs racing. Rows without RULE_CODE are left out.
CREATE UNIQUE INDEX IF NOT EXISTS UX_BEHAVIORAL_INSIGHTS_RULE_DEDUPE
    ON BEHAVIORAL_INSIGHTS (USER_ID, RULE_CODE, COALESCE(RELATED_ENTITY_ID, 0), COALESCE(CATEGORY_ID, 0), REFERENCE_DATE)
    WHERE RULE_CODE IS NOT NULL;
