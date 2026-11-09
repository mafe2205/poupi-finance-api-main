-- =====================================================================
-- Dados de teste para usar o aplicativo (NÃO rodar em produção).
--
-- Cria um usuário com ~3 meses de histórico, com datas relativas ao dia
-- em que o script roda:
--   login:  poupi.teste
--   senha:  Teste@123
--
-- Pode rodar mais de uma vez: apaga antes tudo do usuário de teste
-- (email teste@poupi.com) e recria. Nenhum outro usuário é tocado.
--
--   psql "postgresql://postgres:SENHA@localhost:5433/appfinances" -f seed_dados_teste.sql
-- =====================================================================

BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;  -- crypt()/gen_salt('bf') = hash BCrypt, lido pelo BCrypt.Net

DO $$
BEGIN
    IF to_regclass('bank_statement_payments') IS NULL THEN
        RAISE EXCEPTION 'Rode antes a migração Database/018_bank_statement_payments.sql';
    END IF;
END $$;

-- ---------------------------------------------------------------------
-- 1) Limpa o usuário de teste anterior
-- ---------------------------------------------------------------------
CREATE TEMP TABLE seed_old_users ON COMMIT DROP AS
    SELECT ID FROM USERS WHERE EMAIL = 'teste@poupi.com';

DELETE FROM DOCUMENT_CONFIRMED_ITEMS WHERE DOCUMENT_ID IN (SELECT ID FROM DOCUMENTS WHERE USER_ID IN (SELECT ID FROM seed_old_users));
DELETE FROM DOCUMENT_CONFIRMATIONS   WHERE DOCUMENT_ID IN (SELECT ID FROM DOCUMENTS WHERE USER_ID IN (SELECT ID FROM seed_old_users));
DELETE FROM DOCUMENT_ITEMS           WHERE DOCUMENT_ID IN (SELECT ID FROM DOCUMENTS WHERE USER_ID IN (SELECT ID FROM seed_old_users));
DELETE FROM DOCUMENTS                WHERE USER_ID IN (SELECT ID FROM seed_old_users);
DELETE FROM BEHAVIORAL_INSIGHTS      WHERE USER_ID IN (SELECT ID FROM seed_old_users);
DELETE FROM NOTIFICATIONS            WHERE USER_ID IN (SELECT ID FROM seed_old_users);
DELETE FROM DEVICE_TOKENS            WHERE USER_ID IN (SELECT ID FROM seed_old_users);
DELETE FROM WEEKLY_PROFILE_METRICS   WHERE USER_ID IN (SELECT ID FROM seed_old_users);
DELETE FROM TRANSACTIONS             WHERE USER_ID IN (SELECT ID FROM seed_old_users);
DELETE FROM BANK_STATEMENT_PAYMENTS WHERE BANK_ACCOUNT_ID IN (SELECT ID FROM BANK_ACCOUNTS WHERE USER_ID IN (SELECT ID FROM seed_old_users));
DELETE FROM BANK_STATEMENTS WHERE CARD_ID IN (
    SELECT c.ID FROM CARDS c JOIN BANK_ACCOUNTS b ON b.ID = c.BANK_ACCOUNT_ID WHERE b.USER_ID IN (SELECT ID FROM seed_old_users));
DELETE FROM CARDS WHERE BANK_ACCOUNT_ID IN (SELECT ID FROM BANK_ACCOUNTS WHERE USER_ID IN (SELECT ID FROM seed_old_users));
DELETE FROM GOALS                    WHERE USER_ID IN (SELECT ID FROM seed_old_users);
DELETE FROM CATEGORIES               WHERE USER_ID IN (SELECT ID FROM seed_old_users);
DELETE FROM BANK_ACCOUNTS            WHERE USER_ID IN (SELECT ID FROM seed_old_users);
DELETE FROM AUTHENTICATION           WHERE USER_ID IN (SELECT ID FROM seed_old_users);
DELETE FROM USERS                    WHERE ID IN (SELECT ID FROM seed_old_users);

-- ---------------------------------------------------------------------
-- 2) Funções auxiliares (só existem nesta sessão)
-- ---------------------------------------------------------------------

-- Fatura do cartão para uma data, com a mesma regra de
-- TransactionsService.ResolveBillingCycle. Cria a fatura se não existir.
CREATE FUNCTION pg_temp.seed_statement(p_card BIGINT, p_date DATE) RETURNS BIGINT AS $$
DECLARE
    v_closing INT; v_due INT;
    v_cycle DATE; v_due_month DATE;
    v_closing_date DATE; v_due_date DATE;
    v_ref TEXT; v_id BIGINT; v_status INT; v_paid DATE;
