using Blood_Donations_Project.Common;
using Blood_Donations_Project.Filters;
using Blood_Donations_Project.Models;
using Blood_Donations_Project.Services;
using Blood_Donations_Project.ViewModels;
using Blood_Donations_Project.ViewModels.Donors;
using Blood_Donations_Project.ViewModels.Inventory;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;
using Blood_Donations_Project.ViewModels.Hospitals;

namespace Blood_Donations_Project.Controllers
{
    [SessionAuthorize(AppRoles.Admin)]
    public class AdminController : Controller
    {
        private readonly BloodDonationContext _context;
        private readonly IInventoryService _inventoryService;
        private readonly IHospitalService _hospitalService;
        private readonly IDonorManagementService _donorService;
        private readonly IBloodRequestService _bloodRequestService;
        private readonly IDonationRequestService _donationRequestService;
        private readonly IDashboardService _dashboardService;

        public AdminController(
            BloodDonationContext context,
            IInventoryService inventoryService,
            IHospitalService hospitalService,
            IDonorManagementService donorService,
            IBloodRequestService bloodRequestService,
            IDonationRequestService donationRequestService,
            IDashboardService dashboardService)
        {
            _context = context;
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
            var userIdStr = HttpContext.Session.GetString("UserId");
            var role = HttpContext.Session.GetString("UserRole");

            if (!int.TryParse(userIdStr, out var userId) || string.IsNullOrWhiteSpace(role))
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

            TempData["Success"] = "Donor updated successfully.";
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [SessionAuthorize(AppRoles.Admin, Ajax = true)]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var myIdStr = HttpContext.Session.GetString("UserId");
            int? currentUserId = int.TryParse(myIdStr, out var myId) ? myId : null;

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

            TempData["Success"] = "Hospital account created successfully.";
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

            TempData["Success"] = "Hospital updated successfully.";
            return RedirectToAction(nameof(Hospitals));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteHospital(int id)
        {
            var userIdValue = HttpContext.Session.GetString("UserId");

            if (!int.TryParse(userIdValue, out var currentUserId))
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

            TempData["Success"] = "Hospital deleted successfully.";
            return RedirectToAction(nameof(Hospitals));
        }

        public async Task<IActionResult> ManageDonations()
        {
            var donations = await _context.Donations
                .Include(d => d.User)
                .ThenInclude(u => u.Donors)
                .ThenInclude(d => d.BloodType)
                .Include(d => d.ApprovedByNavigation)
                .OrderByDescending(d => d.Id)
                .ToListAsync();

            return View(donations);
        }

        [HttpPost]
        [SessionAuthorize(AppRoles.Admin, Ajax = true)]
        public async Task<IActionResult> ApproveDonation(int id)
        {
            var adminIdStr = HttpContext.Session.GetString("UserId");
            if(!int.TryParse(adminIdStr, out var adminId))
                return Json(new {success = false, message = "Not authorized" });

            var donation = await _context.Donations.FindAsync(id);
            if (donation == null)
                return Json(new { success = false, message = "Donation not found" });

            donation.Status = "Approved";
            donation.ApprovedBy = adminId;
            donation.DonationDate = DateOnly.FromDateTime(DateTime.Now);

            var donor = await _context.Donors.FirstOrDefaultAsync(d => d.UserId == donation.UserId);
            if(donor != null)
            {
                donor.LastDonationDate = DateOnly.FromDateTime(DateTime.Now);
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Donation approved successfully" });                
        }

        [HttpPost]
        [SessionAuthorize(AppRoles.Admin, Ajax = true)]
        public async Task<IActionResult> RejectDonation(int id)
        {
            var adminIdStr = HttpContext.Session.GetString("UserId");
            if (!int.TryParse(adminIdStr, out var adminId))
                return Json(new { success = false, message = "Not authorized" });

            var donation = await _context.Donations.FindAsync(id);
            if (donation == null)
                return Json(new { success = false, message = "Donation not found" });

            donation.Status = "Rejected";
            donation.ApprovedBy = adminId;

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Donation rejected" });
        }

        // Manage Blood Requests
        public async Task<IActionResult> ManageBloodRequests(string status = "Pending")
        {
            status = (status ?? "Pending").Trim();

            var requests = await _bloodRequestService.GetRequestsForAdminAsync(status);

            ViewBag.SelectedStatus = status;
            return View(requests);
        }


        [HttpPost]
        [SessionAuthorize(AppRoles.Admin, Ajax = true)]
        public async Task<IActionResult> ApproveBloodRequest(int id)
        {
            var adminIdStr = HttpContext.Session.GetString("UserId");
            if (!int.TryParse(adminIdStr, out var adminId))
                return Json(new { success = false, message = "Not authorized" });

            var result = await _bloodRequestService.ApproveRequestAsync(id);
            return Json(new { success = result.Success, message = result.Message });
        }

        [HttpPost]
        [SessionAuthorize(AppRoles.Admin, Ajax = true)]
        public async Task<IActionResult> RejectBloodRequest(int id)
        {
            var adminIdStr = HttpContext.Session.GetString("UserId");
            if (!int.TryParse(adminIdStr, out var adminId))
                return Json(new { success = false, message = "Not authorized" });

            var result = await _bloodRequestService.RejectRequestAsync(id);
            return Json(new { success = result.Success, message = result.Message });
        }

        [HttpPost]
        [SessionAuthorize(AppRoles.Admin, Ajax = true)]
        public async Task<IActionResult> VerifyDonorMedical(int userId)
        {
            var adminIdStr = HttpContext.Session.GetString("UserId");
            if (!int.TryParse(adminIdStr, out var adminId))
                return Json(new { success = false, message = "Not authorized" });

            var result = await _donorService.VerifyMedicalAsync(userId, adminId);
            return Json(new { success = result.Success, message = result.Message });
        }

        // Manage Donor Requests
        [HttpPost]
        [SessionAuthorize(AppRoles.Admin, Ajax = true)]
        public async Task<IActionResult> ApproveDonorRequest(int id)
        {
            var adminIdStr = HttpContext.Session.GetString("UserId");
            int.TryParse(adminIdStr, out var adminId);

            var result = await _donationRequestService.ApproveRequestAsync(id, adminId);
            return Json(new { success = result.Success, message = result.Message });
        }

        [HttpPost]
        [SessionAuthorize(AppRoles.Admin, Ajax = true)]
        public async Task<IActionResult> RejectDonorRequest(int id)
        {
            var adminIdStr = HttpContext.Session.GetString("UserId");
            int.TryParse(adminIdStr, out var adminId);

            var result = await _donationRequestService.RejectRequestAsync(id, adminId);
            return Json(new { success = result.Success, message = result.Message });
        }

        public async Task<IActionResult> Statistics()
        {
            var bloodTypeStats = await _context.Donors
                .Where(d => d.BloodType != null)
                .GroupBy(d => d.BloodType!.TypeName)
                .Select(g => new
                {
                    BloodType = g.Key,
                    Count = g.Count()
                })
                .ToListAsync();

            ViewBag.BloodTypeStats = bloodTypeStats;

            ViewBag.TotalDonations = await _context.Donations.CountAsync(d => d.Status == "Approved");
            ViewBag.PendingDonations = await _context.Donations.CountAsync(d => d.Status == "Pending");
            ViewBag.RejectedDonations = await _context.Donations.CountAsync(d => d.Status == "Rejected");

            ViewBag.ApprovedRequests = await _context.BloodRequests.CountAsync(br => br.Status == "Approved");
            ViewBag.PendingRequests = await _context.BloodRequests.CountAsync(br => br.Status == "Pending");
            ViewBag.RejectedRequests = await _context.BloodRequests.CountAsync(br => br.Status == "Rejected");

            return View();
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
