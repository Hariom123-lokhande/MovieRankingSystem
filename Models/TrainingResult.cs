namespace MovieRankingSystem.Models
{
    public class TrainingResult
    {
        public string RankingType { get; set; } = RankingTypes.Listwise;
        public string Algorithm { get; set; } = string.Empty;
        public string MetricName { get; set; } = string.Empty;
        public string MetricValue { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string ModelNdcgAt3 { get; set; } = string.Empty;
        public string ModelMap { get; set; } = string.Empty;
        public string BaselineNdcgAt3 { get; set; } = string.Empty;
        public string BaselineMap { get; set; } = string.Empty;
    }
}
