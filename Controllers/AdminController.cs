using Blood_Donations_Project.Filters;
using Blood_Donations_Project.Models;
using Blood_Donations_Project.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace Blood_Donations_Project.Controllers
{
    [SessionAuthorize(AppRoles.Admin)]
    public class AdminController : Controller
    {
        private readonly BloodDonationContext _context;

        public AdminController(BloodDonationContext context)
        {
            _context = context;
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
            var users = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Donors).ThenInclude(d => d.BloodType)
                .Where(u => u.Role != null && u.Role.RoleName == "Donor")
                .Select(u => new AdminUserRow
                {
                    UserId = u.UserId,
                    FullName = u.FullName,
                    UserName = u.UserName,
                    Email = u.Email,
                    MobileNo = u.MobileNo,
                    Address = u.Address,
                    DateOfBirth = u.DateOfBirth,
                    Gender = u.Gender,
                    BloodTypeName = u.Donors.Select(d => d.BloodType.TypeName).FirstOrDefault(),
                    HealthStatus = u.Donors.Select(d => d.HealthStatus).FirstOrDefault()
                })

                .OrderBy(u => u.UserId)
                .ToListAsync();

            return View(users);
        }

        [HttpGet]
        public async Task<IActionResult> EditUser(int id)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Donors)
                .FirstOrDefaultAsync(u => u.UserId == id);

            if (user == null || user.Role.RoleName != "Donor")
                return NotFound();

            var donor = user.Donors.FirstOrDefault();
            if (donor == null)
                return NotFound();

            var model = new EditUser
            {
                UserId = user.UserId,
                FullName = user.FullName,
                UserName = user.UserName,
                Email = user.Email,
                MobileNo = user.MobileNo,
                Address = user.Address,
                DateOfBirth = user.DateOfBirth,
                HealthStatus = donor.HealthStatus,
                BloodTypeId = donor.BloodTypeId,
                Gender = user.Gender
            };

            ViewBag.BloodTypes = await _context.BloodTypes
               .Select(bt => new { bt.BloodTypeId, bt.TypeName })
               .ToListAsync();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditUser(EditUser model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.BloodTypes = await _context.BloodTypes.ToListAsync();
                return View(model);
            }

            var user = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Donors)
                .FirstOrDefaultAsync(u => u.UserId == model.UserId);

            if (user == null || user.Role.RoleName != "Donor")
                return NotFound();

            var donor = user.Donors.FirstOrDefault();
            if (donor == null)
                return NotFound();

            user.FullName = model.FullName;
            user.UserName = model.UserName;
            user.Email = model.Email;
            user.MobileNo = model.MobileNo;
            user.Address = model.Address;
            user.DateOfBirth = model.DateOfBirth;

            donor.HealthStatus = model.HealthStatus;
            donor.BloodTypeId = model.BloodTypeId;
            user.Gender = model.Gender;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Donor updated successfully.";
            return RedirectToAction(nameof(Users));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [SessionAuthorize(AppRoles.Admin, Ajax = true)]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var myIdStr = HttpContext.Session.GetString("UserId");
            if (int.TryParse(myIdStr, out var myId) && myId == id)
            {
                return Json(new { success = false, message = "You cannot delete your own account." });
            }

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == id);

            if (user == null)
                return Json(new { success = false, message = "User not found." });

            if (user.Role?.RoleName != "Donor")
                return Json(new { success = false, message = "This action is only for Donor accounts." });

            using var tx = await _context.Database.BeginTransactionAsync();

            try
            {
                var tokens = await _context.PasswordReset.Where(x => x.UserId == id).ToListAsync();
                if (tokens.Any()) _context.PasswordReset.RemoveRange(tokens);

                var donationReqs = await _context.DonationRequests.Where(x => x.UserId == id).ToListAsync();
                if (donationReqs.Any()) _context.DonationRequests.RemoveRange(donationReqs);

                var donations = await _context.Donations.Where(x => x.UserId == id).ToListAsync();
                if (donations.Any()) _context.Donations.RemoveRange(donations);

                var donor = await _context.Donors.FirstOrDefaultAsync(d => d.UserId == id);
                if (donor != null) _context.Donors.Remove(donor);

                var userRoles = await _context.UserRoles.Where(ur => ur.UserId == id).ToListAsync();
                if (userRoles.Any()) _context.UserRoles.RemoveRange(userRoles);

                _context.Users.Remove(user);

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return Json(new { success = true, message = "Donor deleted successfully." });
            }
            catch
            {
                await tx.RollbackAsync();
                return Json(new { success = false, message = "Failed to delete donor." });
            }
        }

        // Hospitals Management

        public async Task<IActionResult> Hospitals()
        {
            var hospitals = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Role != null && u.Role.RoleName == "Hospital")
                .OrderBy(u => u.UserId)
                .ToListAsync();

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

            if (await _context.Users.AnyAsync(u => u.Email == model.Email))
            {
                ModelState.AddModelError(nameof(model.Email), "Email already exists.");
                return View(model);
            }

            if (await _context.Users.AnyAsync(u => u.UserName == model.UserName))
            {
                ModelState.AddModelError(nameof(model.UserName), "UserName already exists.");
                return View(model);
            }

            var hospitalRoleId = await _context.Roles
                .Where(r => r.RoleName == "Hospital")
                .Select(r => r.RoleId)
                .FirstOrDefaultAsync();

            if (hospitalRoleId == 0)
            {
                ModelState.AddModelError("", "Hospital role not found in Roles table.");
                return View(model);
            }

            var user = new User
            {
                UserName = model.UserName,
                FullName = model.FullName,
                Email = model.Email,
                MobileNo = model.MobileNo,
                Address = model.Address,
                RoleId = hospitalRoleId
            };

            var hasher = new PasswordHasher<User>();
            user.Password = hasher.HashPassword(user, model.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Hospital account created successfully.";
            return RedirectToAction(nameof(Hospitals));
        }

        [HttpGet]
        public async Task<IActionResult> EditHospital(int id)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == id);

            if (user == null) return NotFound();
            if (!string.Equals(user.Role?.RoleName, "Hospital", StringComparison.OrdinalIgnoreCase))
                return NotFound();

            var vm = new HospitalEdit
            {
                UserId = user.UserId,
                UserName = user.UserName ?? "",
                FullName = user.FullName ?? "",
                Email = user.Email ?? "",
                MobileNo = user.MobileNo,
                Address = user.Address
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditHospital(HospitalEdit model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == model.UserId);

            if (user == null) return NotFound();
            if (!string.Equals(user.Role?.RoleName, "Hospital", StringComparison.OrdinalIgnoreCase))
                return NotFound();

            if (await _context.Users.AnyAsync(u => u.Email == model.Email && u.UserId != model.UserId))
            {
                ModelState.AddModelError(nameof(model.Email), "Email already exists.");
                return View(model);
            }

            if (await _context.Users.AnyAsync(u => u.UserName == model.UserName && u.UserId != model.UserId))
            {
                ModelState.AddModelError(nameof(model.UserName), "UserName already exists.");
                return View(model);
            }

            user.UserName = model.UserName;
            user.FullName = model.FullName;
            user.Email = model.Email;
            user.MobileNo = model.MobileNo;
            user.Address = model.Address;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Hospital updated successfully.";
            return RedirectToAction(nameof(Hospitals));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteHospital(int id)
        {
            var myIdStr = HttpContext.Session.GetString("UserId");
            if (int.TryParse(myIdStr, out var myId) && myId == id)
            {
                TempData["Error"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Hospitals));
            }

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == id);

            if (user == null) return NotFound();

            if (!string.Equals(user.Role?.RoleName, "Hospital", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "This action is only for Hospital accounts.";
                return RedirectToAction(nameof(Hospitals));
            }

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var tokens = await _context.PasswordReset.Where(x => x.UserId == id).ToListAsync();
                if (tokens.Any()) _context.PasswordReset.RemoveRange(tokens);

                var bloodReqs = await _context.BloodRequests.Where(br => br.UserId == id).ToListAsync();
                if (bloodReqs.Any()) _context.BloodRequests.RemoveRange(bloodReqs);

                var donations = await _context.Donations.Where(d => d.UserId == id).ToListAsync();
                if (donations.Any()) _context.Donations.RemoveRange(donations);

                var userRoles = await _context.UserRoles.Where(ur => ur.UserId == id).ToListAsync();
                if (userRoles.Any()) _context.UserRoles.RemoveRange(userRoles);

                _context.Users.Remove(user);

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                TempData["Success"] = "Hospital deleted successfully.";
                return RedirectToAction(nameof(Hospitals));
            }
            catch
            {
                await tx.RollbackAsync();
                TempData["Error"] = "Failed to delete hospital. Please try again.";
                return RedirectToAction(nameof(Hospitals));
            }
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

            var donor = await _context.Donors
                .FirstOrDefaultAsync(d => d.UserId == userId);

            if (donor == null)
                return Json(new { success = false, message = "Donor profile not found" });

            donor.IsMedicalVerified = true;
            donor.MedicalVerifiedBy = adminId;
            donor.MedicalVerifiedDate = DateOnly.FromDateTime(DateTime.Now);

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Donor medical data verified successfully." });
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
            var stock = await _context.BloodInventories
                .Include(i => i.BloodType)
                .OrderBy(i => i.BloodType!.TypeName)
                .ToListAsync();

            return View(stock);
        }

        public class InventoryUnitsDto
        {
            public int Id { get; set; }
            public int Units { get; set; }
        }

        public class InventoryAmountDto
        {
            public int Id { get; set; }
            public int Amount { get; set; }
        }

        [HttpPost]
        [SessionAuthorize(AppRoles.Admin, Ajax = true)]
        public async Task<IActionResult> UpdateInventoryUnits([FromBody] InventoryUnitsDto dto)
        {
            if (dto.Units < 0) return Json(new { success = false, message = "Units must be >= 0" });

            var inv = await _context.BloodInventories.FirstOrDefaultAsync(x => x.Id == dto.Id);
            if (inv == null) return Json(new { success = false, message = "Inventory row not found" });

            inv.UnitsAvailable = dto.Units;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Units updated successfully" });
        }

        [HttpPost]
        [SessionAuthorize(AppRoles.Admin, Ajax = true)]
        public async Task<IActionResult> AddInventoryUnits([FromBody] InventoryAmountDto dto)
        {
            if (dto.Amount <= 0) return Json(new { success = false, message = "Amount must be > 0" });

            var inv = await _context.BloodInventories.FirstOrDefaultAsync(x => x.Id == dto.Id);
            if (inv == null) return Json(new { success = false, message = "Inventory row not found" });

            inv.UnitsAvailable += dto.Amount;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"+{dto.Amount} units added" });
        }

        [HttpPost]
        [SessionAuthorize(AppRoles.Admin, Ajax = true)]
        public async Task<IActionResult> RemoveInventoryUnits([FromBody] InventoryAmountDto dto)
        {
            if (dto.Amount <= 0) return Json(new { success = false, message = "Amount must be > 0" });

            var inv = await _context.BloodInventories.FirstOrDefaultAsync(x => x.Id == dto.Id);
            if (inv == null) return Json(new { success = false, message = "Inventory row not found" });

            if (inv.UnitsAvailable < dto.Amount)
                return Json(new { success = false, message = "Not enough units to remove" });

            inv.UnitsAvailable -= dto.Amount;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"-{dto.Amount} units removed" });
        }
    }
}
