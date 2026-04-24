namespace MovieRankingSystem.Models
{
    public class PairwiseMovieData
    {
        public float RatingDiff { get; set; }
        public float PopularityDiff { get; set; }
        public float RatingPopularityScoreDiff { get; set; }
        public float NormalizedPopularityDiff { get; set; }
        public float HighRatedDiff { get; set; }
        public bool Label { get; set; }

        public static PairwiseMovieData FromMovies(MovieData left, MovieData right, bool label = false)
        {
            return new PairwiseMovieData
            {
                RatingDiff = left.AvgRating - right.AvgRating,
                PopularityDiff = left.Popularity - right.Popularity,
                RatingPopularityScoreDiff = left.RatingPopularityScore - right.RatingPopularityScore,
                NormalizedPopularityDiff = left.NormalizedPopularity - right.NormalizedPopularity,
                HighRatedDiff = left.IsHighRated - right.IsHighRated,
                Label = label
            };
        }
    }
}
