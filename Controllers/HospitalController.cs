using Blood_Donations_Project.Common;
using Blood_Donations_Project.Filters;
using Blood_Donations_Project.Models;
using Blood_Donations_Project.Services;
using Blood_Donations_Project.ViewModels.BloodRequests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Blood_Donations_Project.Controllers
{
    [SessionAuthorize(AppRoles.Hospital)]
    public class HospitalController : Controller
    {
        private readonly BloodDonationContext _context;
        private readonly IBloodRequestService _bloodRequestService;

        public HospitalController(BloodDonationContext context, IBloodRequestService bloodRequestService)
        {
            _context = context;
            _bloodRequestService = bloodRequestService;
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

            var model = new RequestBloodViewModel
            {
                BloodTypeOptions = await _bloodRequestService.GetBloodTypeOptionsAsync()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestBlood(RequestBloodViewModel model)
        {
            var userIdStr = HttpContext.Session.GetString("UserId");

            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out var userId))
                return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
            {
                model.BloodTypeOptions = await _bloodRequestService.GetBloodTypeOptionsAsync();
                return View(model);
            }

            var result = await _bloodRequestService.CreateRequestAsync(userId, model);
            if (!result.Success)
            {
                ModelState.AddModelError(result.FieldName ?? string.Empty, result.Message);
                model.BloodTypeOptions = await _bloodRequestService.GetBloodTypeOptionsAsync();
                return View(model);
            }

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
