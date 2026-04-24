namespace MovieRankingSystem.Models
{
    public class RankingEvaluationSummary
    {
        public double ModelNdcgAt3 { get; set; }
        public double BaselineNdcgAt3 { get; set; }
        public double ModelMap { get; set; }
        public double BaselineMap { get; set; }
    }
}
