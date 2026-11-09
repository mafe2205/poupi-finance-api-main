namespace AppFinancesClassLibrary.Application.BehavioralInsights.Services
{
    public interface IActionTakenBatchCalculator
    {
        Task RunAsync(CancellationToken cancellationToken = default);
    }
}
