using Blood_Donations_Project.Filters;
using Blood_Donations_Project.Models;
using Blood_Donations_Project.Services;
using Blood_Donations_Project.ViewModels;
using Blood_Donations_Project.ViewModels.Profile;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Blood_Donations_Project.Controllers
{
    public class AccountController : Controller
    {
        private readonly BloodDonationContext _context;
        private readonly IConfiguration _config;
        private readonly IProfileService _profileService;

        public AccountController(BloodDonationContext context, IConfiguration config, IProfileService profileService)
        {
            _context = context;
            _config = config;
            _profileService = profileService;
        }

        // LOGIN

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == model.Email);

            if (user == null)
            {
                ModelState.AddModelError("", "Invalid email or password.");
                return View(model);
            }

            var hasher = new PasswordHasher<User>();
            var result = hasher.VerifyHashedPassword(user, user.Password, model.Password);

            if (result == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError("", "Invalid email or password.");
                return View(model);
            }

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? ""),
                new Claim(ClaimTypes.Role, user.Role?.RoleName ?? "")
            };

            var keyString = _config["Jwt:Key"];
            if (string.IsNullOrEmpty(keyString))
                throw new Exception("JWT Key is missing in appsettings.json");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(keyString));

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: model.RememberMe ? DateTime.UtcNow.AddDays(7) : DateTime.UtcNow.AddHours(2),
                signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
            );

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

            Response.Cookies.Append("jwt", tokenString, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps,
                Expires = model.RememberMe ? DateTimeOffset.Now.AddDays(7) : DateTimeOffset.Now.AddHours(2)
            });

            HttpContext.Session.SetString("UserRole", user.Role?.RoleName ?? "");
            HttpContext.Session.SetString("UserId", user.UserId.ToString());

            return RedirectToAction("Dashboard", "Admin");
        }

        // REGISTER (DONOR ONLY)

        [HttpGet]
        public async Task<IActionResult> Register()
        {
            await LoadRegisterDropdowns();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (model.DateOfBirth == null)
                ModelState.AddModelError(nameof(model.DateOfBirth), "Date of birth is required.");
            else
            {
                var age = CalculateAge(model.DateOfBirth.Value);
                if (age < 18)
                    ModelState.AddModelError(nameof(model.DateOfBirth), "You must be 18 or older to register as a donor.");
            }

            if (!model.BloodTypeId.HasValue)
                ModelState.AddModelError(nameof(model.BloodTypeId), "Blood type is required.");

            if (string.IsNullOrWhiteSpace(model.Gender))
                ModelState.AddModelError(nameof(model.Gender), "Gender is required.");

            if (!ModelState.IsValid)
            {
                await LoadRegisterDropdowns();
                return View(model);
            }

            if (await _context.Users.AnyAsync(u => u.Email == model.Email))
            {
                ModelState.AddModelError(nameof(model.Email), "Email already exists.");
                await LoadRegisterDropdowns();
                return View(model);
            }

            if (await _context.Users.AnyAsync(u => u.UserName == model.UserName))
            {
                ModelState.AddModelError(nameof(model.UserName), "Username already exists.");
                await LoadRegisterDropdowns();
                return View(model);
            }

            var donorRoleId = await _context.Roles
                .Where(r => r.RoleName == "Donor")
                .Select(r => r.RoleId)
                .FirstOrDefaultAsync();

            if (donorRoleId == 0)
            {
                ModelState.AddModelError("", "Donor role not configured.");
                await LoadRegisterDropdowns();
                return View(model);
            }

            var hasher = new PasswordHasher<User>();

            var user = new User
            {
                UserName = model.UserName,
                Email = model.Email,
                FullName = model.FullName,
                MobileNo = model.MobileNo,
                Address = model.Address,
                RoleId = donorRoleId,
                DateOfBirth = model.DateOfBirth!.Value,
                Gender = model.Gender
            };

            user.Password = hasher.HashPassword(user, model.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var donor = new Donor
            {
                UserId = user.UserId,
                BloodTypeId = model.BloodTypeId!.Value,
                HealthStatus = model.HealthStatus,
                IsAvailable = true,
                LastDonationDate = null,
                IsMedicalVerified = false
            };

            _context.Donors.Add(donor);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Registration successful! Please login.";
            return RedirectToAction("Login");
        }

        private async Task LoadRegisterDropdowns()
        {
            var bloodTypes = await _context.BloodTypes
                .Select(bt => new { bt.BloodTypeId, bt.TypeName })
                .ToListAsync();

            ViewBag.BloodTypes = new SelectList(bloodTypes, "BloodTypeId", "TypeName");
        }

        private int CalculateAge(DateTime dob)
        {
            var today = DateTime.Today;
            var age = today.Year - dob.Year;
            if (dob.Date > today.AddYears(-age)) age--;
            return age;
        }

        // LOGOUT

        [HttpGet]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("jwt");
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        // PROFILE 

        [HttpGet]
        [SessionAuthorize]
        public async Task<IActionResult> Profile()
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (!int.TryParse(userIdStr, out var userId))
                return RedirectToAction("Login");

            var roleName = HttpContext.Session.GetString("UserRole") ?? "";

            var model = await _profileService.GetProfileAsync(userId, roleName);
            if (model == null)
                return RedirectToAction("Login");

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [SessionAuthorize]
        public async Task<IActionResult> Profile(ProfileViewModel model)
        {
            var userIdStr = HttpContext.Session.GetString("UserId");
            if (!int.TryParse(userIdStr, out var userId))
                return RedirectToAction("Login");

            if (model.UserId != userId)
                return Forbid();

            var roleName = HttpContext.Session.GetString("UserRole") ?? "";

            var canUpdate = _profileService.CheckCanUpdate(roleName);
            if (!canUpdate.Success)
            {
                // Existing behavior: this notice is shown with the success styling.
                TempData["Success"] = canUpdate.Message;
                return RedirectToAction("Profile");
            }

            foreach (var error in _profileService.ValidateForRole(model, roleName))
                ModelState.AddModelError(error.FieldName ?? string.Empty, error.Message);

            if (!ModelState.IsValid)
            {
                await _profileService.PrepareForRedisplayAsync(model, userId, roleName);
                return View(model);
            }

            var result = await _profileService.UpdateProfileAsync(userId, roleName, model);
            if (result.IsNotFound)
                return RedirectToAction("Login");

            TempData["Success"] = result.Message;
            return RedirectToAction("Profile");
        }

        // FORGOT/RESET PASSWORD 

        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPassword model)
        {
            if (string.IsNullOrWhiteSpace(model.Email))
            {
                ModelState.AddModelError("", "Email is required.");
                return View(model);
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);

            TempData["SuccessMessage"] = "If the email exists, a reset link has been sent.";

            if (user == null)
                return RedirectToAction("Login");

            var oldTokens = await _context.PasswordReset
                .Where(t => t.UserId == user.UserId && !t.Used)
                .ToListAsync();

            if (oldTokens.Any())
                _context.PasswordReset.RemoveRange(oldTokens);

            var token = Guid.NewGuid().ToString("N");

            _context.PasswordReset.Add(new PasswordReset
            {
                UserId = user.UserId,
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddMinutes(30),
                Used = false
            });

            await _context.SaveChangesAsync();

            var resetUrl = Url.Action("ResetPassword", "Account",
                new { email = user.Email, token = token }, Request.Scheme);

            Console.WriteLine("RESET LINK => " + resetUrl);

            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult ResetPassword(string email, string token)
        {
            return View(new ResetPassword
            {
                Email = email ?? "",
                Token = token ?? ""
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPassword model)
        {
            if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Token))
            {
                ModelState.AddModelError("", "Invalid reset link.");
                return View(model);
            }

            if (string.IsNullOrWhiteSpace(model.NewPassword))
            {
                ModelState.AddModelError("", "New password is required.");
                return View(model);
            }

            if (model.NewPassword != model.ConfirmPassword)
            {
                ModelState.AddModelError("", "Passwords do not match.");
                return View(model);
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (user == null)
            {
                ModelState.AddModelError("", "Invalid reset link.");
                return View(model);
            }

            var tokenRow = await _context.PasswordReset
                .FirstOrDefaultAsync(t =>
                    t.UserId == user.UserId &&
                    t.Token == model.Token &&
                    !t.Used);

            if (tokenRow == null || tokenRow.ExpiresAt < DateTime.UtcNow)
            {
                ModelState.AddModelError("", "Reset link expired or invalid.");
                return View(model);
            }

            var hasher = new PasswordHasher<User>();
            user.Password = hasher.HashPassword(user, model.NewPassword);

            tokenRow.Used = true;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Password reset successfully. Please login.";
            return RedirectToAction("Login");
        }
    }
}
