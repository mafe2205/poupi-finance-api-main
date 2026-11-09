namespace AppFinancesClassLibrary.Application.Options
{
    public class OllamaOptions
    {
        public string BaseUrl { get; set; } = "http://localhost:11434";
        public string Model { get; set; } = "qwen3:4b";
        public int TimeoutSeconds { get; set; } = 300;
    }
}
