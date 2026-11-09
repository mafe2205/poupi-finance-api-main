using AppFinancesClassLibrary.Application.BehavioralInsights.Services;
using AppFinancesClassLibrary.Application.Documents.Entities.Results;
using AppFinancesClassLibrary.Application.Documents.Services;
using AppFinancesClassLibrary.Application.Notifications.Services;
using System.Collections.Concurrent;

namespace AppFinances_API.IntegrationTests.Infrastructure
{
    public class FixedOllamaClient : IOllamaClient
    {
        public const string Text = "Texto de insight gerado pelo stub do Ollama.";

        public ConcurrentQueue<string> Prompts { get; } = new();

        public Task<string> GenerateInsightTextAsync(string prompt, CancellationToken cancellationToken = default)
        {
            Prompts.Enqueue(prompt);
            return Task.FromResult(Text);
        }
    }

    public class NoPushNotificationSender : IPushNotificationSender
    {
        public Task<IReadOnlyCollection<string>> SendAsync(IEnumerable<string> tokens, string title, string body,
            IDictionary<string, string> data, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyCollection<string>>([]);
    }

    public class NoCnpjRegistryClient : ICnpjRegistryClient
    {
        public Task<CnpjActivity> FetchAsync(string cnpj, CancellationToken cancellationToken = default)
            => Task.FromResult(new CnpjActivity(cnpj, null, null));
    }
}
