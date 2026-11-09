namespace AppFinancesClassLibrary.Application.Notifications.Services
{
    public interface IPushNotificationSender
    {
        Task<IReadOnlyCollection<string>> SendAsync(
            IEnumerable<string> tokens,
            string title,
            string body,
            IDictionary<string, string> data,
            CancellationToken cancellationToken = default);
    }
}
