using Blood_Donations_Project.Common;
using Blood_Donations_Project.Filters;
using Blood_Donations_Project.Models;
using Blood_Donations_Project.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Blood_Donations_Project.Controllers
{
    [SessionAuthorize(AppRoles.Hospital)]
    public class HospitalController : Controller
    {
        private readonly BloodDonationContext _context;

        public HospitalController(BloodDonationContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Dashboard()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (!int.TryParse(userIdStr, out var userId))
                return RedirectToAction("Login", "Account");

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
                return RedirectToAction("Login", "Account");

            ViewBag.User = user;

            ViewBag.BloodTypeMap = await _context.BloodTypes
                .ToDictionaryAsync(bt => bt.BloodTypeId, bt => bt.TypeName);

            var bloodRequests = await _context.BloodRequests
                .Where(br => br.UserId == userId)
                .OrderByDescending(br => br.Id)
                .ToListAsync();

            return View(bloodRequests);
        }

        public async Task<IActionResult> RequestBlood()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (!int.TryParse(userIdStr, out var userId))
                return RedirectToAction("Login", "Account");

            var bloodTypes = await _context.BloodTypes
                .Select(bt => new { bt.BloodTypeId, bt.TypeName })
                .ToListAsync();

            ViewBag.BloodTypes = new SelectList(bloodTypes, "BloodTypeId", "TypeName");

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestBlood(BloodRequestViewModel model)
        {
            var userIdStr = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
                return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
            {
                var bloodTypes = await _context.BloodTypes
                    .Select(bt => new { bt.BloodTypeId, bt.TypeName })
                    .ToListAsync();

                ViewBag.BloodTypes = new SelectList(bloodTypes, "BloodTypeId", "TypeName");
                return View(model);
            }

            var bloodRequest = new BloodRequest
            {
                UserId = userId,
                BloodTypeId = model.BloodTypeId,
                Quantity = model.Quantity,
                RequestDate = DateOnly.FromDateTime(DateTime.Now),
                Status = "Pending"
            };

            _context.BloodRequests.Add(bloodRequest);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Blood request submitted successfully! Waiting for admin approval.";
            return RedirectToAction("Dashboard", "Admin");
        }


        public async Task<IActionResult> MyRequests()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (!int.TryParse(userIdStr, out var userId))
                return RedirectToAction("Login", "Account");

            var bloodRequests = await _context.BloodRequests
                .Where(br => br.UserId == userId)
                .OrderByDescending(br => br.Id)
                .ToListAsync();

            return View(bloodRequests);
        }

    }
}
