using Microsoft.ML;
using MovieRankingSystem.Models;
using System.Diagnostics;
using System.Globalization;

namespace MovieRankingSystem.Services
{
    public class PredictionService
    {
        private readonly string _dataPath;
        private readonly RankingService _rankingService;
        private readonly IMovieDataLoader _movieDataLoader;
        private readonly MLContext _mlContext;
        private PredictionEngine<MovieData, MoviePrediction>? _listwiseEngine;
        private PredictionEngine<MovieData, MoviePrediction>? _pointwiseEngine;
        private PredictionEngine<PairwiseMovieData, PairwiseMoviePrediction>? _pairwiseEngine;

        public bool IsModelLoaded { get; private set; }

        public PredictionService(RankingService rankingService, IMovieDataLoader movieDataLoader)
        {
            _rankingService = rankingService;
            _movieDataLoader = movieDataLoader;
            _dataPath = Path.Combine(Directory.GetCurrentDirectory(), "Data", "augmented_movies.csv");
            _mlContext = new MLContext();
            
            // Pre-load all available models at startup
            LoadModel(RankingTypes.Listwise);
            LoadModel(RankingTypes.Pointwise);
            LoadModel(RankingTypes.Pairwise);
        }

        public void LoadModel(string? rankingType = null)
        {
            var normalizedType = RankingTypes.Normalize(rankingType);
            var modelPath = _rankingService.GetModelPath(normalizedType);

            if (!File.Exists(modelPath))
            {
                if (normalizedType == RankingTypes.Listwise)
                {
                    IsModelLoaded = false;
                }
                return;
            }

            var model = _mlContext.Model.Load(modelPath, out _);

            switch (normalizedType)
            {
                case RankingTypes.Pointwise:
                    _pointwiseEngine = _mlContext.Model.CreatePredictionEngine<MovieData, MoviePrediction>(model);
                    break;
                case RankingTypes.Pairwise:
                    _pairwiseEngine = _mlContext.Model.CreatePredictionEngine<PairwiseMovieData, PairwiseMoviePrediction>(model);
                    break;
                default:
                    _listwiseEngine = _mlContext.Model.CreatePredictionEngine<MovieData, MoviePrediction>(model);
                    IsModelLoaded = true;
                    break;
            }
        }

        public List<MovieResult> RankMovies(string query, string? rankingType)
        {
            var normalizedType = RankingTypes.Normalize(rankingType);

            var allMovies = _movieDataLoader.LoadMoviesWithEngineeredFeatures(_dataPath);
            var filteredMovies = allMovies
                .Where(m => m.Query.Equals(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!filteredMovies.Any())
            {
                Debug.WriteLine($"No movies found for query: {query}");
                return new List<MovieResult>();
            }

            var scoredMovies = normalizedType switch
            {
                RankingTypes.Pointwise => ScorePointwise(filteredMovies),
                RankingTypes.Pairwise => ScorePairwise(filteredMovies),
                _ => ScoreListwise(filteredMovies)
            };

            var orderedMovies = scoredMovies
                .OrderByDescending(m => m.RankScore)
                .ThenByDescending(m => m.Movie.AvgRating)
                .ThenByDescending(m => m.Movie.Popularity)
                .ThenBy(m => m.Movie.Movie, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return orderedMovies
                .Select((item, index) => new MovieResult
                {
                    Movie = item.Movie.Movie,
                    AvgRating = item.Movie.AvgRating,
                    Popularity = item.Movie.Popularity,
                    Score = (item.RankScore * 10f) + 50f, // Simplified score mapping
                    ModelScore = item.ModelScore,
                    Rank = index + 1,
                    BetterThanCount = orderedMovies.Count - index - 1,
                    SummaryTag = BuildSummaryTag(item.Movie, normalizedType),
                    Explanation = BuildExplanation(item.Movie, item.ModelScore, index, orderedMovies.Count, normalizedType)
                })
                .ToList();
        }

        public List<MovieResult> GetBaselineMovies(string query)
        {
            var allMovies = _movieDataLoader.LoadMoviesWithEngineeredFeatures(_dataPath);
            var filteredMovies = allMovies
                .Where(m => m.Query.Equals(query, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(m => m.AvgRating)
                .ThenByDescending(m => m.Popularity)
                .ThenBy(m => m.Movie, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return filteredMovies
                .Select((movie, index) => new MovieResult
                {
                    Movie = movie.Movie,
                    AvgRating = movie.AvgRating,
                    Popularity = movie.Popularity,
                    Score = 50f + (movie.AvgRating * 10f),
                    ModelScore = movie.RatingPopularityScore,
                    Rank = index + 1,
                    BetterThanCount = filteredMovies.Count - index - 1,
                    SummaryTag = "Baseline Sort",
                    Explanation = "This list uses a simple non-ML ordering based on rating first and popularity second."
                })
                .ToList();
        }



        private List<(MovieData Movie, float ModelScore, float RankScore)> ScoreListwise(List<MovieData> filteredMovies)
        {
            if (_listwiseEngine == null) return new List<(MovieData, float, float)>();

            return filteredMovies
                .Select(movie =>
                {
                    var prediction = _listwiseEngine.Predict(movie);
                    return (movie, prediction.Score, prediction.Score);
                })
                .ToList();
        }

        private List<(MovieData Movie, float ModelScore, float RankScore)> ScorePointwise(List<MovieData> filteredMovies)
        {
            if (_pointwiseEngine == null) return new List<(MovieData, float, float)>();

            return filteredMovies
                .Select(movie =>
                {
                    var prediction = _pointwiseEngine.Predict(movie);
                    return (movie, prediction.Score, prediction.Score);
                })
                .ToList();
        }

        private List<(MovieData Movie, float ModelScore, float RankScore)> ScorePairwise(List<MovieData> filteredMovies)
        {
            if (_pairwiseEngine == null)
            {
                return new List<(MovieData, float, float)>();
            }

            var totals = filteredMovies.ToDictionary(
                movie => movie.Movie,
                _ => (ModelScore: 0f, RankScore: 0f),
                StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < filteredMovies.Count; i++)
            {
                for (var j = i + 1; j < filteredMovies.Count; j++)
                {
                    var left = filteredMovies[i];
                    var right = filteredMovies[j];
                    var comparison = PairwiseMovieData.FromMovies(left, right);
                    var prediction = _pairwiseEngine.Predict(comparison);
                    var probability = prediction.Probability;

                    totals[left.Movie] = (
                        totals[left.Movie].ModelScore + probability,
                        totals[left.Movie].RankScore + probability);

                    totals[right.Movie] = (
                        totals[right.Movie].ModelScore + (1f - probability),
                        totals[right.Movie].RankScore + (1f - probability));
                }
            }

            return filteredMovies
                .Select(movie => (movie, totals[movie.Movie].ModelScore, totals[movie.Movie].RankScore))
                .ToList();
        }







        private static string BuildSummaryTag(MovieData movie, string rankingType) => "Relevance Match";

        private static string BuildExplanation(MovieData movie, float modelScore, int zeroBasedRank, int totalCount, string rankingType)
            => $"Ranked #{zeroBasedRank + 1} based on {rankingType} model signals.";
    }
}
