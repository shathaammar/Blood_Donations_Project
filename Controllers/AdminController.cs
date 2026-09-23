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

        public AdminController(
            BloodDonationContext context,
            IInventoryService inventoryService,
            IHospitalService hospitalService,
            IDonorManagementService donorService)
        {
            _context = context;
            _inventoryService = inventoryService;
            _hospitalService = hospitalService;
            _donorService = donorService;
        }

        private int CalculateAge(DateTime dob)
        {
            var today = DateTime.Today;
            var age = today.Year - dob.Year;
            if (dob.Date > today.AddYears(-age)) age--;
            return age;
        }

        [SessionAuthorize]
        public async Task<IActionResult> Dashboard(string table = "BloodRequests", string status = "All")
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            var role = HttpContext.Session.GetString("UserRole");

            if (!int.TryParse(userIdStr, out var userId) || string.IsNullOrWhiteSpace(role))
                return RedirectToAction("Login", "Account");

            // Cards 
            ViewBag.TotalDonations = await _context.Donations.CountAsync();
            ViewBag.TotalBloodRequests = await _context.BloodRequests.CountAsync();
            ViewBag.TotalDonationRequests = await _context.DonationRequests.CountAsync();
            ViewBag.TotalRequests = (int)ViewBag.TotalBloodRequests + (int)ViewBag.TotalDonationRequests;



            if (role == "Admin")
            {
                table = (table ?? "BloodRequests").Trim();
                status = (status ?? "All").Trim();

                ViewBag.SelectedTable = table;
                ViewBag.SelectedStatus = status;

                var bloodQuery =
                    from br in _context.BloodRequests
                    join u in _context.Users on br.UserId equals u.UserId into users
                    from u in users.DefaultIfEmpty()
                    join bt in _context.BloodTypes on br.BloodTypeId equals bt.BloodTypeId into bts
                    from bt in bts.DefaultIfEmpty()
                    select new BloodRequestRowTable
                    {
                        Id = br.Id,
                        UserId = br.UserId,
                        UserName = u != null ? u.FullName : "-",
                        BloodTypeId = br.BloodTypeId,
                        BloodTypeName = bt != null ? bt.TypeName : "-",
                        RequestDate = br.RequestDate,
                        Quantity = br.Quantity,
                        Status = br.Status
                    };

                if (!string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
                    bloodQuery = bloodQuery.Where(x => x.Status != null && x.Status.Trim() == status);

                ViewBag.AdminRequests = await bloodQuery
                    .OrderByDescending(x => x.Id)
                    .ToListAsync();

                var donorQuery = _context.DonationRequests
                    .Include(r => r.User)
                    .ThenInclude(u => u.Donors)
                    .Include(r => r.ApprovedByNavigation)
                    .AsQueryable();


                if (!string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
                    donorQuery = donorQuery.Where(r => r.Status != null && r.Status.Trim() == status);

                ViewBag.DonorDonationRequests = await donorQuery
                    .OrderByDescending(r => r.Id)
                    .ToListAsync();
            }


            if (role == "Hospital" || role == "BloodBank")
            {
                ViewBag.BloodTypeMap = await _context.BloodTypes
            .ToDictionaryAsync(bt => bt.BloodTypeId, bt => bt.TypeName);

                ViewBag.HospitalRequests = await _context.BloodRequests
                    .Where(br => br.UserId == userId)
                    .OrderByDescending(br => br.Id)
                    .ToListAsync();
            }

            if (role == "Donor")
            {
                var donor = await _context.Donors
                    .Include(d => d.User)
                    .Include(d => d.BloodType)
                    .FirstOrDefaultAsync(d => d.UserId == userId);

                ViewBag.Donor = donor;

 
                var lastApprovedDonation = await _context.Donations
                    .Where(d => d.UserId == userId && d.Status == "Approved")
                    .OrderByDescending(d => d.DonationDate)
                    .FirstOrDefaultAsync();

                ViewBag.LastDonation = lastApprovedDonation;


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


                ViewBag.DonorRequests = await _context.DonationRequests
                    .Where(r => r.UserId == userId)
                    .Include(r => r.ApprovedByNavigation)
                    .OrderByDescending(r => r.Id)
                    .ToListAsync();
            }


            return View();

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
            return View(new HospitalCreate());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddHospital(HospitalCreate model)
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
        public async Task<IActionResult> EditHospital(HospitalEdit model)
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

            var query =
                from br in _context.BloodRequests
                join u in _context.Users on br.UserId equals u.UserId into users
                from u in users.DefaultIfEmpty()
                join bt in _context.BloodTypes on br.BloodTypeId equals bt.BloodTypeId into bts
                from bt in bts.DefaultIfEmpty()
                select new BloodRequestRowTable
                {
                    Id = br.Id,
                    UserId = br.UserId,
                    UserName = u != null ? u.FullName : "-",
                    BloodTypeId = br.BloodTypeId,
                    BloodTypeName = bt != null ? bt.TypeName : "-",
                    RequestDate = br.RequestDate,
                    Quantity = br.Quantity,
                    Status = br.Status
                };

            if (!string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.Status != null && x.Status.Trim() == status);
            }

            var requests = await query.OrderByDescending(x => x.Id).ToListAsync();

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

            var req = await _context.BloodRequests.FirstOrDefaultAsync(br => br.Id == id);
            if (req == null)
                return Json(new { success = false, message = "Request not found" });

            if (!string.Equals(req.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                return Json(new { success = false, message = "Already processed" });

            if (req.BloodTypeId == null)
                return Json(new { success = false, message = "Invalid blood type" });

            var units = req.Quantity ?? 0;
            if (units <= 0)
                return Json(new { success = false, message = "Invalid quantity" });

            var inventory = await _context.BloodInventories
                .FirstOrDefaultAsync(i => i.BloodTypeId == req.BloodTypeId.Value);

            if (inventory == null)
                return Json(new { success = false, message = "Inventory not found" });

            if (inventory.UnitsAvailable < units)
                return Json(new { success = false, message = "Not enough blood units available" });

            inventory.UnitsAvailable -= units;
            req.Status = "Approved";

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Request approved successfully" });
        }

        [HttpPost]
        [SessionAuthorize(AppRoles.Admin, Ajax = true)]
        public async Task<IActionResult> RejectBloodRequest(int id)
        {
            var adminIdStr = HttpContext.Session.GetString("UserId");
            if (!int.TryParse(adminIdStr, out var adminId))
                return Json(new { success = false, message = "Not authorized" });

            var req = await _context.BloodRequests.FirstOrDefaultAsync(br => br.Id == id);
            if (req == null)
                return Json(new { success = false, message = "Request not found" });

            if (!string.Equals(req.Status, "Pending", StringComparison.OrdinalIgnoreCase))
                return Json(new { success = false, message = "Already processed" });

            req.Status = "Rejected";

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Request rejected" });
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

            var req = await _context.DonationRequests
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (req == null)
                return Json(new { success = false, message = "Request not found" });

            if (req.Status != "Pending")
                return Json(new { success = false, message = "Already processed" });

            var donor = await _context.Donors
                .FirstOrDefaultAsync(d => d.UserId == req.UserId);

            if (donor == null)
                return Json(new { success = false, message = "Donor profile not found" });

            if (req.User.DateOfBirth == default)
                return Json(new { success = false, message = "Date of birth missing." });

            int age = CalculateAge(req.User.DateOfBirth);
            if (age < 18)
                return Json(new { success = false, message = "Donor must be 18+." });

            if (!donor.IsMedicalVerified)
                return Json(new { success = false, message = "Medical data not verified." });

            if (donor.LastDonationDate.HasValue)
            {
                var nextAllowed = donor.LastDonationDate.Value
                    .ToDateTime(TimeOnly.MinValue)
                    .AddMonths(3);

                if (DateTime.Now < nextAllowed)
                    return Json(new
                    {
                        success = false,
                        message = $"Can donate again after {nextAllowed:dd/MM/yyyy}"
                    });
            }

            req.Status = "Approved";
            req.ApprovedBy = adminId;
            req.ApprovedDate = DateOnly.FromDateTime(DateTime.Now);

            _context.Donations.Add(new Donation
            {
                UserId = req.UserId,
                ApprovedBy = adminId,
                DonationDate = DateOnly.FromDateTime(DateTime.Now),
                Status = "Approved"
            });

            if (donor.BloodTypeId.HasValue)
            {
                var inventory = await _context.BloodInventories
                    .FirstOrDefaultAsync(i => i.BloodTypeId == donor.BloodTypeId);

                if (inventory != null)
                    inventory.UnitsAvailable += 1;
            }

            donor.LastDonationDate = DateOnly.FromDateTime(DateTime.Now);

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Donor request approved." });
        }

        [HttpPost]
        [SessionAuthorize(AppRoles.Admin, Ajax = true)]
        public async Task<IActionResult> RejectDonorRequest(int id)
        {
            var adminIdStr = HttpContext.Session.GetString("UserId");
            int.TryParse(adminIdStr, out var adminId);

            var req = await _context.DonationRequests
                .FirstOrDefaultAsync(r => r.Id == id);

            if (req == null)
                return Json(new { success = false, message = "Request not found" });

            if (req.Status != "Pending")
                return Json(new { success = false, message = "Already processed" });

            req.Status = "Rejected";
            req.ApprovedBy = adminId;
            req.ApprovedDate = DateOnly.FromDateTime(DateTime.Now);

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Donor request rejected." });
        }

        public async Task<IActionResult> Statistics()
        {
            var bloodTypeStats = await _context.Donors
                .Include(d => d.BloodType)
                .GroupBy(d => d.BloodType.TypeName)
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
