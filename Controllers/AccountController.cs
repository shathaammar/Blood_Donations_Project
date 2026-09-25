using Blood_Donations_Project.Filters;
using Blood_Donations_Project.Services;
using Blood_Donations_Project.ViewModels.Account;
using Blood_Donations_Project.ViewModels.Profile;
using Microsoft.AspNetCore.Mvc;

namespace Blood_Donations_Project.Controllers
{
    public class AccountController : Controller
    {
        private readonly IAccountService _accountService;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IProfileService _profileService;

        public AccountController(
            IAccountService accountService,
            IJwtTokenService jwtTokenService,
            IProfileService profileService)
        {
            _accountService = accountService;
            _jwtTokenService = jwtTokenService;
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

            var user = await _accountService.ValidateCredentialsAsync(model.Email, model.Password);
            if (user == null)
            {
                ModelState.AddModelError("", "Invalid email or password.");
                return View(model);
            }

            var tokenString = _jwtTokenService.CreateToken(user, model.RememberMe);

            Response.Cookies.Append("jwt", tokenString, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Secure = Request.IsHttps,
                Expires = model.RememberMe ? DateTimeOffset.Now.AddDays(7) : DateTimeOffset.Now.AddHours(2)
            });

            HttpContext.Session.SetString("UserRole", user.RoleName ?? "");
            HttpContext.Session.SetString("UserId", user.UserId.ToString());

            return RedirectToAction("Dashboard", "Admin");
        }

        // REGISTER (DONOR ONLY)

        [HttpGet]
        public async Task<IActionResult> Register()
        {
            var model = new RegisterViewModel
            {
                BloodTypeOptions = await _accountService.GetBloodTypeOptionsAsync()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            foreach (var error in _accountService.ValidateRegistration(model))
                ModelState.AddModelError(error.FieldName ?? string.Empty, error.Message);

            if (!ModelState.IsValid)
            {
                model.BloodTypeOptions = await _accountService.GetBloodTypeOptionsAsync();
                return View(model);
            }

            var result = await _accountService.RegisterDonorAsync(model);
            if (!result.Success)
            {
                ModelState.AddModelError(result.FieldName ?? string.Empty, result.Message);
                model.BloodTypeOptions = await _accountService.GetBloodTypeOptionsAsync();
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction("Login");
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
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Email))
            {
                ModelState.AddModelError("", "Email is required.");
                return View(model);
            }

            var reset = await _accountService.CreatePasswordResetTokenAsync(model.Email);

            // Same response whether or not the email exists.
            TempData["SuccessMessage"] = "If the email exists, a reset link has been sent.";

            if (reset == null)
                return RedirectToAction("Login");

            var resetUrl = Url.Action("ResetPassword", "Account",
                new { email = reset.Email, token = reset.Token }, Request.Scheme);

            Console.WriteLine("RESET LINK => " + resetUrl);

            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult ResetPassword(string email, string token)
        {
            return View(new ResetPasswordViewModel
            {
                Email = email ?? "",
                Token = token ?? ""
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            var result = await _accountService.ResetPasswordAsync(model);
            if (!result.Success)
            {
                ModelState.AddModelError(result.FieldName ?? string.Empty, result.Message);
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction("Login");
        }
    }
}
