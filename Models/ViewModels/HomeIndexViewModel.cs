namespace MovieRankingSystem.Models.ViewModels
{
    public class HomeIndexViewModel
    {
        public bool IsModelLoaded { get; set; }
        public string DefaultRankingType { get; set; } = Models.RankingTypes.Listwise;
    }
}
