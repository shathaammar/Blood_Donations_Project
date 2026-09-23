using Blood_Donations_Project.Common;
using Blood_Donations_Project.Filters;
using Blood_Donations_Project.Services;
using Microsoft.AspNetCore.Mvc;

namespace Blood_Donations_Project.Controllers
{
    [SessionAuthorize(AppRoles.Donor)]
    public class DonorController : Controller
    {
        private readonly IDonationRequestService _donationRequestService;

        public DonorController(IDonationRequestService donationRequestService)
        {
            _donationRequestService = donationRequestService;
        }

        private int? GetUserId()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            return int.TryParse(userIdStr, out var id) ? id : null;
        }

        // No Donor/Dashboard view exists: the shared role-aware dashboard lives at Admin/Dashboard.
        // Route kept (the RequestDonation POST waiting-period branch redirects here).
        public IActionResult Dashboard()
        {
            return RedirectToAction("Dashboard", "Admin");
        }

        [HttpGet]
        public async Task<IActionResult> RequestDonation()
        {
            var userId = GetUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            var check = await _donationRequestService.CheckCanOpenRequestFormAsync(userId.Value);
            if (!check.Success)
            {
                TempData["ErrorMessage"] = check.Message;
                return RedirectToAction("Dashboard", "Admin");
            }

            var model = await _donationRequestService.GetRequestFormAsync(userId.Value);
            if (model == null)
            {
                TempData["ErrorMessage"] = "Donor profile not found.";
                return RedirectToAction("Dashboard", "Admin");
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestDonation(IFormCollection _)
        {
            var userId = GetUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            var pending = await _donationRequestService.CheckNoPendingRequestForSubmitAsync(userId.Value);
            if (!pending.Success)
            {
                TempData["ErrorMessage"] = pending.Message;
                return RedirectToAction("Dashboard", "Admin");
            }

            var waitingPeriod = await _donationRequestService.CheckSubmissionWaitingPeriodForSubmitAsync(userId.Value);
            if (!waitingPeriod.Success)
            {
                TempData["ErrorMessage"] = waitingPeriod.Message;
                // Existing redirect preserved: Donor/Dashboard (this action has no view yet).
                return RedirectToAction("Dashboard");
            }

            var result = await _donationRequestService.CreateRequestAsync(userId.Value);

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction("Dashboard", "Admin");
        }
    }
}
