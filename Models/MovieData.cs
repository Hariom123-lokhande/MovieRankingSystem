using Microsoft.ML.Data;

namespace MovieRankingSystem.Models
{
    // Input data schema for ML training/prediction
    public class MovieData
    {
        [LoadColumn(0)]
        public string Query { get; set; } = string.Empty;

        [LoadColumn(2)]
        public float AvgRating { get; set; }

        [LoadColumn(3)]
        public float Popularity { get; set; }

        [LoadColumn(4)]
        public float Label { get; set; }

        [LoadColumn(1)]
        public string Movie { get; set; } = string.Empty;

        // Engineered Features
        public float RatingPopularityScore { get; set; }
        public float NormalizedPopularity { get; set; }
        public float IsHighRated { get; set; }
    }
}
