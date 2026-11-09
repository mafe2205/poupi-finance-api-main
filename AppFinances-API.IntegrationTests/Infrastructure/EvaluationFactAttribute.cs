namespace AppFinances_API.IntegrationTests.Infrastructure
{
    public sealed class EvaluationFactAttribute : FactAttribute
    {
        public const string Variable = "AVALIACAO_OCR";

        public EvaluationFactAttribute()
        {
            if (Environment.GetEnvironmentVariable(Variable) != "1")
                Skip = $"Avaliação do OCR: defina {Variable}=1 para rodar.";
        }
    }
}
