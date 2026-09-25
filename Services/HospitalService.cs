using Blood_Donations_Project.Common;
using Blood_Donations_Project.Models;
using Blood_Donations_Project.ViewModels.Hospitals;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Blood_Donations_Project.Services
{
    public class HospitalService : IHospitalService
    {
        private readonly BloodDonationContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly ILogger<HospitalService> _logger;

        public HospitalService(BloodDonationContext context, IPasswordHasher<User> passwordHasher, ILogger<HospitalService> logger)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _logger = logger;
        }

        public async Task<List<HospitalRowViewModel>> GetHospitalsAsync()
        {
            return await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Role != null && u.Role.RoleName == AppRoles.Hospital)
                .OrderBy(u => u.UserId)
                .Select(u => new HospitalRowViewModel
                {
                    UserId = u.UserId,
                    FullName = u.FullName ?? "",
                    UserName = u.UserName ?? "",
                    Email = u.Email ?? "",
                    MobileNo = u.MobileNo,
                    Address = u.Address
                })
                .ToListAsync();
        }

        public async Task<ServiceResult> CreateHospitalAsync(HospitalCreateViewModel model)
        {
            // Check duplicate email
            if (await _context.Users.AnyAsync(u => u.Email == model.Email))
                return ServiceResult.Fail("Email already exists.", "Email");

            // Check duplicate username
            if (await _context.Users.AnyAsync(u => u.UserName == model.UserName))
                return ServiceResult.Fail("UserName already exists.", "UserName");

            // Get Hospital role
            var hospitalRoleId = await _context.Roles
                .Where(r => r.RoleName == AppRoles.Hospital)
                .Select(r => r.RoleId)
                .FirstOrDefaultAsync();

            if (hospitalRoleId == 0)
                return ServiceResult.Fail("Hospital role not found in Roles table.");

            // Create user entity
            var user = new User
            {
                UserName = model.UserName,
                FullName = model.FullName,
                Email = model.Email,
                MobileNo = model.MobileNo,
                Address = model.Address,
                RoleId = hospitalRoleId
            };

            // Hash password
            user.Password = _passwordHasher.HashPassword(user, model.Password);

            // Save
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return ServiceResult.Ok();
        }

        public async Task<HospitalEditViewModel?> GetHospitalForEditAsync(int id)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == id);

            if (user == null)
                return null;

            if (!string.Equals(user.Role?.RoleName, AppRoles.Hospital, StringComparison.OrdinalIgnoreCase))
                return null;

            return new HospitalEditViewModel
            {
                UserId = user.UserId,
                UserName = user.UserName ?? "",
                FullName = user.FullName ?? "",
                Email = user.Email ?? "",
                MobileNo = user.MobileNo,
                Address = user.Address
            };
        }

        public async Task<ServiceResult> UpdateHospitalAsync(HospitalEditViewModel model)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == model.UserId);

            if (user == null)
                return ServiceResult.NotFound();

            if (!string.Equals(user.Role?.RoleName, AppRoles.Hospital, StringComparison.OrdinalIgnoreCase))
                return ServiceResult.NotFound();

            // Check duplicate email (excluding current user)
            if (await _context.Users.AnyAsync(u => u.Email == model.Email && u.UserId != model.UserId))
                return ServiceResult.Fail("Email already exists.", "Email");

            // Check duplicate username (excluding current user)
            if (await _context.Users.AnyAsync(u => u.UserName == model.UserName && u.UserId != model.UserId))
                return ServiceResult.Fail("UserName already exists.", "UserName");

            // Update fields
            user.UserName = model.UserName;
            user.FullName = model.FullName;
            user.Email = model.Email;
            user.MobileNo = model.MobileNo;
            user.Address = model.Address;

            await _context.SaveChangesAsync();

            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> DeleteHospitalAsync(int id, int currentUserId)
        {
            // Check if deleting own account
            if (currentUserId == id)
                return ServiceResult.Fail("You cannot delete your own account.");

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == id);

            if (user == null)
                return ServiceResult.NotFound();

            if (!string.Equals(user.Role?.RoleName, AppRoles.Hospital, StringComparison.OrdinalIgnoreCase))
                return ServiceResult.Fail("This action is only for Hospital accounts.");

            // Transaction for cleanup
            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // Remove password reset tokens
                var tokens = await _context.PasswordReset.Where(x => x.UserId == id).ToListAsync();
                if (tokens.Any()) _context.PasswordReset.RemoveRange(tokens);

                // Remove blood requests
                var bloodReqs = await _context.BloodRequests.Where(br => br.UserId == id).ToListAsync();
                if (bloodReqs.Any()) _context.BloodRequests.RemoveRange(bloodReqs);

                // Remove donations
                var donations = await _context.Donations.Where(d => d.UserId == id).ToListAsync();
                if (donations.Any()) _context.Donations.RemoveRange(donations);

                // Remove user roles
                var userRoles = await _context.UserRoles.Where(ur => ur.UserId == id).ToListAsync();
                if (userRoles.Any()) _context.UserRoles.RemoveRange(userRoles);

                // Remove user
                _context.Users.Remove(user);

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return ServiceResult.Ok();
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Failed to delete hospital with UserId {HospitalId}", id);
                return ServiceResult.Fail("Failed to delete hospital. Please try again.");
            }
        }
    }
}
