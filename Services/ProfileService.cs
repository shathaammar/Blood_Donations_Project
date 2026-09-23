using Blood_Donations_Project.Common;
using Blood_Donations_Project.Models;
using Blood_Donations_Project.ViewModels.Profile;
using Blood_Donations_Project.ViewModels.Shared;
using Microsoft.EntityFrameworkCore;

namespace Blood_Donations_Project.Services
{
    public class ProfileService : IProfileService
    {
        private readonly BloodDonationContext _context;

        public ProfileService(BloodDonationContext context)
        {
            _context = context;
        }

        // ---------------- Read ----------------

        public async Task<ProfileViewModel?> GetProfileAsync(int userId, string role)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (user == null)
                return null;

            var model = new ProfileViewModel
            {
                UserId = user.UserId,
                FullName = user.FullName,
                Email = user.Email,
                MobileNo = user.MobileNo,
                Address = user.Address,
                Gender = user.Gender
            };

            if (IsDonor(role))
            {
                var donor = await _context.Donors
                    .Include(d => d.BloodType)
                    .FirstOrDefaultAsync(d => d.UserId == userId);

                model.BloodTypeId = donor?.BloodTypeId;
                model.BloodTypeName = donor?.BloodType?.TypeName;
                model.HealthStatus = donor?.HealthStatus;
            }

            await FillDisplayDataAsync(model, user, role);
            return model;
        }

        public async Task PrepareForRedisplayAsync(ProfileViewModel model, int userId, string role)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.UserId == userId);

            await FillDisplayDataAsync(model, user, role);
        }

        // ---------------- Update ----------------

        public ServiceResult CheckCanUpdate(string role)
        {
            if (IsManagedByAdmin(role))
                return ServiceResult.Fail("Your profile data is managed by the Admin.");

            return ServiceResult.Ok();
        }

        public IReadOnlyList<ServiceResult> ValidateForRole(ProfileViewModel model, string role)
        {
            var errors = new List<ServiceResult>();

            if (IsDonor(role))
            {
                if (!model.BloodTypeId.HasValue)
                    errors.Add(ServiceResult.Fail("Blood type is required.", nameof(ProfileViewModel.BloodTypeId)));

                if (string.IsNullOrWhiteSpace(model.Gender))
                    errors.Add(ServiceResult.Fail("Gender is required.", nameof(ProfileViewModel.Gender)));
            }

            return errors;
        }

        public async Task<ServiceResult> UpdateProfileAsync(int userId, string role, ProfileViewModel model)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
            if (user == null)
                return ServiceResult.NotFound();

            var isDonor = IsDonor(role);
            var isAdmin = IsAdmin(role);

            // Existing behavior: these four fields are updated for every role that
            // reaches this point (Hospital/BloodBank are stopped by CheckCanUpdate).
            user.FullName = model.FullName;
            user.Email = model.Email;
            user.MobileNo = model.MobileNo;
            user.Address = model.Address;

            if (isDonor || isAdmin)
            {
                user.Gender = model.Gender;
            }

            if (isDonor)
            {
                var donor = await _context.Donors.FirstOrDefaultAsync(d => d.UserId == userId);
                if (donor != null)
                {
                    // ValidateForRole guarantees a value for donors.
                    donor.BloodTypeId = model.BloodTypeId!.Value;
                    donor.HealthStatus = model.HealthStatus;
                }
            }

            await _context.SaveChangesAsync();

            return ServiceResult.Ok("Profile updated successfully.");
        }

        // ---------------- Helpers ----------------

        private async Task FillDisplayDataAsync(ProfileViewModel model, User? user, string role)
        {
            var isDonor = IsDonor(role);
            var isAdmin = IsAdmin(role);

            model.RoleName = role;
            model.IsDonor = isDonor;
            model.IsAdmin = isAdmin;
            model.IsManagedByAdmin = IsManagedByAdmin(role);
            model.CanEdit = isDonor || isAdmin;

            model.UserName = user?.UserName;
            model.DateOfBirth = isDonor ? user?.DateOfBirth : null;

            model.BloodTypeOptions = isDonor
                ? await GetBloodTypeOptionsAsync()
                : new List<BloodTypeOptionViewModel>();
        }

        private async Task<List<BloodTypeOptionViewModel>> GetBloodTypeOptionsAsync()
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

        private static bool IsDonor(string role)
            => string.Equals(role, AppRoles.Donor, StringComparison.OrdinalIgnoreCase);

        private static bool IsAdmin(string role)
            => string.Equals(role, AppRoles.Admin, StringComparison.OrdinalIgnoreCase);

        private static bool IsManagedByAdmin(string role)
            => string.Equals(role, AppRoles.Hospital, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, AppRoles.BloodBank, StringComparison.OrdinalIgnoreCase);
    }
}
