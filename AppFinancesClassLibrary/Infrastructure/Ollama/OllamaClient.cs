using AppFinancesClassLibrary.Application.BehavioralInsights.Services;
using AppFinancesClassLibrary.Application.Options;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AppFinancesClassLibrary.Infrastructure.Ollama
{
    public class OllamaClient(HttpClient httpClient, IOptions<OllamaOptions> options) : IOllamaClient
    {
        private readonly OllamaOptions options = options.Value;

        public async Task<string> GenerateInsightTextAsync(string prompt, CancellationToken cancellationToken = default)
        {
            var requestBody = new OllamaGenerateRequest
            {
                Model = options.Model,
                Prompt = prompt,
                Stream = false
            };

            using var response = await httpClient.PostAsJsonAsync("/api/generate", requestBody, cancellationToken);
            response.EnsureSuccessStatusCode();

            var payload = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken: cancellationToken);
            return payload?.Response?.Trim() ?? string.Empty;
        }

        private sealed class OllamaGenerateRequest
        {
            [JsonPropertyName("model")] public string Model { get; set; } = string.Empty;
            [JsonPropertyName("prompt")] public string Prompt { get; set; } = string.Empty;
            [JsonPropertyName("stream")] public bool Stream { get; set; }
        }

        private sealed class OllamaGenerateResponse
        {
            [JsonPropertyName("response")] public string Response { get; set; }
        }
    }
}
