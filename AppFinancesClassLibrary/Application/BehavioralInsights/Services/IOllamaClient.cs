namespace AppFinancesClassLibrary.Application.BehavioralInsights.Services
{
    public interface IOllamaClient
    {
        Task<string> GenerateInsightTextAsync(string prompt, CancellationToken cancellationToken = default);
    }
}