BEGIN
    SELECT CLOSING_DATE, DUE_DATE INTO v_closing, v_due FROM CARDS WHERE ID = p_card;

    v_cycle := date_trunc('month', p_date)::DATE;
    IF extract(day FROM p_date) > v_closing THEN v_cycle := (v_cycle + INTERVAL '1 month')::DATE; END IF;

    v_closing_date := v_cycle + (LEAST(v_closing, extract(day FROM (v_cycle + INTERVAL '1 month' - INTERVAL '1 day'))::INT) - 1);
    v_due_month := CASE WHEN v_due <= v_closing THEN (v_cycle + INTERVAL '1 month')::DATE ELSE v_cycle END;
    v_due_date := v_due_month + (LEAST(v_due, extract(day FROM (v_due_month + INTERVAL '1 month' - INTERVAL '1 day'))::INT) - 1);
    v_ref := to_char(v_cycle, 'YYYY-MM');

    SELECT ID INTO v_id FROM BANK_STATEMENTS WHERE CARD_ID = p_card AND MONTH_REFERENCE = v_ref;
    IF v_id IS NOT NULL THEN RETURN v_id; END IF;

    -- aberta = 0, fechada = 1, paga = 2
    IF v_due_date < CURRENT_DATE THEN v_status := 2; v_paid := v_due_date - 1;
    ELSIF v_closing_date < CURRENT_DATE THEN v_status := 1;
    ELSE v_status := 0;
    END IF;

    INSERT INTO BANK_STATEMENTS (CARD_ID, MONTH_REFERENCE, CLOSING_DATE, DUE_DATE, PAYMENT_DATE, STATUS)
    VALUES (p_card, v_ref, v_closing_date, v_due_date, v_paid, v_status)
    RETURNING ID INTO v_id;
    RETURN v_id;
END $$ LANGUAGE plpgsql;

-- Transação em conta (pix = 3, dinheiro = 2, boleto = 4, débito = 1 sem cartão).
CREATE FUNCTION pg_temp.seed_tx_account(
    p_user BIGINT, p_category BIGINT, p_account BIGINT, p_description TEXT, p_method INT,
    p_value INT, p_date DATE,
    p_meta BIGINT DEFAULT NULL, p_meta_direction INT DEFAULT NULL,
    p_recorrent_months INT DEFAULT NULL, p_serial BIGINT DEFAULT NULL) RETURNS BIGINT AS $$
DECLARE v_id BIGINT;
BEGIN
    INSERT INTO TRANSACTIONS (USER_ID, CATEGORY_ID, META_ID, META_DIRECTION, BANK_ACCOUNT_ID, DESCRIPTION,
                              PAYMENT_METHOD, PAYMENT_VALUE_CENTS, RECORRENT, RECORRENT_MONTHS, RECORRENT_SERIAL_ID,
                              TRANSACTION_DATE, PAYMENT_DATE, SOURCE, CREATED_AT, UPDATED_AT)
    VALUES (p_user, p_category, p_meta, p_meta_direction, p_account, p_description,
            p_method, p_value, p_serial IS NOT NULL AND p_recorrent_months IS NOT NULL, p_recorrent_months, p_serial,
            p_date, CASE WHEN p_date <= CURRENT_DATE THEN p_date END, 0,
            LEAST(p_date, CURRENT_DATE)::TIMESTAMPTZ + INTERVAL '12 hours', LEAST(p_date, CURRENT_DATE)::TIMESTAMPTZ + INTERVAL '12 hours')
    RETURNING ID INTO v_id;
    RETURN v_id;
END $$ LANGUAGE plpgsql;

-- Transação no cartão de crédito (= 0): entra na fatura do ciclo, não mexe no saldo.
CREATE FUNCTION pg_temp.seed_tx_credit(
    p_user BIGINT, p_category BIGINT, p_card BIGINT, p_description TEXT,
    p_value INT, p_date DATE, p_statement_date DATE DEFAULT NULL,
    p_parcels INT DEFAULT 1, p_recorrent_months INT DEFAULT NULL, p_serial BIGINT DEFAULT NULL) RETURNS BIGINT AS $$
DECLARE v_id BIGINT; v_statement BIGINT;
BEGIN
    v_statement := pg_temp.seed_statement(p_card, COALESCE(p_statement_date, p_date));

    INSERT INTO TRANSACTIONS (USER_ID, CATEGORY_ID, CARD_ID, BANK_STATEMENT_ID, DESCRIPTION,
                              PAYMENT_METHOD, PAYMENT_PARCELS, PAYMENT_VALUE_CENTS, RECORRENT, RECORRENT_MONTHS, RECORRENT_SERIAL_ID,
                              TRANSACTION_DATE, EXPIRING_DATE, SOURCE, CREATED_AT, UPDATED_AT)
    VALUES (p_user, p_category, p_card, v_statement, p_description,
            0, p_parcels, p_value, p_recorrent_months IS NOT NULL, p_recorrent_months, p_serial,
            p_date, (SELECT DUE_DATE FROM BANK_STATEMENTS WHERE ID = v_statement), 0,
            LEAST(p_date, CURRENT_DATE)::TIMESTAMPTZ + INTERVAL '12 hours', LEAST(p_date, CURRENT_DATE)::TIMESTAMPTZ + INTERVAL '12 hours')
    RETURNING ID INTO v_id;
    RETURN v_id;
END $$ LANGUAGE plpgsql;

