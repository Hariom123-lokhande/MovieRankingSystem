namespace MovieRankingSystem.Models
{
    public static class RankingTypes
    {
        public const string Listwise = "listwise";
        public const string Pointwise = "pointwise";
        public const string Pairwise = "pairwise";

        public static string Normalize(string? rankingType)
        {
            return rankingType?.Trim().ToLowerInvariant() switch
            {
                Pointwise => Pointwise,
                Pairwise => Pairwise,
                _ => Listwise
            };
        }
    }
}
