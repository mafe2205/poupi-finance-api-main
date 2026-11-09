-- =====================================================================
-- Apoio à conferência manual do OCR (seções 2.4.3 e 2.4.4 do TCC).
-- Não é migração: só lê os dados. Rodar no psql ou no DBeaver e copiar
-- o resultado para a planilha do gabarito.
--
-- Filtro: troque NULL pelo ID do usuário do experimento em "params"
-- (NULL considera todos os usuários).
--
-- A sugestão do OCR fica gravada em DOCUMENT_CONFIRMATIONS quando o
-- comprovante é confirmado. Antes disso, a sugestão e os motivos de
-- revisão aparecem em GET /api/documents/{id} (campos "suggestion" e
-- "reviewReasons").
-- =====================================================================


-- ---------------------------------------------------------------------
-- 1. Uma linha por comprovante.
--    STATUS: 0 pendente, 1 processado, 2 erro.
--    Tempos em segundos:
--      fila    = CREATED_AT -> OCR_STARTED_AT (espera na fila em segundo plano)
--      leitura = OCR_STARTED_AT -> OCR_FINISHED_AT (pré-processamento + OCR + itens)
--      total   = CREATED_AT -> OCR_FINISHED_AT (do envio até a conclusão da leitura)
--    Colunas sugerido_* e confirmado_* ficam vazias enquanto o comprovante
--    não for confirmado.
-- ---------------------------------------------------------------------
WITH params AS (SELECT NULL::BIGINT AS user_id)
SELECT d.ID AS documento,
    d.USER_ID AS usuario,
    d.CREATED_AT AS enviado_em,
    d.STATUS AS status,
    d.TRUST_OCR AS trust_ocr,
    ROUND(EXTRACT(EPOCH FROM d.OCR_STARTED_AT - d.CREATED_AT)::NUMERIC, 2) AS fila_s,
    ROUND(EXTRACT(EPOCH FROM d.OCR_FINISHED_AT - d.OCR_STARTED_AT)::NUMERIC, 2) AS leitura_s,
    ROUND(EXTRACT(EPOCH FROM d.OCR_FINISHED_AT - d.CREATED_AT)::NUMERIC, 2) AS total_s,
    d.TRANSACTION_ID IS NOT NULL AS confirmado,
    c.SUGGESTED_PAYMENT_VALUE_CENTS / 100.0 AS sugerido_valor,
    c.CONFIRMED_PAYMENT_VALUE_CENTS / 100.0 AS confirmado_valor,
    c.SUGGESTED_TRANSACTION_DATE AS sugerido_data,
    c.CONFIRMED_TRANSACTION_DATE AS confirmado_data,
    c.SUGGESTED_DESCRIPTION AS sugerido_descricao,
    c.CONFIRMED_DESCRIPTION AS confirmado_descricao,
    sc.NAME AS sugerido_categoria,
    cc.NAME AS confirmado_categoria,
    c.SUGGESTED_ITEMS_TOTAL_CENTS / 100.0 AS sugerido_soma_itens,
    (SELECT COUNT(*) FROM DOCUMENT_ITEMS i WHERE i.DOCUMENT_ID = d.ID) AS itens_lidos
FROM DOCUMENTS d
CROSS JOIN params p
LEFT JOIN DOCUMENT_CONFIRMATIONS c ON c.DOCUMENT_ID = d.ID
LEFT JOIN CATEGORIES sc ON sc.ID = c.SUGGESTED_CATEGORY_ID
LEFT JOIN CATEGORIES cc ON cc.ID = c.CONFIRMED_CATEGORY_ID
WHERE d.ACTIVE AND (p.user_id IS NULL OR d.USER_ID = p.user_id)
ORDER BY d.ID;


-- ---------------------------------------------------------------------
-- 2. Resumo dos tempos de processamento, em segundos.
--    Só entram comprovantes cuja leitura começou.
-- ---------------------------------------------------------------------
WITH params AS (SELECT NULL::BIGINT AS user_id),
timings AS (
    SELECT d.STATUS,
        EXTRACT(EPOCH FROM d.OCR_STARTED_AT - d.CREATED_AT) AS fila,
        EXTRACT(EPOCH FROM d.OCR_FINISHED_AT - d.OCR_STARTED_AT) AS leitura,
        EXTRACT(EPOCH FROM d.OCR_FINISHED_AT - d.CREATED_AT) AS total
    FROM DOCUMENTS d
    CROSS JOIN params p
    WHERE d.ACTIVE AND d.OCR_STARTED_AT IS NOT NULL
        AND (p.user_id IS NULL OR d.USER_ID = p.user_id)
)
SELECT COUNT(*) AS comprovantes,
    COUNT(*) FILTER (WHERE STATUS = 2) AS com_erro,
    ROUND(AVG(fila)::NUMERIC, 2) AS fila_media,
    ROUND(AVG(leitura)::NUMERIC, 2) AS leitura_media,
    ROUND(PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY leitura)::NUMERIC, 2) AS leitura_mediana,
    ROUND(MAX(leitura)::NUMERIC, 2) AS leitura_maxima,
    ROUND(AVG(total)::NUMERIC, 2) AS total_medio,
    ROUND(PERCENTILE_CONT(0.5) WITHIN GROUP (ORDER BY total)::NUMERIC, 2) AS total_mediana,
    ROUND(PERCENTILE_CONT(0.95) WITHIN GROUP (ORDER BY total)::NUMERIC, 2) AS total_p95,
    ROUND(MAX(total)::NUMERIC, 2) AS total_maximo
FROM timings;
