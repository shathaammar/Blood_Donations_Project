using Blood_Donations_Project.Common;
using Blood_Donations_Project.ViewModels.Profile;

namespace Blood_Donations_Project.Services
{
    /// <summary>
    /// Account/Profile read and update logic. The role passed in is the signed-in
    /// session role; role comparisons are case-insensitive, as before.
    /// </summary>
    public interface IProfileService
    {
        /// <summary>Profile page model, or null when the user no longer exists.</summary>
        Task<ProfileViewModel?> GetProfileAsync(int userId, string role);

        /// <summary>
        /// Fails for roles whose profile is managed by the Admin (Hospital, BloodBank).
        /// </summary>
        ServiceResult CheckCanUpdate(string role);

        /// <summary>
        /// Role-specific required fields (Donor: blood type and gender).
        /// Each failure carries its FieldName for ModelState.
        /// </summary>
        IReadOnlyList<ServiceResult> ValidateForRole(ProfileViewModel model, string role);

        /// <summary>
        /// Re-fills display-only values, role flags and options on a posted model
        /// before the form is shown again (posted field values are kept).
        /// </summary>
        Task PrepareForRedisplayAsync(ProfileViewModel model, int userId, string role);

        Task<ServiceResult> UpdateProfileAsync(int userId, string role, ProfileViewModel model);
    }
}
