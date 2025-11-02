using Microsoft.AspNetCore.Mvc;
using Registration.Models;
using Registration.Repository;

namespace Registration.Controllers
{
    public class DashBoardController : Controller
    {
        private readonly IUserInterestRepository _userInterestRepository;
        private readonly IConfiguration _configuration;
        public DashBoardController(IUserInterestRepository userInterestRepository, IConfiguration configuration)
        {
            _userInterestRepository = userInterestRepository;
            _configuration = configuration;
        }


        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            var userId = HttpContext.Session.GetString("UserId");

            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }
            var hasCompletedInterestSelection = await _userInterestRepository.HasCompletedInterestSelectionAsync(userId);

            if (!hasCompletedInterestSelection)
            {
                return RedirectToAction("SelectInterests");
            }

            return View();
        }


        [HttpGet]
        public async Task<IActionResult> SelectInterests()
        {
            var userId = HttpContext.Session.GetString("UserId");

            if (userId == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // Check if user already completed interest selection (prevent re-access)
            var hasCompletedInterestSelection = await _userInterestRepository.HasCompletedInterestSelectionAsync(userId);

            if (hasCompletedInterestSelection)
            {
                return RedirectToAction("Dashboard");
            }

            // Get all active categories
            var categories = await _userInterestRepository.GetAllActiveCategoriesAsync();

            var viewModel = new InterestSelectionViewModel
            {
                Categories = categories
            };

            return View(viewModel);
        }

        [HttpPost]
        public async Task<IActionResult> SaveInterests([FromBody] List<int> categoryIds)
        {
            var userId = HttpContext.Session.GetString("UserId");

            if (userId == null)
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            if (categoryIds == null || !categoryIds.Any())
            {
                return Json(new { success = false, message = "Please select at least one interest" });
            }

            try
            {
                await _userInterestRepository.SaveUserInterestsAsync(userId, categoryIds);
                return Json(new { success = true, message = "Interests saved successfully" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error saving interests: " + ex.Message });
            }
        }


        [HttpGet]
        public async Task<IActionResult> SkipInterests()
        {
            var userId = HttpContext.Session.GetString("UserId");

            if (userId == null)
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            try
            {
                await _userInterestRepository.MarkInterestSelectionAsSkippedAsync(userId);
                TempData["skip"] = "Interest selection skipped";
                return RedirectToAction("Dashboard");
                //return Json(new { success = true, message = "Interest selection skipped" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error skipping interests: " + ex.Message });
            }
        }

        [HttpGet]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            Response.Cookies.Delete("AuthToken");
            TempData["SuccessMessage"] = "You have been logged out successfully.";
            return RedirectToAction("Login","Account");
        }




    }
}
