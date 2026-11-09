using AppFinancesClassLibrary.Application.Notifications.Services;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace AppFinancesClassLibrary.Infrastructure.Push
{
    public class ExpoPushNotificationSender(HttpClient httpClient, ILogger<ExpoPushNotificationSender> logger) : IPushNotificationSender
    {
        private const int MaxMessagesPerRequest = 100;

        public async Task<IReadOnlyCollection<string>> SendAsync(
            IEnumerable<string> tokens,
            string title,
            string body,
            IDictionary<string, string> data,
            CancellationToken cancellationToken = default)
        {
            var invalidTokens = new List<string>();

            var validTokens = tokens.Distinct().Where(token =>
            {
                if (IsExpoPushToken(token)) return true;
                logger.LogWarning("Token de dispositivo ignorado por não estar no formato do Expo.");
                return false;
            }).ToList();

            foreach (var chunk in validTokens.Chunk(MaxMessagesPerRequest))
            {
                var messages = chunk.Select(token => new ExpoPushMessage
                {
                    To = token,
                    Title = title,
                    Body = body,
                    Data = data,
                    Sound = "default"
                }).ToList();

                using var response = await httpClient.PostAsJsonAsync("/--/api/v2/push/send", messages, cancellationToken);
                response.EnsureSuccessStatusCode();

                var payload = await response.Content.ReadFromJsonAsync<ExpoPushResponse>(cancellationToken: cancellationToken);
                if (payload?.Data is null) continue;

                for (var i = 0; i < payload.Data.Count && i < chunk.Length; i++)
                {
                    var ticket = payload.Data[i];
                    if (ticket.Status != "error") continue;

                    if (ticket.Details?.Error == "DeviceNotRegistered")
                        invalidTokens.Add(chunk[i]);
                    else
                        logger.LogWarning("Expo recusou o push: {Error} - {Message}", ticket.Details?.Error, ticket.Message);
                }
            }

            return invalidTokens;
        }

        private static bool IsExpoPushToken(string token) =>
            !string.IsNullOrWhiteSpace(token)
            && (token.StartsWith("ExponentPushToken[") || token.StartsWith("ExpoPushToken["))
            && token.EndsWith(']');

        private sealed class ExpoPushMessage
        {
            [JsonPropertyName("to")] public string To { get; set; } = string.Empty;
            [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
            [JsonPropertyName("body")] public string Body { get; set; } = string.Empty;
            [JsonPropertyName("data")] public IDictionary<string, string> Data { get; set; }
            [JsonPropertyName("sound")] public string Sound { get; set; }
        }

        private sealed class ExpoPushResponse
        {
            [JsonPropertyName("data")] public List<ExpoPushTicket> Data { get; set; }
        }

        private sealed class ExpoPushTicket
        {
            [JsonPropertyName("status")] public string Status { get; set; }
            [JsonPropertyName("message")] public string Message { get; set; }
            [JsonPropertyName("details")] public ExpoPushTicketDetails Details { get; set; }
        }

        private sealed class ExpoPushTicketDetails
        {
            [JsonPropertyName("error")] public string Error { get; set; }
        }
    }
}
