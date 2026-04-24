using Microsoft.AspNetCore.Mvc;
using MovieRankingSystem.Services;
using MovieRankingSystem.Models.ViewModels;

namespace MovieRankingSystem.Controllers
{
    public class UIController : Controller
    {
        private readonly PredictionService _predictionService;

        public UIController(PredictionService predictionService)
        {
            _predictionService = predictionService;
        }

        [HttpGet("/")]
        public IActionResult Index()
        {
            return View("~/Views/Home/Index.cshtml", new HomeIndexViewModel
            {
                IsModelLoaded = _predictionService.IsModelLoaded,
                DefaultRankingType = Models.RankingTypes.Listwise
            });
        }

        [HttpGet("/compare")]
        public IActionResult Compare()
        {
            return View("~/Views/Compare/Index.cshtml", new HomeIndexViewModel
            {
                IsModelLoaded = _predictionService.IsModelLoaded,
                DefaultRankingType = Models.RankingTypes.Listwise
            });
        }

        [HttpGet("/login")]
        public IActionResult Login()
        {
            return View("~/Views/Account/Login.cshtml");
        }

        [HttpGet("/signup")]
        public IActionResult LoginRedirect()
        {
            return View("~/Views/Account/Signup.cshtml");
        }
    }
}
