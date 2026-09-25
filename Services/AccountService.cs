using Blood_Donations_Project.Common;
using Blood_Donations_Project.Models;
using Blood_Donations_Project.ViewModels.Account;
using Blood_Donations_Project.ViewModels.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Blood_Donations_Project.Services
{
    public class AccountService : IAccountService
    {
        private readonly BloodDonationContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IBloodTypeLookupService _bloodTypeLookup;
        private readonly IDonorEligibilityService _eligibility;

        public AccountService(
            BloodDonationContext context,
            IPasswordHasher<User> passwordHasher,
            IBloodTypeLookupService bloodTypeLookup,
            IDonorEligibilityService eligibility)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _bloodTypeLookup = bloodTypeLookup;
            _eligibility = eligibility;
        }

        // ---------------- Login ----------------

        public async Task<AuthenticatedUser?> ValidateCredentialsAsync(string email, string password)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
                return null;

            var result = _passwordHasher.VerifyHashedPassword(user, user.Password, password);

            // Existing behavior: only Failed is rejected; SuccessRehashNeeded is a
            // successful login and the stored hash is not upgraded.
            if (result == PasswordVerificationResult.Failed)
                return null;

            return new AuthenticatedUser(user.UserId, user.Email, user.Role?.RoleName);
        }

        // ---------------- Register ----------------

        public Task<List<BloodTypeOptionViewModel>> GetBloodTypeOptionsAsync()
            => _bloodTypeLookup.GetBloodTypeOptionsAsync();

        public IReadOnlyList<ServiceResult> ValidateRegistration(RegisterViewModel model)
        {
            var errors = new List<ServiceResult>();

            if (model.DateOfBirth == null)
                errors.Add(ServiceResult.Fail("Date of birth is required.", nameof(RegisterViewModel.DateOfBirth)));
            else
            {
                // Same rule as before: age from DateTime.Today, minimum 18.
                if (!_eligibility.IsOldEnough(model.DateOfBirth.Value, DateTime.Today))
                    errors.Add(ServiceResult.Fail("You must be 18 or older to register as a donor.", nameof(RegisterViewModel.DateOfBirth)));
            }

            if (!model.BloodTypeId.HasValue)
                errors.Add(ServiceResult.Fail("Blood type is required.", nameof(RegisterViewModel.BloodTypeId)));

            if (string.IsNullOrWhiteSpace(model.Gender))
                errors.Add(ServiceResult.Fail("Gender is required.", nameof(RegisterViewModel.Gender)));

            return errors;
        }

        public async Task<ServiceResult> RegisterDonorAsync(RegisterViewModel model)
        {
            if (await _context.Users.AnyAsync(u => u.Email == model.Email))
                return ServiceResult.Fail("Email already exists.", nameof(RegisterViewModel.Email));

            if (await _context.Users.AnyAsync(u => u.UserName == model.UserName))
                return ServiceResult.Fail("Username already exists.", nameof(RegisterViewModel.UserName));

            var donorRoleId = await _context.Roles
                .Where(r => r.RoleName == AppRoles.Donor)
                .Select(r => r.RoleId)
                .FirstOrDefaultAsync();

            if (donorRoleId == 0)
                return ServiceResult.Fail("Donor role not configured.");

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

            user.Password = _passwordHasher.HashPassword(user, model.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Existing behavior: the Donor row is saved separately, after the User
            // (no transaction), because it needs the generated UserId.
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

            return ServiceResult.Ok("Registration successful! Please login.");
        }

        // ---------------- Password reset ----------------

        public async Task<PasswordResetToken?> CreatePasswordResetTokenAsync(string email)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
                return null;

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

            return new PasswordResetToken(user.Email, token);
        }

        public async Task<ServiceResult> ResetPasswordAsync(ResetPasswordViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Token))
                return ServiceResult.Fail("Invalid reset link.");

            if (string.IsNullOrWhiteSpace(model.NewPassword))
                return ServiceResult.Fail("New password is required.");

            if (model.NewPassword != model.ConfirmPassword)
                return ServiceResult.Fail("Passwords do not match.");

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
            if (user == null)
                return ServiceResult.Fail("Invalid reset link.");

            var tokenRow = await _context.PasswordReset
                .FirstOrDefaultAsync(t =>
                    t.UserId == user.UserId &&
                    t.Token == model.Token &&
                    !t.Used);

            if (tokenRow == null || tokenRow.ExpiresAt < DateTime.UtcNow)
                return ServiceResult.Fail("Reset link expired or invalid.");

            user.Password = _passwordHasher.HashPassword(user, model.NewPassword);

            tokenRow.Used = true;

            // One save for the new password and the used token.
            await _context.SaveChangesAsync();

            return ServiceResult.Ok("Password reset successfully. Please login.");
        }
    }
}
