using Blood_Donations_Project.Common;
using Blood_Donations_Project.Extensions;
using Blood_Donations_Project.Filters;
using Blood_Donations_Project.Services;
using Blood_Donations_Project.ViewModels.Donors;
using Blood_Donations_Project.ViewModels.Inventory;
using Microsoft.AspNetCore.Mvc;
using Blood_Donations_Project.ViewModels.Hospitals;

namespace Blood_Donations_Project.Controllers
{
    [SessionAuthorize(AppRoles.Admin)]
    public class AdminController : Controller
    {
        private readonly IInventoryService _inventoryService;
        private readonly IHospitalService _hospitalService;
        private readonly IDonorManagementService _donorService;
        private readonly IBloodRequestService _bloodRequestService;
        private readonly IDonationRequestService _donationRequestService;
        private readonly IDashboardService _dashboardService;

        public AdminController(
            IInventoryService inventoryService,
            IHospitalService hospitalService,
            IDonorManagementService donorService,
            IBloodRequestService bloodRequestService,
            IDonationRequestService donationRequestService,
            IDashboardService dashboardService)
        {
            _inventoryService = inventoryService;
            _hospitalService = hospitalService;
            _donorService = donorService;
            _bloodRequestService = bloodRequestService;
            _donationRequestService = donationRequestService;
            _dashboardService = dashboardService;
        }

        [SessionAuthorize]
        public async Task<IActionResult> Dashboard(string table = "BloodRequests", string status = "All")
        {
            var role = HttpContext.Session.GetUserRole();

            if (HttpContext.Session.GetUserId() is not int userId || string.IsNullOrWhiteSpace(role))
                return RedirectToAction("Login", "Account");

            var model = await _dashboardService.GetDashboardAsync(userId, role, table, status);
            return View(model);
        }

        // User (Donor) Management

        [HttpGet]
        public async Task<IActionResult> Users()
        {
            var users = await _donorService.GetDonorsAsync();
            return View(users);
        }

        [HttpGet]
        public async Task<IActionResult> EditUser(int id)
        {
            var model = await _donorService.GetDonorForEditAsync(id);
            if (model == null)
                return NotFound();

            model.BloodTypeOptions = await _donorService.GetBloodTypeOptionsAsync();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(EditDonorViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model.BloodTypeOptions = await _donorService.GetBloodTypeOptionsAsync();
                return View(model);
            }

            var result = await _donorService.UpdateDonorAsync(model);
            if (result.IsNotFound)
                return NotFound();

            if (!result.Success)
            {
                ModelState.AddModelError(result.FieldName ?? string.Empty, result.Message);
                model.BloodTypeOptions = await _donorService.GetBloodTypeOptionsAsync();
                return View(model);
            }

            TempData["SuccessMessage"] = "Donor updated successfully.";
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [SessionAuthorize(AppRoles.Admin, Ajax = true)]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var currentUserId = HttpContext.Session.GetUserId();

            var result = await _donorService.DeleteDonorAsync(id, currentUserId);
            return Json(new { success = result.Success, message = result.Message });
        }

        // Hospitals Management

        [HttpGet]
        public async Task<IActionResult> Hospitals()
        {
            var hospitals = await _hospitalService.GetHospitalsAsync();
            return View(hospitals);
        }