-- ---------------------------------------------------------------------
-- 3) Dados
-- ---------------------------------------------------------------------
DO $$
DECLARE
    v_start DATE := CURRENT_DATE - 92;      -- começo do histórico
    v_user BIGINT;
    v_carteira BIGINT; v_itau BIGINT; v_nubank BIGINT;
    v_card_nu BIGINT; v_card_itau BIGINT; v_card_itau_deb BIGINT;
    v_cat_metas BIGINT; v_cat_salario BIGINT; v_cat_outras_rec BIGINT;
    v_cat_mercado BIGINT; v_cat_restaurante BIGINT; v_cat_moradia BIGINT; v_cat_transporte BIGINT;
    v_cat_combustivel BIGINT; v_cat_saude BIGINT; v_cat_farmacia BIGINT; v_cat_educacao BIGINT;
    v_cat_lazer BIGINT; v_cat_vestuario BIGINT; v_cat_outros BIGINT; v_cat_pets BIGINT; v_cat_assinaturas BIGINT;
    v_goal_viagem BIGINT; v_goal_reserva BIGINT; v_goal_notebook BIGINT; v_goal_carro BIGINT;
    v_serial BIGINT; v_tx BIGINT; v_first_paid BOOLEAN := TRUE;
    r RECORD;
    v_month DATE; v_d DATE; i INT;
