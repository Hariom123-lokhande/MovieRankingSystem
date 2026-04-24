using Microsoft.ML.Data;

namespace MovieRankingSystem.Models
{
    public class PairwiseMoviePrediction
    {
        public bool PredictedLabel { get; set; }

        [ColumnName("Probability")]
        public float Probability { get; set; }

        [ColumnName("Score")]
        public float Score { get; set; }
    }
}