        [HttpGet]
        public IActionResult AddHospital()
        {
            return View(new HospitalCreateViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddHospital(HospitalCreateViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var result = await _hospitalService.CreateHospitalAsync(model);

            if (!result.Success)
            {
                ModelState.AddModelError(
                    result.FieldName ?? string.Empty,
                    result.Message);

                return View(model);
            }

            TempData["SuccessMessage"] = "Hospital account created successfully.";
            return RedirectToAction(nameof(Hospitals));
        }

        [HttpGet]
        public async Task<IActionResult> EditHospital(int id)
        {
            var model = await _hospitalService.GetHospitalForEditAsync(id);

            if (model == null)
                return NotFound();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditHospital(HospitalEditViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var result = await _hospitalService.UpdateHospitalAsync(model);

            if (result.IsNotFound)
                return NotFound();

            if (!result.Success)
            {
                ModelState.AddModelError(
                    result.FieldName ?? string.Empty,
                    result.Message);

                return View(model);
            }

            TempData["SuccessMessage"] = "Hospital updated successfully.";
            return RedirectToAction(nameof(Hospitals));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteHospital(int id)
        {
            if (HttpContext.Session.GetUserId() is not int currentUserId)
                return RedirectToAction("Login", "Account");

            var result = await _hospitalService.DeleteHospitalAsync(
                id,
                currentUserId);

            if (result.IsNotFound)
                return NotFound();

            if (!result.Success)
            {
                TempData["Error"] = result.Message;

                return RedirectToAction(nameof(Hospitals));
            }

            TempData["SuccessMessage"] = "Hospital deleted successfully.";
            return RedirectToAction(nameof(Hospitals));
        }

        [HttpPost]
        [SessionAuthorize(AppRoles.Admin, Ajax = true)]
        public async Task<IActionResult> ApproveBloodRequest(int id)
        {
            if (HttpContext.Session.GetUserId() is not int adminId)
                return Json(new { success = false, message = "Not authorized" });

            var result = await _bloodRequestService.ApproveRequestAsync(id);
            return Json(new { success = result.Success, message = result.Message });
        }

        [HttpPost]
        [SessionAuthorize(AppRoles.Admin, Ajax = true)]
        public async Task<IActionResult> RejectBloodRequest(int id)
        {
            if (HttpContext.Session.GetUserId() is not int adminId)
                return Json(new { success = false, message = "Not authorized" });

            var result = await _bloodRequestService.RejectRequestAsync(id);
            return Json(new { success = result.Success, message = result.Message });
        }

        [HttpPost]
        [SessionAuthorize(AppRoles.Admin, Ajax = true)]
        public async Task<IActionResult> VerifyDonorMedical(int userId)
        {
            if (HttpContext.Session.GetUserId() is not int adminId)
                return Json(new { success = false, message = "Not authorized" });

            var result = await _donorService.VerifyMedicalAsync(userId, adminId);
            return Json(new { success = result.Success, message = result.Message });
        }

        // Manage Donor Requests
        [HttpPost]
        [SessionAuthorize(AppRoles.Admin, Ajax = true)]
        public async Task<IActionResult> ApproveDonorRequest(int id)
        {
            // Existing behavior: a missing/invalid id is passed on as 0.
            var adminId = HttpContext.Session.GetUserId() ?? 0;

            var result = await _donationRequestService.ApproveRequestAsync(id, adminId);
            return Json(new { success = result.Success, message = result.Message });
        }

        [HttpPost]
        [SessionAuthorize(AppRoles.Admin, Ajax = true)]
        public async Task<IActionResult> RejectDonorRequest(int id)
        {
            // Existing behavior: a missing/invalid id is passed on as 0.
            var adminId = HttpContext.Session.GetUserId() ?? 0;

            var result = await _donationRequestService.RejectRequestAsync(id, adminId);
            return Json(new { success = result.Success, message = result.Message });
        }

        // Blood Availability Management
        [HttpGet]
        public async Task<IActionResult> BloodAvailability()
        {
            var stock = await _inventoryService.GetInventoryAsync();
            return View(stock);
        }

        [HttpPost]
        [SessionAuthorize(AppRoles.Admin, Ajax = true)]
        public async Task<IActionResult> UpdateInventoryUnits([FromBody] InventoryUnitsRequest dto)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Invalid request" });

            var result = await _inventoryService.SetUnitsAsync(dto.Id, dto.Units);
            return Json(new { success = result.Success, message = result.Message });
        }

        [HttpPost]
        [SessionAuthorize(AppRoles.Admin, Ajax = true)]
        public async Task<IActionResult> AddInventoryUnits([FromBody] InventoryAmountRequest dto)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Invalid request" });

            var result = await _inventoryService.AddUnitsAsync(dto.Id, dto.Amount);
            return Json(new { success = result.Success, message = result.Message });
        }

        [HttpPost]
        [SessionAuthorize(AppRoles.Admin, Ajax = true)]
        public async Task<IActionResult> RemoveInventoryUnits([FromBody] InventoryAmountRequest dto)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Invalid request" });

            var result = await _inventoryService.RemoveUnitsAsync(dto.Id, dto.Amount);
            return Json(new { success = result.Success, message = result.Message });
        }
    }
}
