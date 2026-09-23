using Blood_Donations_Project.Common;
using Blood_Donations_Project.Filters;
using Blood_Donations_Project.Models;
using Blood_Donations_Project.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Blood_Donations_Project.Controllers
{
    [SessionAuthorize(AppRoles.Donor)]
    public class DonorController : Controller
    {
        private readonly BloodDonationContext _context;
        private readonly IDonationRequestService _donationRequestService;

        public DonorController(BloodDonationContext context, IDonationRequestService donationRequestService)
        {
            _context = context;
            _donationRequestService = donationRequestService;
        }

        private int? GetUserId()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            return int.TryParse(userIdStr, out var id) ? id : null;
        }

        public async Task<IActionResult> Dashboard()
        {
            var userId = GetUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            var donor = await _context.Donors
                .Include(d => d.BloodType)
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.UserId == userId);

            if (donor == null)
                return RedirectToAction("Login", "Account");

            
            var lastApprovedDonation = await _context.Donations
                .Where(d => d.UserId == userId && d.Status == "Approved")
                .OrderByDescending(d => d.DonationDate)
                .FirstOrDefaultAsync();

     
            var lastRequestedDate = await _context.DonationRequests
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.RequestDate)
                .Select(r => (DateOnly?)r.RequestDate)
                .FirstOrDefaultAsync();

            bool canDonate = true;
            if (lastRequestedDate.HasValue)
            {
                var nextAllowed = lastRequestedDate.Value.ToDateTime(TimeOnly.MinValue).AddMonths(3);
                canDonate = DateTime.Now >= nextAllowed;
            }

            ViewBag.CanDonate = canDonate;
            ViewBag.LastDonation = lastApprovedDonation;
            ViewBag.Donor = donor;

    
            var requests = await _context.DonationRequests
                .Where(r => r.UserId == userId)
                .Include(r => r.ApprovedByNavigation)
                .OrderByDescending(r => r.Id)
                .ToListAsync();

            return View(requests);
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
