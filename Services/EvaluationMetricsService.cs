using MovieRankingSystem.Models;

namespace MovieRankingSystem.Services
{
    public interface IEvaluationMetricsService
    {
        RankingEvaluationSummary EvaluateMovieRanking(IEnumerable<IGrouping<string, MovieData>> testGroups, Func<MovieData, float> scoreSelector, string rankingType);
        RankingEvaluationSummary EvaluateMovieRanking(IEnumerable<IGrouping<string, MovieData>> testGroups, Func<IGrouping<string, MovieData>, Dictionary<string, float>> groupScoreSelector, string rankingType);
    }

    public class EvaluationMetricsService : IEvaluationMetricsService
    {
        public RankingEvaluationSummary EvaluateMovieRanking(
            IEnumerable<IGrouping<string, MovieData>> testGroups,
            Func<MovieData, float> scoreSelector,
            string rankingType)
        {
            return EvaluateGroups(testGroups, (group, movie) => scoreSelector(movie), rankingType);
        }

        public RankingEvaluationSummary EvaluateMovieRanking(
            IEnumerable<IGrouping<string, MovieData>> testGroups,
            Func<IGrouping<string, MovieData>, Dictionary<string, float>> groupScoreSelector,
            string rankingType)
        {
            return EvaluateGroups(testGroups, (group, movie) => groupScoreSelector(group)[movie.Movie], rankingType);
        }

        private RankingEvaluationSummary EvaluateGroups(
            IEnumerable<IGrouping<string, MovieData>> testGroups,
            Func<IGrouping<string, MovieData>, MovieData, float> scoreEvaluator,
            string rankingType)
        {
            var validGroups = testGroups.Where(g => g.Any()).ToList();
            var groupList = validGroups.Select(g => g.ToList()).ToList();
            
            var modelRankings = validGroups
                .Select(group => RankGroup(group.ToList(), movie => scoreEvaluator(group, movie), rankingType))
                .ToList();

            return EvaluateAgainstBaseline(groupList, modelRankings);
        }

        private RankingEvaluationSummary EvaluateAgainstBaseline(
            List<List<MovieData>> groupList,
            List<List<MovieData>> modelRankings)
        {
            var baselineRankings = groupList
                .Select(group => group
                    .OrderByDescending(m => m.AvgRating)
                    .ThenByDescending(m => m.Popularity)
                    .ThenBy(m => m.Movie, StringComparer.OrdinalIgnoreCase)
                    .ToList())
                .ToList();

            var modelNdcg = modelRankings.Average(group => CalculateNdcgAtK(group, 3));
            var baselineNdcg = baselineRankings.Average(group => CalculateNdcgAtK(group, 3));
            var modelMap = modelRankings.Average(CalculateAveragePrecision);
            var baselineMap = baselineRankings.Average(CalculateAveragePrecision);

            return new RankingEvaluationSummary
            {
                ModelNdcgAt3 = modelNdcg,
                BaselineNdcgAt3 = baselineNdcg,
                ModelMap = modelMap,
                BaselineMap = baselineMap
            };
        }

        private List<MovieData> RankGroup(List<MovieData> group, Func<MovieData, float> scoreSelector, string rankingType)
        {
            return group
                .OrderByDescending(movie => scoreSelector(movie))
                .ThenByDescending(movie => rankingType == RankingTypes.Listwise ? movie.RatingPopularityScore : movie.AvgRating)
                .ThenByDescending(movie => movie.Popularity)
                .ThenBy(movie => movie.Movie, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private double CalculateNdcgAtK(List<MovieData> rankedMovies, int k)
        {
            var topK = rankedMovies.Take(k).ToList();
            if (topK.Count == 0)
            {
                return 0d;
            }

            double dcg = CalculateDcg(topK);

            var idealTopK = rankedMovies
                .OrderByDescending(m => m.Label)
                .ThenByDescending(m => m.Popularity)
                .Take(k)
                .ToList();

            double idcg = CalculateDcg(idealTopK);

            return idcg <= 0d ? 0d : dcg / idcg;
        }

        private static double CalculateDcg(List<MovieData> movies)
        {
            double dcg = 0d;
            for (var i = 0; i < movies.Count; i++)
            {
                var gain = Math.Pow(2d, movies[i].Label) - 1d;
                var discount = 1d / Math.Log2(i + 2d);
                dcg += gain * discount;
            }
            return dcg;
        }

        private double CalculateAveragePrecision(List<MovieData> rankedMovies)
        {
            const float relevanceThreshold = 2f;
            var relevantCount = rankedMovies.Count(movie => movie.Label >= relevanceThreshold);
            if (relevantCount == 0)
            {
                return 0d;
            }

            double precisionSum = 0d;
            var hits = 0;

            for (var i = 0; i < rankedMovies.Count; i++)
            {
                if (rankedMovies[i].Label < relevanceThreshold)
                {
                    continue;
                }

                hits++;
                precisionSum += (double)hits / (i + 1);
            }

            return precisionSum / relevantCount;
        }
    }
}
