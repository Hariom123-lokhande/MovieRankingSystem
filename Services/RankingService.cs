using Microsoft.ML;
using MovieRankingSystem.Models;
using System.Globalization;
using System.Text;

namespace MovieRankingSystem.Services
{
    public class RankingService
    {
        private readonly string _dataPath;
        private readonly string _dataDirectory;
        private readonly IMovieDataLoader _movieDataLoader;
        private readonly IEvaluationMetricsService _evaluationMetricsService;

        public RankingService(IMovieDataLoader movieDataLoader, IEvaluationMetricsService evaluationMetricsService)
        {
            _movieDataLoader = movieDataLoader;
            _evaluationMetricsService = evaluationMetricsService;
            _dataDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Data");
            _dataPath = Path.Combine(_dataDirectory, "augmented_movies.csv");

            if (!Directory.Exists(_dataDirectory))
            {
                Directory.CreateDirectory(_dataDirectory);
            }
        }

        public TrainingResult TrainModel(string? rankingType)
        {
            var normalizedType = RankingTypes.Normalize(rankingType);
            var mlContext = new MLContext(seed: 42);
            var dataList = _movieDataLoader.LoadMoviesWithEngineeredFeatures(_dataPath);

            if (dataList.Count < 2)
            {
                throw new InvalidOperationException("Not enough movie rows were loaded to train the model.");
            }

            var groupedMovies = dataList
                .GroupBy(m => m.Query, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (groupedMovies.Count < 2)
            {
                throw new InvalidOperationException("Training requires at least two query groups.");
            }

            var (trainGroups, testGroups) = SplitGroups(groupedMovies);

            return normalizedType switch
            {
                RankingTypes.Pointwise => TrainPointwise(mlContext, trainGroups, testGroups),
                RankingTypes.Pairwise => TrainPairwise(mlContext, trainGroups, testGroups),
                _ => TrainListwise(mlContext, trainGroups, testGroups)
            };
        }

        public string GetModelPath(string? rankingType)
        {
            var normalizedType = RankingTypes.Normalize(rankingType);
            return Path.Combine(_dataDirectory, $"model-{normalizedType}.zip");
        }

        private TrainingResult TrainListwise(MLContext mlContext, List<IGrouping<string, MovieData>> trainGroups, List<IGrouping<string, MovieData>> testGroups)
        {
            var trainList = FlattenGroups(trainGroups);
            var testList = FlattenGroups(testGroups);
            EnsureNonEmptySplit(trainList, testList);

            var trainData = mlContext.Data.LoadFromEnumerable(trainList);
            var testData = mlContext.Data.LoadFromEnumerable(testList);

            var pipeline = mlContext.Transforms.Conversion.MapValueToKey("GroupId", nameof(MovieData.Query))
                .Append(mlContext.Transforms.Concatenate("Features",
                    nameof(MovieData.AvgRating),
                    nameof(MovieData.Popularity),
                    nameof(MovieData.RatingPopularityScore),
                    nameof(MovieData.NormalizedPopularity),
                    nameof(MovieData.IsHighRated)))
                .Append(mlContext.Ranking.Trainers.LightGbm(
                    labelColumnName: nameof(MovieData.Label),
                    featureColumnName: "Features",
                    rowGroupColumnName: "GroupId"));

            Console.WriteLine("Training ML.NET listwise ranking model...");
            var model = pipeline.Fit(trainData);
            var predictions = model.Transform(testData);
            var metrics = mlContext.Ranking.Evaluate(predictions, labelColumnName: nameof(MovieData.Label), rowGroupColumnName: "GroupId");
            var predictionEngine = mlContext.Model.CreatePredictionEngine<MovieData, MoviePrediction>(model);
            var evaluation = _evaluationMetricsService.EvaluateMovieRanking(
                testGroups,
                movie => predictionEngine.Predict(movie).Score,
                RankingTypes.Listwise);

            var ndcgAt3 = metrics.NormalizedDiscountedCumulativeGains.Count > 2
                ? metrics.NormalizedDiscountedCumulativeGains[2].ToString("F4")
                : "N/A";

            mlContext.Model.Save(model, trainData.Schema, GetModelPath(RankingTypes.Listwise));

            return new TrainingResult
            {
                RankingType = RankingTypes.Listwise,
                Algorithm = "LightGBM LambdaMART",
                MetricName = "NDCG@3",
                MetricValue = ndcgAt3,
                Message = BuildTrainingMessage("Listwise ranking model trained successfully.", evaluation),
                ModelNdcgAt3 = evaluation.ModelNdcgAt3.ToString("F4"),
                ModelMap = evaluation.ModelMap.ToString("F4"),
                BaselineNdcgAt3 = evaluation.BaselineNdcgAt3.ToString("F4"),
                BaselineMap = evaluation.BaselineMap.ToString("F4")
            };
        }

        private TrainingResult TrainPointwise(MLContext mlContext, List<IGrouping<string, MovieData>> trainGroups, List<IGrouping<string, MovieData>> testGroups)
        {
            var trainList = FlattenGroups(trainGroups);
            var testList = FlattenGroups(testGroups);
            EnsureNonEmptySplit(trainList, testList);

            var trainData = mlContext.Data.LoadFromEnumerable(trainList);
            var testData = mlContext.Data.LoadFromEnumerable(testList);

            var pipeline = mlContext.Transforms.Concatenate("Features",
                    nameof(MovieData.AvgRating),
                    nameof(MovieData.Popularity),
                    nameof(MovieData.RatingPopularityScore),
                    nameof(MovieData.NormalizedPopularity),
                    nameof(MovieData.IsHighRated))
                .Append(mlContext.Regression.Trainers.FastTree(
                    labelColumnName: nameof(MovieData.Label),
                    featureColumnName: "Features"));

            Console.WriteLine("Training ML.NET pointwise ranking model...");
            var model = pipeline.Fit(trainData);
            var predictions = model.Transform(testData);
            var metrics = mlContext.Regression.Evaluate(predictions, labelColumnName: nameof(MovieData.Label), scoreColumnName: "Score");
            var predictionEngine = mlContext.Model.CreatePredictionEngine<MovieData, MoviePrediction>(model);
            var evaluation = _evaluationMetricsService.EvaluateMovieRanking(
                testGroups,
                movie => predictionEngine.Predict(movie).Score,
                RankingTypes.Pointwise);

            mlContext.Model.Save(model, trainData.Schema, GetModelPath(RankingTypes.Pointwise));

            return new TrainingResult
            {
                RankingType = RankingTypes.Pointwise,
                Algorithm = "FastTree Regression",
                MetricName = "RSquared",
                MetricValue = metrics.RSquared.ToString("F4"),
                Message = BuildTrainingMessage("Pointwise ranking model trained successfully.", evaluation),
                ModelNdcgAt3 = evaluation.ModelNdcgAt3.ToString("F4"),
                ModelMap = evaluation.ModelMap.ToString("F4"),
                BaselineNdcgAt3 = evaluation.BaselineNdcgAt3.ToString("F4"),
                BaselineMap = evaluation.BaselineMap.ToString("F4")
            };
        }

        private TrainingResult TrainPairwise(MLContext mlContext, List<IGrouping<string, MovieData>> trainGroups, List<IGrouping<string, MovieData>> testGroups)
        {
            var trainPairs = BuildPairwiseExamples(trainGroups);
            var testPairs = BuildPairwiseExamples(testGroups);

            if (trainPairs.Count == 0 || testPairs.Count == 0)
            {
                throw new InvalidOperationException("Pairwise training requires enough labeled comparisons in both train and test groups.");
            }

            var trainData = mlContext.Data.LoadFromEnumerable(trainPairs);
            var testData = mlContext.Data.LoadFromEnumerable(testPairs);

            var pipeline = mlContext.Transforms.Concatenate("Features",
                    nameof(PairwiseMovieData.RatingDiff),
                    nameof(PairwiseMovieData.PopularityDiff),
                    nameof(PairwiseMovieData.RatingPopularityScoreDiff),
                    nameof(PairwiseMovieData.NormalizedPopularityDiff),
                    nameof(PairwiseMovieData.HighRatedDiff))
                .Append(mlContext.BinaryClassification.Trainers.SdcaLogisticRegression(
                    labelColumnName: nameof(PairwiseMovieData.Label),
                    featureColumnName: "Features"));

            Console.WriteLine("Training ML.NET pairwise ranking model...");
            var model = pipeline.Fit(trainData);
            var predictions = model.Transform(testData);
            var metrics = mlContext.BinaryClassification.Evaluate(predictions, labelColumnName: nameof(PairwiseMovieData.Label), scoreColumnName: "Score");
            var predictionEngine = mlContext.Model.CreatePredictionEngine<PairwiseMovieData, PairwiseMoviePrediction>(model);
            var evaluation = _evaluationMetricsService.EvaluateMovieRanking(
                testGroups,
                group => ScorePairwiseGroup(group.ToList(), predictionEngine),
                RankingTypes.Pairwise);

            mlContext.Model.Save(model, trainData.Schema, GetModelPath(RankingTypes.Pairwise));

            return new TrainingResult
            {
                RankingType = RankingTypes.Pairwise,
                Algorithm = "SDCA Logistic Regression",
                MetricName = "AUC",
                MetricValue = metrics.AreaUnderRocCurve.ToString("F4"),
                Message = BuildTrainingMessage("Pairwise ranking model trained successfully.", evaluation),
                ModelNdcgAt3 = evaluation.ModelNdcgAt3.ToString("F4"),
                ModelMap = evaluation.ModelMap.ToString("F4"),
                BaselineNdcgAt3 = evaluation.BaselineNdcgAt3.ToString("F4"),
                BaselineMap = evaluation.BaselineMap.ToString("F4")
            };
        }

        private static (List<IGrouping<string, MovieData>> TrainGroups, List<IGrouping<string, MovieData>> TestGroups) SplitGroups(List<IGrouping<string, MovieData>> groupedMovies)
        {
            var testGroupCount = Math.Max(1, (int)Math.Round(groupedMovies.Count * 0.2f));
            if (testGroupCount >= groupedMovies.Count)
            {
                testGroupCount = groupedMovies.Count - 1;
            }

            var trainGroups = groupedMovies.Take(groupedMovies.Count - testGroupCount).ToList();
            var testGroups = groupedMovies.Skip(groupedMovies.Count - testGroupCount).ToList();

            return (trainGroups, testGroups);
        }

        private static List<MovieData> FlattenGroups(IEnumerable<IGrouping<string, MovieData>> groups)
        {
            return groups
                .SelectMany(g => g.OrderBy(m => m.Movie, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }

        private static void EnsureNonEmptySplit(List<MovieData> trainList, List<MovieData> testList)
        {
            if (trainList.Count == 0 || testList.Count == 0)
            {
                throw new InvalidOperationException("Training split produced an empty train or test dataset.");
            }
        }

        private static List<PairwiseMovieData> BuildPairwiseExamples(IEnumerable<IGrouping<string, MovieData>> groups)
        {
            var pairs = new List<PairwiseMovieData>();

            foreach (var group in groups)
            {
                var movies = group.OrderBy(m => m.Movie, StringComparer.OrdinalIgnoreCase).ToList();
                for (var i = 0; i < movies.Count; i++)
                {
                    for (var j = i + 1; j < movies.Count; j++)
                    {
                        if (Math.Abs(movies[i].Label - movies[j].Label) < 0.001f)
                        {
                            continue;
                        }

                        var leftWins = movies[i].Label > movies[j].Label;
                        pairs.Add(PairwiseMovieData.FromMovies(movies[i], movies[j], leftWins));
                        pairs.Add(PairwiseMovieData.FromMovies(movies[j], movies[i], !leftWins));
                    }
                }
            }

            return pairs;
        }





        private static Dictionary<string, float> ScorePairwiseGroup(
            List<MovieData> movies,
            PredictionEngine<PairwiseMovieData, PairwiseMoviePrediction> predictionEngine)
        {
            var totals = movies.ToDictionary(movie => movie.Movie, _ => 0f, StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < movies.Count; i++)
            {
                for (var j = i + 1; j < movies.Count; j++)
                {
                    var left = movies[i];
                    var right = movies[j];
                    var probability = predictionEngine.Predict(PairwiseMovieData.FromMovies(left, right)).Probability;
                    totals[left.Movie] += probability;
                    totals[right.Movie] += 1f - probability;
                }
            }

            return totals;
        }



        private static string BuildTrainingMessage(string heading, RankingEvaluationSummary evaluation)
        {
            var builder = new StringBuilder();
            builder.AppendLine(heading);
            builder.AppendLine($"Model NDCG@3: {evaluation.ModelNdcgAt3:F4}");
            builder.AppendLine($"Baseline NDCG@3: {evaluation.BaselineNdcgAt3:F4}");
            builder.AppendLine($"Model MAP: {evaluation.ModelMap:F4}");
            builder.Append($"Baseline MAP: {evaluation.BaselineMap:F4}");
            return builder.ToString();
        }


    }
}
