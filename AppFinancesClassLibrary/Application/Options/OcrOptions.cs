namespace AppFinancesClassLibrary.Application.Options
{
    public class OcrOptions
    {
        public decimal ConfidenceThreshold { get; set; } = 0.8m;
        public decimal ValueConfidenceThreshold { get; set; } = 0.9m;
        public int PaddleDetectionMaxSize { get; set; } = 1600;
        public string StorageBasePath { get; set; } = "storage/documents";
    }
}
