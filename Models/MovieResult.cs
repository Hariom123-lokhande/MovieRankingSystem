namespace MovieRankingSystem.Models
{
    public class MovieResult
    {
        public string Movie { get; set; } = string.Empty;
        public float AvgRating { get; set; }
        public float Popularity { get; set; }
        public float Score { get; set; }
        public float ModelScore { get; set; }
        public int Rank { get; set; }
        public int BetterThanCount { get; set; }
        public string SummaryTag { get; set; } = string.Empty;
        public string Explanation { get; set; } = string.Empty;
    }
}
