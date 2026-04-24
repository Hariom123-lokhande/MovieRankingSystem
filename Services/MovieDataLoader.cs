using MovieRankingSystem.Models;
using System.Diagnostics;
using System.Globalization;

namespace MovieRankingSystem.Services
{
    public interface IMovieDataLoader
    {
        List<MovieData> LoadMoviesWithEngineeredFeatures(string dataPath);
    }

    public class MovieDataLoader : IMovieDataLoader
    {
        private List<MovieData>? _cachedMovies;

        public List<MovieData> LoadMoviesWithEngineeredFeatures(string dataPath)
        {
            if (_cachedMovies != null)
            {
                return _cachedMovies;
            }

            var movies = new List<MovieData>();
            if (!File.Exists(dataPath))
            {
                return movies;
            }

            try
            {
                using var reader = new StreamReader(dataPath);
                _ = reader.ReadLine();

                while (!reader.EndOfStream)
                {
                    var line = reader.ReadLine();
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var values = line.Split(',');
                    if (values.Length < 5) continue;

                    if (!float.TryParse(values[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var avgRating) ||
                        !float.TryParse(values[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var popularity) ||
                        !float.TryParse(values[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var label))
                    {
                        continue;
                    }

                    movies.Add(new MovieData
                    {
                        Query = values[0].Trim(),
                        Movie = values[1].Trim(),
                        AvgRating = avgRating,
                        Popularity = popularity,
                        Label = NormalizeLabel(label),
                        RatingPopularityScore = avgRating * popularity,
                        NormalizedPopularity = popularity / 100f,
                        IsHighRated = avgRating > 4.0f ? 1.0f : 0.0f
                    });
                }
                _cachedMovies = movies;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading CSV: {ex.Message}");
            }

            return movies;
        }

        private static float NormalizeLabel(float label)
        {
            return Math.Max(0f, label - 1f);
        }
    }
}
