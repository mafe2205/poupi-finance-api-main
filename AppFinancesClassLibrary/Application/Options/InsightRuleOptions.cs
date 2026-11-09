namespace AppFinancesClassLibrary.Application.Options
{
    public class InsightRuleOptions
    {
        public decimal CategoryLimitThresholdPercent { get; set; } = 100m;
        public decimal WeekOverWeekIncreaseThresholdPercent { get; set; } = 50m;
        public int MinCategorySpendCentsToConsider { get; set; } = 1000;
        public int MinHistoryDays { get; set; } = 14;
        public int OneMonthHistoryDays { get; set; } = 30;
        public int TwoMonthsHistoryDays { get; set; } = 60;
        public GoalRuleOptions Goals { get; set; } = new();
        public CardRuleOptions Cards { get; set; } = new();
        public AccountRuleOptions Accounts { get; set; } = new();
        public FlowRuleOptions Flow { get; set; } = new();
        public BudgetRuleOptions Budget { get; set; } = new();
        public UsageRuleOptions Usage { get; set; } = new();
    }

    public class GoalRuleOptions
    {
        public GoalDelayOptions Atraso { get; set; } = new();
        public GoalDeadlineOptions Vencimento { get; set; } = new();
        public GoalContributionOptions Aporte { get; set; } = new();
        public GoalNearOptions Proxima { get; set; } = new();
        public GoalCompetitionOptions Concorrencia { get; set; } = new();
    }

    public class GoalDelayOptions
    {
        /// pontos percentuais de folga entre o progresso e o prazo decorrido.</summary>
        public decimal FolgaPp { get; set; } = 15m;
    }

    public class GoalDeadlineOptions
    {
        /// a partir de quantos dias antes do prazo a meta é avaliada.</summary>
        public int DiasAlerta { get; set; } = 30;
    }

    public class GoalContributionOptions
    {
        /// dias sem aporte para a meta ser considerada parada.</summary>
        public int DiasSemAporte { get; set; } = 30;
    }

    public class GoalNearOptions
    {
        /// progresso (%) a partir do qual a meta é considerada quase atingida.</summary>
        public decimal PercentualAviso { get; set; } = 90m;
    }

    public class GoalCompetitionOptions
    {
        /// fração do aporte mensal necessário que o aumento de gasto precisa passar.</summary>
        public decimal FracaoAporte { get; set; } = 0.5m;
    }

    public class CardRuleOptions
    {
        public CardLimitOptions Limite { get; set; } = new();
        public CardDueOptions Vencimento { get; set; } = new();
        public CardComparisonOptions Comparacao { get; set; } = new();
        public CardProjectionOptions Projecao { get; set; } = new();
        public CardConcentrationOptions Concentracao { get; set; } = new();
        public CardUnusedOptions SemUso { get; set; } = new();
    }

    public class CardLimitOptions
    {
        /// níveis (% do limite) em que a fatura aberta gera alerta
        public int[] Niveis { get; set; }

        public IReadOnlyList<int> EffectiveNiveis => Niveis is { Length: > 0 } ? Niveis.Order().ToArray() : [80, 100];
    }

    public class CardDueOptions
    {
        ///  dias antes do vencimento em que a fatura não paga gera lembrete
        public int DiasAviso { get; set; } = 3;

        /// só avisa faturas vencidas há no máximo esse número de dias, para não acusar faturas antigas
        public int DiasVencidaAviso { get; set; } = 7;
    }

    public class CardComparisonOptions
    {
        /// aumento mínimo (fração) da fatura fechada sobre a anterior
        public decimal Aumento { get; set; } = 0.30m;

        /// a fatura anterior precisa passar desse valor
        public int FaturaAnteriorMinimaCentavos { get; set; } = 5000;
    }

    public class CardProjectionOptions
    {
        /// dias mínimos decorridos no ciclo antes de projetar
        public int DiasMinimosNoCiclo { get; set; } = 7;

        /// transações mínimas no ciclo antes de projetar
        public int TransacoesMinimas { get; set; } = 5;
    }

    public class CardConcentrationOptions
    {
        /// fração do gasto em cartões concentrada em um único cartão
        public decimal Percentual { get; set; } = 0.70m;

        /// uso máximo (fração do limite) do outro cartão para ele contar como folgado
        public decimal UsoMaximoOutroCartao { get; set; } = 0.30m;

        /// transações mínimas em cartão no mês
        public int TransacoesMinimas { get; set; } = 10;
    }

    public class CardUnusedOptions
    {
        /// dias sem transação para o cartão ser considerado sem uso
        public int Dias { get; set; } = 60;
    }

    public class AccountRuleOptions
    {
        public AccountLowBalanceOptions SaldoBaixo { get; set; } = new();
        public AccountLinkedStatementOptions FaturaVinculada { get; set; } = new();
        public AccountIdleBalanceOptions SaldoParado { get; set; } = new();
    }

    public class AccountLowBalanceOptions
    {
        /// dias de gasto que o saldo precisa cobrir
        public int DiasCobertura { get; set; } = 7;
    }

    public class AccountLinkedStatementOptions
    {
        /// dias antes do vencimento em que o saldo é comparado com a fatura
        public int DiasAviso { get; set; } = 5;
    }

    public class AccountIdleBalanceOptions
    {
        /// múltiplo do gasto mensal médio que o saldo precisa manter
        public decimal Multiplo { get; set; } = 3m;

        /// janela (dias) em que o saldo precisa ficar parado
        public int DiasParado { get; set; } = 60;
    }

    public class FlowRuleOptions
    {
        /// semanas fechadas seguidas com saídas maiores que entradas
        public int DeficitSemanas { get; set; } = 2;

        public FlowSavingsOptions Poupanca { get; set; } = new();
        public FlowIncomeDropOptions QuedaReceita { get; set; } = new();
        public FlowOutlierOptions ForaPadrao { get; set; } = new();
        public FlowRecurringOptions Recorrente { get; set; } = new();
        public FlowConcentrationOptions Concentracao { get; set; } = new();
        public FlowCriticalDayOptions DiaCritico { get; set; } = new();
        public FlowSpendDropOptions QuedaGasto { get; set; } = new();
    }

    public class FlowSavingsOptions
    {
        /// taxa de poupança mínima (fração da renda)
        public decimal TaxaMinima { get; set; } = 0.10m;
    }

    public class FlowIncomeDropOptions
    {
        /// queda mínima (fração) da renda do mês sobre a média dos 3 anteriores
        public decimal Percentual { get; set; } = 0.20m;
    }

    public class FlowOutlierOptions
    {
        /// quantos desvios-padrão acima da média
        public decimal K { get; set; } = 2m;

        /// transações abaixo desse valor são ignoradas
        public int ValorMinimoCentavos { get; set; } = 5000;
    }

    public class FlowRecurringOptions
    {
        /// ocorrências mensais seguidas para considerar o gasto recorrente
        public int Ocorrencias { get; set; } = 3;

        /// variação máxima (fração) entre os valores das ocorrências
        public decimal Tolerancia { get; set; } = 0.10m;
    }

    public class FlowConcentrationOptions
    {
        /// fração das saídas do mês concentrada em uma categoria
        public decimal Percentual { get; set; } = 0.50m;

        /// nomes de categorias estruturais (ex.: "Moradia") que não entram na regra
        public string[] CategoriasExcluidas { get; set; }
    }

    public class FlowCriticalDayOptions
    {
        /// fração das saídas concentrada em um dia da semana
        public decimal Percentual { get; set; } = 0.40m;

        /// meses fechados analisados
        public int MesesAnalisados { get; set; } = 2;
    }

    public class FlowSpendDropOptions
    {
        /// queda mínima (fração) do gasto da categoria sobre a semana anterior
        public decimal Percentual { get; set; } = 0.30m;
    }

    public class BudgetRuleOptions
    {
        public BudgetWarningOptions Aviso { get; set; } = new();
        public BudgetUndersizedOptions Subdimensionado { get; set; } = new();
        public BudgetNoLimitOptions SemLimite { get; set; } = new();
    }

    public class BudgetWarningOptions
    {
        /// fração do limite mensal em que o aviso antecipado dispara
        public decimal Nivel { get; set; } = 0.80m;
    }

    public class BudgetUndersizedOptions
    {
        /// meses fechados seguidos acima do limite
        public int MesesSeguidos { get; set; } = 2;
    }

    public class BudgetNoLimitOptions
    {
        /// quantas categorias de maior gasto são avaliadas
        public int TopN { get; set; } = 3;
    }

    public class UsageRuleOptions
    {
        public UsagePendingDocsOptions DocsPendentes { get; set; } = new();
        public UsageUncategorizedOptions SemCategoria { get; set; } = new();
        public UsageNoRecordsOptions SemRegistros { get; set; } = new();
    }

    public class UsagePendingDocsOptions
    {
        /// idade (dias) do documento pendente para gerar o lembrete
        public int Dias { get; set; } = 2;
    }

    public class UsageUncategorizedOptions
    {
        /// fração das transações do mês em categorias genéricas
        public decimal Percentual { get; set; } = 0.20m;

        /// nomes de categoria tratados como "sem categoria definida". O banco não tem um flag RequiresConfirmation, então a regra olha só para essas categorias genéricas
        public string[] CategoriasGenericas { get; set; }

        public IReadOnlyList<string> EffectiveCategoriasGenericas => CategoriasGenericas is { Length: > 0 }
            ? CategoriasGenericas
            : ["Outros", "Outras", "Outro", "Diversos", "Geral", "Sem categoria"];
    }

    public class UsageNoRecordsOptions
    {
        /// dias sem registrar transação
        public int Dias { get; set; } = 5;
    }
}
