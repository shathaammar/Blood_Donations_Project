using Blood_Donations_Project.Common;
using Blood_Donations_Project.Extensions;
using Blood_Donations_Project.Filters;
using Blood_Donations_Project.Services;
using Blood_Donations_Project.ViewModels.BloodRequests;
using Microsoft.AspNetCore.Mvc;

namespace Blood_Donations_Project.Controllers
{
    [SessionAuthorize(AppRoles.Hospital)]
    public class HospitalController : Controller
    {
        private readonly IBloodRequestService _bloodRequestService;

        public HospitalController(IBloodRequestService bloodRequestService)
        {
            _bloodRequestService = bloodRequestService;
        }

        // No Hospital/Dashboard view exists: the shared role-aware dashboard lives at Admin/Dashboard.
        public IActionResult Dashboard()
        {
            return RedirectToAction("Dashboard", "Admin");
        }

        public async Task<IActionResult> RequestBlood()
        {
            if (HttpContext.Session.GetUserId() == null)
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
            if (HttpContext.Session.GetUserId() is not int userId)
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


        // No Hospital/MyRequests view exists: the hospital's requests are listed on Admin/Dashboard.
        public IActionResult MyRequests()
        {
            return RedirectToAction("Dashboard", "Admin");
        }

    }
}