BEGIN
    PERFORM setseed(0.42);  -- valores "aleatórios" sempre iguais

    -- Usuário -----------------------------------------------------------
    INSERT INTO USERS (NAME, SURNAME, BIRTHDATE, EMAIL, PHONE, CREATED_AT, UPDATED_AT)
    VALUES ('Ana', 'Teste', DATE '1998-04-15', 'teste@poupi.com', '+5511990000001', v_start, v_start)
    RETURNING ID INTO v_user;

    INSERT INTO AUTHENTICATION (USER_ID, USERNAME, PASSWORD_HASH, ROLE, CREATED_AT, UPDATED_AT)
    VALUES (v_user, 'poupi.teste', crypt('Teste@123', gen_salt('bf', 11)), 0, v_start, v_start);

    -- Contas (saldo calculado no fim) --------------------------------------
    INSERT INTO BANK_ACCOUNTS (USER_ID, BANK_NAME, AGENCY, ACCOUNT_NUMBER, IS_DEFAULT, CREATED_AT, UPDATED_AT)
    VALUES (v_user, 'Carteira', '-', '-', TRUE, v_start, v_start) RETURNING ID INTO v_carteira;
    INSERT INTO BANK_ACCOUNTS (USER_ID, BANK_NAME, AGENCY, ACCOUNT_NUMBER, CREATED_AT, UPDATED_AT)
    VALUES (v_user, 'Itaú', '0341', '12345-6', v_start, v_start) RETURNING ID INTO v_itau;
    INSERT INTO BANK_ACCOUNTS (USER_ID, BANK_NAME, AGENCY, ACCOUNT_NUMBER, CREATED_AT, UPDATED_AT)
    VALUES (v_user, 'Nubank', '0001', '9876543-2', v_start, v_start) RETURNING ID INTO v_nubank;

    -- Cartões (crédito = 0, débito = 1) ---------------------------------
    INSERT INTO CARDS (BANK_ACCOUNT_ID, NAME, TYPE, LIMIT_VALUE_CENTS, CLOSING_DATE, DUE_DATE, CREATED_AT, UPDATED_AT)
    VALUES (v_nubank, 'Nubank Roxinho', 0, 300000, 3, 10, v_start, v_start) RETURNING ID INTO v_card_nu;
    INSERT INTO CARDS (BANK_ACCOUNT_ID, NAME, TYPE, LIMIT_VALUE_CENTS, CLOSING_DATE, DUE_DATE, CREATED_AT, UPDATED_AT)
    VALUES (v_itau, 'Itaú Click', 0, 150000, 25, 5, v_start, v_start) RETURNING ID INTO v_card_itau;
    INSERT INTO CARDS (BANK_ACCOUNT_ID, NAME, TYPE, LIMIT_VALUE_CENTS, CLOSING_DATE, DUE_DATE, CREATED_AT, UPDATED_AT)
    VALUES (v_itau, 'Itaú Débito', 1, 0, 1, 1, v_start, v_start) RETURNING ID INTO v_card_itau_deb;

    -- Categorias (Entrada = 0, Saida = 1) --------------------------------
    INSERT INTO CATEGORIES (USER_ID, NAME, TYPE, IS_DEFAULT, CREATED_AT, UPDATED_AT) VALUES (v_user, 'Metas', 1, TRUE, v_start, v_start) RETURNING ID INTO v_cat_metas;
    INSERT INTO CATEGORIES (USER_ID, NAME, TYPE, CREATED_AT, UPDATED_AT) VALUES (v_user, 'Salário', 0, v_start, v_start) RETURNING ID INTO v_cat_salario;
    INSERT INTO CATEGORIES (USER_ID, NAME, TYPE, CREATED_AT, UPDATED_AT) VALUES (v_user, 'Outras receitas', 0, v_start, v_start) RETURNING ID INTO v_cat_outras_rec;
    INSERT INTO CATEGORIES (USER_ID, NAME, TYPE, LIMIT_VALUE_CENTS, CREATED_AT, UPDATED_AT) VALUES (v_user, 'Mercado', 1, 90000, v_start, v_start) RETURNING ID INTO v_cat_mercado;
    INSERT INTO CATEGORIES (USER_ID, NAME, TYPE, LIMIT_VALUE_CENTS, CREATED_AT, UPDATED_AT) VALUES (v_user, 'Restaurante', 1, 35000, v_start, v_start) RETURNING ID INTO v_cat_restaurante;
    INSERT INTO CATEGORIES (USER_ID, NAME, TYPE, CREATED_AT, UPDATED_AT) VALUES (v_user, 'Moradia', 1, v_start, v_start) RETURNING ID INTO v_cat_moradia;
    INSERT INTO CATEGORIES (USER_ID, NAME, TYPE, LIMIT_VALUE_CENTS, CREATED_AT, UPDATED_AT) VALUES (v_user, 'Transporte', 1, 25000, v_start, v_start) RETURNING ID INTO v_cat_transporte;
    INSERT INTO CATEGORIES (USER_ID, NAME, TYPE, CREATED_AT, UPDATED_AT) VALUES (v_user, 'Combustível', 1, v_start, v_start) RETURNING ID INTO v_cat_combustivel;
    INSERT INTO CATEGORIES (USER_ID, NAME, TYPE, CREATED_AT, UPDATED_AT) VALUES (v_user, 'Saúde', 1, v_start, v_start) RETURNING ID INTO v_cat_saude;
    INSERT INTO CATEGORIES (USER_ID, NAME, TYPE, CREATED_AT, UPDATED_AT) VALUES (v_user, 'Farmácia', 1, v_start, v_start) RETURNING ID INTO v_cat_farmacia;
    INSERT INTO CATEGORIES (USER_ID, NAME, TYPE, CREATED_AT, UPDATED_AT) VALUES (v_user, 'Educação', 1, v_start, v_start) RETURNING ID INTO v_cat_educacao;
    INSERT INTO CATEGORIES (USER_ID, NAME, TYPE, LIMIT_VALUE_CENTS, CREATED_AT, UPDATED_AT) VALUES (v_user, 'Lazer', 1, 30000, v_start, v_start) RETURNING ID INTO v_cat_lazer;
    INSERT INTO CATEGORIES (USER_ID, NAME, TYPE, CREATED_AT, UPDATED_AT) VALUES (v_user, 'Vestuário', 1, v_start, v_start) RETURNING ID INTO v_cat_vestuario;
    INSERT INTO CATEGORIES (USER_ID, NAME, TYPE, CREATED_AT, UPDATED_AT) VALUES (v_user, 'Outros', 1, v_start, v_start) RETURNING ID INTO v_cat_outros;
    INSERT INTO CATEGORIES (USER_ID, NAME, TYPE, LIMIT_VALUE_CENTS, CREATED_AT, UPDATED_AT) VALUES (v_user, 'Pets', 1, 20000, v_start, v_start) RETURNING ID INTO v_cat_pets;
    INSERT INTO CATEGORIES (USER_ID, NAME, TYPE, CREATED_AT, UPDATED_AT) VALUES (v_user, 'Assinaturas', 1, v_start, v_start) RETURNING ID INTO v_cat_assinaturas;
    -- Uma categoria desativada, para testar a listagem/reativação
    INSERT INTO CATEGORIES (USER_ID, NAME, TYPE, ACTIVE, DELETED_AT, CREATED_AT, UPDATED_AT)
    VALUES (v_user, 'Academia', 1, FALSE, CURRENT_DATE - 20, v_start, CURRENT_DATE - 20);

    -- Metas -------------------------------------------------------------
    INSERT INTO GOALS (USER_ID, INICIAL_DATE, FINAL_DATE, INICIAL_VALUE_CENTS, FINAL_VALUE_CENTS, DESCRIPTION, CREATED_AT, UPDATED_AT)
    VALUES (v_user, v_start, CURRENT_DATE + 240, 50000, 800000, 'Viagem para o Chile', v_start, v_start) RETURNING ID INTO v_goal_viagem;
    -- atrasada: prazo correndo e pouco guardado
    INSERT INTO GOALS (USER_ID, INICIAL_DATE, FINAL_DATE, INICIAL_VALUE_CENTS, FINAL_VALUE_CENTS, DESCRIPTION, CREATED_AT, UPDATED_AT)
    VALUES (v_user, v_start, CURRENT_DATE + 60, 0, 1500000, 'Reserva de emergência', v_start, v_start) RETURNING ID INTO v_goal_reserva;
    -- quase lá, vencendo em breve
    INSERT INTO GOALS (USER_ID, INICIAL_DATE, FINAL_DATE, INICIAL_VALUE_CENTS, FINAL_VALUE_CENTS, DESCRIPTION, CREATED_AT, UPDATED_AT)
    VALUES (v_user, v_start, CURRENT_DATE + 20, 200000, 450000, 'Notebook novo', v_start, v_start) RETURNING ID INTO v_goal_notebook;
    -- desativada
    INSERT INTO GOALS (USER_ID, INICIAL_DATE, FINAL_DATE, INICIAL_VALUE_CENTS, FINAL_VALUE_CENTS, DESCRIPTION, ACTIVE, DELETED_AT, CREATED_AT, UPDATED_AT)
    VALUES (v_user, v_start, CURRENT_DATE + 365, 0, 3000000, 'Entrada do carro', FALSE, CURRENT_DATE - 30, v_start, CURRENT_DATE - 30) RETURNING ID INTO v_goal_carro;

    -- Mensais (4 meses: 3 anteriores + o atual) -------------------------
    FOR i IN 0..3 LOOP
        v_month := (date_trunc('month', CURRENT_DATE) - (3 - i) * INTERVAL '1 month')::DATE;

        v_d := v_month + 4;   -- dia 5
        IF v_d BETWEEN v_start AND CURRENT_DATE THEN
            PERFORM pg_temp.seed_tx_account(v_user, v_cat_salario, v_itau, 'Salário Empresa XYZ', 3, 750000, v_d);
        END IF;

        v_d := v_month + 9;   -- dia 10
        IF v_d BETWEEN v_start AND CURRENT_DATE THEN
            PERFORM pg_temp.seed_tx_account(v_user, v_cat_moradia, v_itau, 'Aluguel', 4, 165000, v_d);
            PERFORM pg_temp.seed_tx_account(v_user, v_cat_moradia, v_itau, 'Conta de luz Enel', 4, 15000 + (random() * 10000)::INT, v_d + 2);
            PERFORM pg_temp.seed_tx_account(v_user, v_cat_moradia, v_itau, 'Conta de água Sabesp', 4, 7000 + (random() * 3000)::INT, v_d + 3);
            -- aporte nas metas
            PERFORM pg_temp.seed_tx_account(v_user, v_cat_metas, v_itau, 'Guardar para a viagem', 3, 60000, v_d + 1, v_goal_viagem, 0);
            PERFORM pg_temp.seed_tx_account(v_user, v_cat_metas, v_itau, 'Guardar para o notebook', 3, 70000, v_d + 1, v_goal_notebook, 0);
        END IF;

        v_d := v_month + 19;  -- dia 20
        IF v_d BETWEEN v_start AND CURRENT_DATE THEN
            PERFORM pg_temp.seed_tx_account(v_user, v_cat_saude, v_itau, 'Plano de saúde Unimed', 4, 38000, v_d);
        END IF;
    END LOOP;

    -- Reserva de emergência: um aporte e uma retirada (atrasada)
    PERFORM pg_temp.seed_tx_account(v_user, v_cat_metas, v_itau, 'Começando a reserva', 3, 100000, v_start + 10, v_goal_reserva, 0);
    PERFORM pg_temp.seed_tx_account(v_user, v_cat_metas, v_itau, 'Retirada para conserto do carro', 3, 40000, v_start + 50, v_goal_reserva, 1);

    -- Recorrentes (a série inteira, inclusive os meses futuros, como o app faz)
    v_serial := nextval('TRANSACTIONS_RECORRENT_SERIAL_SEQ');
    FOR i IN 0..11 LOOP
        v_d := (v_start + 3 + i * INTERVAL '1 month')::DATE;
        PERFORM pg_temp.seed_tx_credit(v_user, v_cat_assinaturas, v_card_nu, 'Netflix', 5590, v_d, NULL, 1, 12, v_serial);
    END LOOP;

    v_serial := nextval('TRANSACTIONS_RECORRENT_SERIAL_SEQ');
    FOR i IN 0..11 LOOP
        v_d := (v_start + 8 + i * INTERVAL '1 month')::DATE;
        PERFORM pg_temp.seed_tx_credit(v_user, v_cat_assinaturas, v_card_nu, 'Spotify', 2190, v_d, NULL, 1, 12, v_serial);
    END LOOP;

    v_serial := nextval('TRANSACTIONS_RECORRENT_SERIAL_SEQ');
    FOR i IN 0..5 LOOP
        v_d := (v_start + 15 + i * INTERVAL '1 month')::DATE;
        PERFORM pg_temp.seed_tx_credit(v_user, v_cat_educacao, v_card_itau, 'Curso de inglês online', 19900, v_d, NULL, 1, 6, v_serial);
    END LOOP;

    v_serial := nextval('TRANSACTIONS_RECORRENT_SERIAL_SEQ');
    FOR i IN 0..11 LOOP
        v_d := (v_start + 12 + i * INTERVAL '1 month')::DATE;
        PERFORM pg_temp.seed_tx_account(v_user, v_cat_moradia, v_nubank, 'Internet Vivo Fibra', 3, 9990, v_d, NULL, NULL, 12, v_serial);
    END LOOP;

    -- Parceladas: todas as parcelas com a data da compra, cada uma na fatura do seu mês
    v_serial := nextval('TRANSACTIONS_RECORRENT_SERIAL_SEQ');
    v_d := v_start + 25;
    FOR i IN 0..2 LOOP
        PERFORM pg_temp.seed_tx_credit(v_user, v_cat_vestuario, v_card_itau, format('Tênis Centauro (%s/3)', i + 1),
                                       CASE WHEN i = 2 THEN 15000 ELSE 14999 END, v_d, (v_d + i * INTERVAL '1 month')::DATE, 3, NULL, v_serial);
    END LOOP;

    v_serial := nextval('TRANSACTIONS_RECORRENT_SERIAL_SEQ');
    v_d := CURRENT_DATE - 40;
    FOR i IN 0..9 LOOP
        PERFORM pg_temp.seed_tx_credit(v_user, v_cat_outros, v_card_nu, format('Celular Samsung (%s/10)', i + 1),
                                       21990, v_d, (v_d + i * INTERVAL '1 month')::DATE, 10, NULL, v_serial);
    END LOOP;

    -- Dia a dia -----------------------------------------------------------
    v_d := v_start;
    WHILE v_d <= CURRENT_DATE LOOP
        -- mercado a cada 4 dias, alternando cartão e débito
        IF (v_d - v_start) % 4 = 0 THEN
            IF random() < 0.6 THEN
                PERFORM pg_temp.seed_tx_credit(v_user, v_cat_mercado, v_card_nu,
                    (ARRAY['Supermercado Pão de Açúcar','Carrefour','Assaí Atacadista','Dia Supermercado'])[1 + (random() * 3)::INT],
                    12000 + (random() * 25000)::INT, v_d);
            ELSE
                INSERT INTO TRANSACTIONS (USER_ID, CATEGORY_ID, CARD_ID, DESCRIPTION, PAYMENT_METHOD, PAYMENT_VALUE_CENTS,
                                          TRANSACTION_DATE, PAYMENT_DATE, SOURCE, CREATED_AT, UPDATED_AT)
                VALUES (v_user, v_cat_mercado, v_card_itau_deb, 'Hortifruti', 1, 4000 + (random() * 8000)::INT,
                        v_d, v_d, 0, v_d + INTERVAL '12 hours', v_d + INTERVAL '12 hours');
            END IF;
        END IF;

        -- restaurante/delivery: ~2x por semana, bem mais nas últimas 2 semanas
        IF random() < (CASE WHEN v_d > CURRENT_DATE - 14 THEN 0.65 ELSE 0.28 END) THEN
            IF random() < 0.5 THEN
                PERFORM pg_temp.seed_tx_credit(v_user, v_cat_restaurante, v_card_nu,
                    (ARRAY['iFood','Outback','Madero','Coco Bambu','Rappi'])[1 + (random() * 4)::INT],
                    3500 + (random() * 9000)::INT, v_d);
            ELSE
                PERFORM pg_temp.seed_tx_account(v_user, v_cat_restaurante, v_nubank,
                    (ARRAY['Padaria Real','Almoço no trabalho','Lanchonete do Zé'])[1 + (random() * 2)::INT],
                    3,
                    1800 + (random() * 3500)::INT, v_d);
            END IF;
        END IF;

        -- uber: ~2x por semana
        IF random() < 0.3 THEN
            PERFORM pg_temp.seed_tx_account(v_user, v_cat_transporte, v_nubank,
                (ARRAY['Uber','99 Táxi'])[1 + (random())::INT], 3, 1400 + (random() * 3500)::INT, v_d);
        END IF;

        -- combustível a cada 9 dias
        IF (v_d - v_start) % 9 = 2 THEN
            PERFORM pg_temp.seed_tx_credit(v_user, v_cat_combustivel, v_card_itau,
                (ARRAY['Posto Shell','Posto Ipiranga'])[1 + (random())::INT], 18000 + (random() * 7000)::INT, v_d);
        END IF;

        -- farmácia em dinheiro, a cada 15 dias
        IF (v_d - v_start) % 15 = 7 THEN
            PERFORM pg_temp.seed_tx_account(v_user, v_cat_farmacia, v_carteira,
                (ARRAY['Drogasil','Droga Raia','Pague Menos'])[1 + (random() * 2)::INT], 2, 2500 + (random() * 7000)::INT, v_d);
        END IF;

        -- lazer nos fins de semana
        IF extract(isodow FROM v_d) IN (6, 7) AND random() < 0.35 THEN
            PERFORM pg_temp.seed_tx_credit(v_user, v_cat_lazer, v_card_nu,
                (ARRAY['Cinemark','Show no Allianz Parque','Boliche','Ingresso teatro'])[1 + (random() * 3)::INT],
                4000 + (random() * 12000)::INT, v_d);
        END IF;

        -- pets a cada 20 dias
        IF (v_d - v_start) % 20 = 11 THEN
            PERFORM pg_temp.seed_tx_account(v_user, v_cat_pets, v_nubank, 'Petz ração e areia', 3, 9000 + (random() * 6000)::INT, v_d);
        END IF;

        v_d := v_d + 1;
    END LOOP;

    -- Avulsas -------------------------------------------------------------
    PERFORM pg_temp.seed_tx_account(v_user, v_cat_outras_rec, v_carteira, 'Dinheiro do aniversário', 2, 40000, v_start + 1);
    PERFORM pg_temp.seed_tx_account(v_user, v_cat_outras_rec, v_nubank, 'Freela de design', 3, 85000, v_start + 38);
    PERFORM pg_temp.seed_tx_account(v_user, v_cat_outras_rec, v_nubank, 'Venda no Enjoei', 3, 12000, CURRENT_DATE - 6);
    PERFORM pg_temp.seed_tx_account(v_user, v_cat_saude, v_itau, 'Consulta dentista', 3, 25000, v_start + 33);
    PERFORM pg_temp.seed_tx_credit(v_user, v_cat_vestuario, v_card_nu, 'Renner', 18990, CURRENT_DATE - 9);
    PERFORM pg_temp.seed_tx_account(v_user, v_cat_outros, v_carteira, 'Presente da mãe', 2, 12000, v_start + 60);

    -- Uma transação excluída (soft delete), para conferir que não aparece
    v_tx := pg_temp.seed_tx_account(v_user, v_cat_lazer, v_nubank, 'Compra cancelada', 3, 9900, CURRENT_DATE - 3);
    UPDATE TRANSACTIONS SET ACTIVE = FALSE, DELETED_AT = now() WHERE ID = v_tx;

    -- Pagamentos das faturas ----------------------------------------------
    -- Faturas pagas: quitadas na véspera do vencimento, da conta do salário (Itaú).
    -- A primeira paga do Nubank é quitada em duas partes, para ver a lista de pagamentos.
    FOR r IN
        SELECT s.ID, s.CARD_ID, s.PAYMENT_DATE, s.DUE_DATE, tot.TOTAL
        FROM BANK_STATEMENTS s
        JOIN (SELECT BANK_STATEMENT_ID, SUM(PAYMENT_VALUE_CENTS)::INT AS TOTAL
              FROM TRANSACTIONS WHERE USER_ID = v_user AND DELETED_AT IS NULL AND BANK_STATEMENT_ID IS NOT NULL
              GROUP BY BANK_STATEMENT_ID) tot ON tot.BANK_STATEMENT_ID = s.ID
        WHERE s.CARD_ID IN (v_card_nu, v_card_itau) AND s.STATUS = 2
        ORDER BY s.CARD_ID, s.MONTH_REFERENCE
    LOOP
        IF r.CARD_ID = v_card_nu AND v_first_paid THEN
            v_first_paid := FALSE;
            INSERT INTO BANK_STATEMENT_PAYMENTS (BANK_STATEMENT_ID, BANK_ACCOUNT_ID, AMOUNT_CENTS, PAYMENT_DATE, CREATED_AT, UPDATED_AT)
            VALUES (r.ID, v_nubank, r.TOTAL * 4 / 10, r.DUE_DATE - 6, (r.DUE_DATE - 6) + INTERVAL '12 hours', (r.DUE_DATE - 6) + INTERVAL '12 hours'),
                   (r.ID, v_itau, r.TOTAL - r.TOTAL * 4 / 10, r.PAYMENT_DATE, r.PAYMENT_DATE + INTERVAL '12 hours', r.PAYMENT_DATE + INTERVAL '12 hours');
        ELSE
            INSERT INTO BANK_STATEMENT_PAYMENTS (BANK_STATEMENT_ID, BANK_ACCOUNT_ID, AMOUNT_CENTS, PAYMENT_DATE, CREATED_AT, UPDATED_AT)
            VALUES (r.ID, v_itau, r.TOTAL, r.PAYMENT_DATE, r.PAYMENT_DATE + INTERVAL '12 hours', r.PAYMENT_DATE + INTERVAL '12 hours');
        END IF;
    END LOOP;

    -- Faturas fechadas e ainda não vencidas: metade já paga, para ver o "falta pagar".
    FOR r IN
        SELECT s.ID, tot.TOTAL
        FROM BANK_STATEMENTS s
        JOIN (SELECT BANK_STATEMENT_ID, SUM(PAYMENT_VALUE_CENTS)::INT AS TOTAL
              FROM TRANSACTIONS WHERE USER_ID = v_user AND DELETED_AT IS NULL AND BANK_STATEMENT_ID IS NOT NULL
              GROUP BY BANK_STATEMENT_ID) tot ON tot.BANK_STATEMENT_ID = s.ID
        WHERE s.CARD_ID IN (v_card_nu, v_card_itau) AND s.STATUS = 1 AND tot.TOTAL > 1
    LOOP
        INSERT INTO BANK_STATEMENT_PAYMENTS (BANK_STATEMENT_ID, BANK_ACCOUNT_ID, AMOUNT_CENTS, PAYMENT_DATE, CREATED_AT, UPDATED_AT)
        VALUES (r.ID, v_itau, r.TOTAL / 2, CURRENT_DATE - 1, now() - INTERVAL '1 day', now() - INTERVAL '1 day');
    END LOOP;

    -- Saldos: saldo inicial + tudo que não foi no crédito - pagamentos de fatura
    -- (débito com cartão mexe na conta do cartão; meta: depósito sai, retirada volta)
    UPDATE BANK_ACCOUNTS b
    SET BALANCE_CENTS = s.OPENING + COALESCE(s.DELTA, 0) - s.STATEMENT_PAYMENTS
    FROM (
        SELECT b2.ID,
               CASE b2.BANK_NAME WHEN 'Itaú' THEN 350000 WHEN 'Nubank' THEN 300000 ELSE 5000 END AS OPENING,
               (SELECT COALESCE(SUM(p.AMOUNT_CENTS), 0) FROM BANK_STATEMENT_PAYMENTS p WHERE p.BANK_ACCOUNT_ID = b2.ID) AS STATEMENT_PAYMENTS,
               (SELECT SUM(CASE
                              WHEN t.META_DIRECTION = 1 THEN t.PAYMENT_VALUE_CENTS
                              WHEN t.META_DIRECTION = 0 THEN -t.PAYMENT_VALUE_CENTS
                              WHEN c.TYPE = 0 THEN t.PAYMENT_VALUE_CENTS
                              ELSE -t.PAYMENT_VALUE_CENTS END)
                FROM TRANSACTIONS t
                JOIN CATEGORIES c ON c.ID = t.CATEGORY_ID
                LEFT JOIN CARDS k ON k.ID = t.CARD_ID
                WHERE t.DELETED_AT IS NULL
                  AND t.PAYMENT_METHOD <> 0
                  AND t.TRANSACTION_DATE <= CURRENT_DATE
                  AND COALESCE(t.BANK_ACCOUNT_ID, k.BANK_ACCOUNT_ID) = b2.ID) AS DELTA
        FROM BANK_ACCOUNTS b2
        WHERE b2.USER_ID = v_user
    ) s
    WHERE b.ID = s.ID;

    -- Notificações (insight_generated = 0, invoice_due = 1, goal_reached = 2)
    INSERT INTO NOTIFICATIONS (USER_ID, TYPE, TITLE, MESSAGE, RELATED_ENTITY_ID, READ_AT, CREATED_AT, UPDATED_AT) VALUES
        (v_user, 1, 'Fatura vencendo', 'A fatura do Nubank Roxinho vence em 3 dias.', v_card_nu, NULL, now() - INTERVAL '1 day', now() - INTERVAL '1 day'),
        (v_user, 0, 'Novo insight', 'Seus gastos com Restaurante subiram nas últimas semanas.', NULL, NULL, now() - INTERVAL '2 hours', now() - INTERVAL '2 hours'),
        (v_user, 1, 'Fatura fechada', 'A fatura do Itaú Click fechou.', v_card_itau, now() - INTERVAL '5 days', now() - INTERVAL '6 days', now() - INTERVAL '5 days'),
        (v_user, 2, 'Meta quase lá!', 'Você já guardou mais de 90% do Notebook novo.', v_goal_notebook, now() - INTERVAL '10 days', now() - INTERVAL '12 days', now() - INTERVAL '10 days');

    -- Insights antigos, sem regra (como os criados pela API)
    INSERT INTO BEHAVIORAL_INSIGHTS (USER_ID, CATEGORY_ID, TYPE, DESCRIPTION, REFERENCE_DATE, WAS_INTERACTED, VIEWED_AT, FEEDBACK, CREATED_AT, UPDATED_AT) VALUES
        (v_user, v_cat_mercado, 'padrao_detectado', 'Você costuma ir ao mercado a cada 4 dias. Compras maiores e menos frequentes podem sair mais baratas.', CURRENT_DATE - 30, TRUE, now() - INTERVAL '29 days', 'util', now() - INTERVAL '30 days', now() - INTERVAL '29 days'),
        (v_user, v_cat_assinaturas, 'sugestao', 'Você tem 2 assinaturas de streaming somando R$ 77,80 por mês. Vale revisar se usa as duas.', CURRENT_DATE - 15, FALSE, now() - INTERVAL '14 days', NULL, now() - INTERVAL '15 days', now() - INTERVAL '14 days');

    RAISE NOTICE 'Usuário de teste criado: id %, login poupi.teste / Teste@123', v_user;
