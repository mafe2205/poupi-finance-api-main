-- =====================================================================
-- Incremental migration for databases already created from init.sql.
-- Safe to run more than once against an existing AppFinances database.
-- =====================================================================

-- Basic categories that every new user gets at sign-up
-- (StarterCategories.cs). Users created before that get them here.
-- A category is skipped when the user already has one with the same name
-- (ignoring case and surrounding spaces), active or deleted: nothing is
-- duplicated and a category the user deleted does not come back.
-- TYPE is the CategoriesTypes ordinal: 0 Entrada, 1 Saida.
INSERT INTO CATEGORIES (USER_ID, NAME, TYPE, IS_DEFAULT, LIMIT_VALUE_CENTS)
SELECT u.ID, s.NAME, s.TYPE, FALSE, 0
FROM USERS u
CROSS JOIN (VALUES
    ('Salário', 0),
    ('Outras receitas', 0),
    ('Mercado', 1),
    ('Restaurante', 1),
    ('Moradia', 1),
    ('Transporte', 1),
    ('Combustível', 1),
    ('Saúde', 1),
    ('Farmácia', 1),
    ('Educação', 1),
    ('Lazer', 1),
    ('Vestuário', 1),
    ('Outros', 1)
) AS s(NAME, TYPE)
WHERE u.ACTIVE
  AND NOT EXISTS (
      SELECT 1 FROM CATEGORIES c
      WHERE c.USER_ID = u.ID
        AND lower(trim(c.NAME)) = lower(s.NAME)
  );
