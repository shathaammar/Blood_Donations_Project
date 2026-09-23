using Blood_Donations_Project.Common;
using Blood_Donations_Project.Filters;
using Blood_Donations_Project.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Blood_Donations_Project.Controllers
{
    [SessionAuthorize(AppRoles.Donor)]
    public class DonorController : Controller
    {
        private readonly BloodDonationContext _context;

        public DonorController(BloodDonationContext context)
        {
            _context = context;
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

            var hasPendingRequest = await _context.DonationRequests
                .AnyAsync(r => r.UserId == userId && r.Status == "Pending");

            if (hasPendingRequest)
            {
                TempData["ErrorMessage"] = "You already have a pending donation request! Please wait for admin Approval.";
                return RedirectToAction("Dashboard", "Admin");
            }

            var lastRequestedDate = await _context.DonationRequests
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.RequestDate)
                .Select(r => (DateOnly?)r.RequestDate)
                .FirstOrDefaultAsync();

            if (lastRequestedDate.HasValue)
            {
                var nextAllowed = lastRequestedDate.Value.ToDateTime(TimeOnly.MinValue).AddMonths(3);
                if (DateTime.Now < nextAllowed)
                {
                    TempData["ErrorMessage"] = $"You can request again after {nextAllowed:dd/MM/yyyy}.";
                    return RedirectToAction("Dashboard", "Admin");
                }
            }

            var donor = await _context.Donors
                 .Include(d => d.User)
                 .Include(d => d.BloodType)
                 .FirstOrDefaultAsync(d => d.UserId == userId);

            if (donor == null)
            {
                TempData["ErrorMessage"] = "Donor profile not found.";
                return RedirectToAction("Dashboard", "Admin");
            }

            return View(donor);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestDonation(IFormCollection _)
        {
            var userId = GetUserId();
            if (userId == null) return RedirectToAction("Login", "Account");

            var hasPendingRequest = await _context.DonationRequests
                .AnyAsync(r => r.UserId == userId && r.Status == "Pending");

            if (hasPendingRequest)
            {
                TempData["ErrorMessage"] = "You already have a pending donation request! Please wait for admin decision.";
                return RedirectToAction("Dashboard", "Admin");
            }

            var lastRequestedDate = await _context.DonationRequests
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.RequestDate)
                .Select(r => (DateOnly?)r.RequestDate)
                .FirstOrDefaultAsync();

            if (lastRequestedDate.HasValue)
            {
                var nextAllowed = lastRequestedDate.Value.ToDateTime(TimeOnly.MinValue).AddMonths(3);
                if (DateTime.Now < nextAllowed)
                {
                    TempData["ErrorMessage"] = $"You can reque  st again after {nextAllowed:dd/MM/yyyy}.";
                    return RedirectToAction("Dashboard");
                }
            }

            _context.DonationRequests.Add(new DonationRequest
            {
                UserId = userId.Value,
                RequestDate = DateOnly.FromDateTime(DateTime.Now),
                Status = "Pending"
            });

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Donation request submitted successfully!";
            return RedirectToAction("Dashboard", "Admin");
        }
    }
}