END $$;

COMMIT;

-- Resumo do que foi criado
SELECT 'transacoes' AS item, count(*) FROM TRANSACTIONS WHERE USER_ID = (SELECT ID FROM USERS WHERE EMAIL = 'teste@poupi.com' AND ACTIVE)
UNION ALL SELECT 'faturas', count(*) FROM BANK_STATEMENTS s JOIN CARDS c ON c.ID = s.CARD_ID JOIN BANK_ACCOUNTS b ON b.ID = c.BANK_ACCOUNT_ID
    WHERE b.USER_ID = (SELECT ID FROM USERS WHERE EMAIL = 'teste@poupi.com' AND ACTIVE)
UNION ALL SELECT 'pagamentos de fatura', count(*) FROM BANK_STATEMENT_PAYMENTS p JOIN BANK_ACCOUNTS b ON b.ID = p.BANK_ACCOUNT_ID
    WHERE b.USER_ID = (SELECT ID FROM USERS WHERE EMAIL = 'teste@poupi.com' AND ACTIVE)
UNION ALL SELECT 'conta ' || BANK_NAME || ' (R$)', BALANCE_CENTS / 100 FROM BANK_ACCOUNTS
    WHERE USER_ID = (SELECT ID FROM USERS WHERE EMAIL = 'teste@poupi.com' AND ACTIVE);
