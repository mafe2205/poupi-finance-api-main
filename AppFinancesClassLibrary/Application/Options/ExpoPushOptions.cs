namespace AppFinancesClassLibrary.Application.Options
{
    public class ExpoPushOptions
    {
        public string BaseUrl { get; set; } = "https://exp.host";
        public string AccessToken { get; set; } = string.Empty;
        public int TimeoutSeconds { get; set; } = 30;
    }
}
