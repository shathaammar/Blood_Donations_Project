using Blood_Donations_Project.Common;
using Blood_Donations_Project.Models;
using Blood_Donations_Project.ViewModels.Donors;
using Blood_Donations_Project.ViewModels.Shared;
using Microsoft.EntityFrameworkCore;

namespace Blood_Donations_Project.Services
{
    public class DonorManagementService : IDonorManagementService
    {
        private readonly BloodDonationContext _context;
        private readonly ILogger<DonorManagementService> _logger;

        public DonorManagementService(BloodDonationContext context, ILogger<DonorManagementService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<List<DonorRowViewModel>> GetDonorsAsync()
        {
            return await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Donors).ThenInclude(d => d.BloodType)
                .Where(u => u.Role != null && u.Role.RoleName == AppRoles.Donor)
                .Select(u => new DonorRowViewModel
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
        }

        public async Task<List<BloodTypeOptionViewModel>> GetBloodTypeOptionsAsync()
        {
            // Same query and (unordered) sequence as the previous ViewBag.BloodTypes.
            return await _context.BloodTypes
                .Select(bt => new BloodTypeOptionViewModel
                {
                    BloodTypeId = bt.BloodTypeId,
                    TypeName = bt.TypeName
                })
                .ToListAsync();
        }

        public async Task<EditDonorViewModel?> GetDonorForEditAsync(int userId)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Donors)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null || user.Role?.RoleName != AppRoles.Donor)
                return null;

            var donor = user.Donors.FirstOrDefault();
            if (donor == null)
                return null;

            return new EditDonorViewModel
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
        }

        public async Task<ServiceResult> UpdateDonorAsync(EditDonorViewModel model)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .Include(u => u.Donors)
                .FirstOrDefaultAsync(u => u.UserId == model.UserId);

            if (user == null || user.Role?.RoleName != AppRoles.Donor)
                return ServiceResult.NotFound();

            var donor = user.Donors.FirstOrDefault();
            if (donor == null)
                return ServiceResult.NotFound();

            // Architecture-only phase: same fields as before, no new duplicate checks.
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

            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> DeleteDonorAsync(int userId, int? currentUserId)
        {
            if (currentUserId == userId)
                return ServiceResult.Fail("You cannot delete your own account.");

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
                return ServiceResult.NotFound("User not found.");

            if (user.Role?.RoleName != AppRoles.Donor)
                return ServiceResult.Fail("This action is only for Donor accounts.");

            using var tx = await _context.Database.BeginTransactionAsync();

            try
            {
                var tokens = await _context.PasswordReset.Where(x => x.UserId == userId).ToListAsync();
                if (tokens.Any()) _context.PasswordReset.RemoveRange(tokens);

                var donationReqs = await _context.DonationRequests.Where(x => x.UserId == userId).ToListAsync();
                if (donationReqs.Any()) _context.DonationRequests.RemoveRange(donationReqs);

                var donations = await _context.Donations.Where(x => x.UserId == userId).ToListAsync();
                if (donations.Any()) _context.Donations.RemoveRange(donations);

                var donor = await _context.Donors.FirstOrDefaultAsync(d => d.UserId == userId);
                if (donor != null) _context.Donors.Remove(donor);

                var userRoles = await _context.UserRoles.Where(ur => ur.UserId == userId).ToListAsync();
                if (userRoles.Any()) _context.UserRoles.RemoveRange(userRoles);

                _context.Users.Remove(user);

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return ServiceResult.Ok("Donor deleted successfully.");
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Failed to delete donor with UserId {DonorUserId}", userId);
                return ServiceResult.Fail("Failed to delete donor.");
            }
        }

        public async Task<ServiceResult> VerifyMedicalAsync(int donorUserId, int adminUserId)
        {
            var donor = await _context.Donors
                .FirstOrDefaultAsync(d => d.UserId == donorUserId);

            if (donor == null)
                return ServiceResult.NotFound("Donor profile not found");

            donor.IsMedicalVerified = true;
            donor.MedicalVerifiedBy = adminUserId;
            donor.MedicalVerifiedDate = DateOnly.FromDateTime(DateTime.Now);

            await _context.SaveChangesAsync();

            return ServiceResult.Ok("Donor medical data verified successfully.");
        }
    }
}
