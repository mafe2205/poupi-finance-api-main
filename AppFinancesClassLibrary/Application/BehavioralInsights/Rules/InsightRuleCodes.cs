namespace AppFinancesClassLibrary.Application.BehavioralInsights.Rules
{
    public static class InsightRuleCodes
    {
        public const string LimiteCategoria = "LIMITE_CATEGORIA";
        public const string AumentoSemanal = "AUMENTO_SEMANAL";

        // Metas
        public const string MetaAtrasada = "META_ATRASADA";
        public const string MetaRiscoPrazo = "META_RISCO_PRAZO";
        public const string AporteNecessario = "APORTE_NECESSARIO";
        public const string MetaQuaseAtingida = "META_QUASE_ATINGIDA";
        public const string MetaConcluida = "META_CONCLUIDA";
        public const string GastoConcorreMeta = "GASTO_CONCORRE_META";

        // Cartões
        public const string FaturaLimitePrefix = "FATURA_LIMITE_";
        public const string VencimentoProximo = "VENCIMENTO_PROXIMO";
        public const string FaturaVencida = "FATURA_VENCIDA";
        public const string FaturaMaior = "FATURA_MAIOR";
        public const string ProjecaoFatura = "PROJECAO_FATURA";
        public const string ConcentracaoCartao = "CONCENTRACAO_CARTAO";
        public const string CartaoSemUso = "CARTAO_SEM_USO";

        // Contas
        public const string SaldoBaixo = "SALDO_BAIXO";
        public const string SaldoInsuficienteFatura = "SALDO_INSUFICIENTE_FATURA";
        public const string SaldoParado = "SALDO_PARADO";

        // Fluxo
        public const string GastosMaioresReceitas = "GASTOS_MAIORES_RECEITAS";
        public const string PoupancaBaixa = "POUPANCA_BAIXA";
        public const string QuedaReceita = "QUEDA_RECEITA";
        public const string GastoForaPadrao = "GASTO_FORA_PADRAO";
        public const string GastoRecorrente = "GASTO_RECORRENTE";
        public const string ConcentracaoGasto = "CONCENTRACAO_GASTO";
        public const string DiaSemanaCritico = "DIA_SEMANA_CRITICO";
        public const string QuedaGasto = "QUEDA_GASTO";

        // Orçamento
        public const string AvisoLimite = "AVISO_LIMITE";
        public const string LimiteSubdimensionado = "LIMITE_SUBDIMENSIONADO";
        public const string CategoriaSemLimite = "CATEGORIA_SEM_LIMITE";

        // Uso do app
        public const string DocumentosPendentes = "DOCUMENTOS_PENDENTES";
        public const string MuitasSemCategoria = "MUITAS_SEM_CATEGORIA";
        public const string SemRegistros = "SEM_REGISTROS";

        public const string DadosInsuficientes = "DADOS_INSUFICIENTES";
    }
}
