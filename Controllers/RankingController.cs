using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MovieRankingSystem.Models;
using MovieRankingSystem.Services;

namespace MovieRankingSystem.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api")]
    public class RankingController : ControllerBase
    {
        private readonly RankingService _rankingService;
        private readonly PredictionService _predictionService;
        private readonly ILogger<RankingController> _logger;

        public RankingController(RankingService rankingService, PredictionService predictionService, ILogger<RankingController> logger)
        {
            _rankingService = rankingService;
            _predictionService = predictionService;
            _logger = logger;
        }

        [HttpPost("rank")]
        public IActionResult Rank([FromBody] RankRequest request)
        {
            if (string.IsNullOrEmpty(request.Query))
            {
                _logger.LogWarning("Rank endpoint called with empty query.");
                return BadRequest(new { Message = "Query is required" });
            }

            var rankingType = RankingTypes.Normalize(request.RankingType);
            _logger.LogInformation("Processing {RankingType} ranking for query: {Query}", rankingType, request.Query);

            var results = _predictionService.RankMovies(request.Query, rankingType);
            
            if (results == null || !results.Any())
            {
                _logger.LogWarning("No results found for query: {Query}", request.Query);
                return NotFound(new { Message = "No results found for this query" });
            }

            _logger.LogInformation("Successfully ranked {Count} movies for query: {Query}", results.Count, request.Query);
            return Ok(results);
        }

        [HttpPost("compare")]
        public IActionResult Compare([FromBody] CompareRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(new { Message = "Query is required" });
            }

            var rankingType = RankingTypes.Normalize(request.RankingType);
            var baseline = _predictionService.GetBaselineMovies(request.Query);
            var ranked = _predictionService.RankMovies(request.Query, rankingType);

            if (!baseline.Any() || !ranked.Any())
            {
                return NotFound(new { Message = "No results found for this query" });
            }

            return Ok(new
            {
                Query = request.Query,
                RankingType = rankingType,
                Baseline = baseline,
                Ranked = ranked
            });
        }

        [HttpPost("train")]
        public IActionResult Train([FromBody] TrainRequest? request)
        {
            try
            {
                var rankingType = RankingTypes.Normalize(request?.RankingType);
                _logger.LogInformation("Starting {RankingType} ML.NET model training...", rankingType);
                var result = _rankingService.TrainModel(rankingType);
                _predictionService.LoadModel(rankingType);

                _logger.LogInformation(
                    "{RankingType} model training completed successfully. {MetricName}: {MetricValue}",
                    result.RankingType,
                    result.MetricName,
                    result.MetricValue);
                
                return Ok(new { 
                    result.Message,
                    result.RankingType,
                    result.Algorithm,
                    result.MetricName,
                    result.MetricValue,
                    result.ModelNdcgAt3,
                    result.ModelMap,
                    result.BaselineNdcgAt3,
                    result.BaselineMap
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to train ML.NET model.");
                return BadRequest(new { Message = $"Training failed: {ex.Message}" });
            }
        }
    }

    public class RankRequest
    {
        public string Query { get; set; } = string.Empty;
        public string RankingType { get; set; } = RankingTypes.Listwise;
    }

    public class TrainRequest
    {
        public string RankingType { get; set; } = RankingTypes.Listwise;
    }

    public class CompareRequest
    {
        public string Query { get; set; } = string.Empty;
        public string RankingType { get; set; } = RankingTypes.Listwise;
    }
}
