using Blood_Donations_Project.Common;
using Blood_Donations_Project.ViewModels.Account;
using Blood_Donations_Project.ViewModels.Shared;

namespace Blood_Donations_Project.Services
{
    /// <summary>
    /// Login credential checks, donor registration and password reset.
    /// No HTTP concerns: session, cookies, TempData and links stay in AccountController.
    /// </summary>
    public interface IAccountService
    {
        /// <summary>
        /// The user when the email exists and the password verifies
        /// (SuccessRehashNeeded counts as success, without rehashing); otherwise null.
        /// </summary>
        Task<AuthenticatedUser?> ValidateCredentialsAsync(string email, string password);

        /// <summary>Blood type options for the Register dropdown (same unordered query as before).</summary>
        Task<List<BloodTypeOptionViewModel>> GetBloodTypeOptionsAsync();

        /// <summary>
        /// Register rules added on top of model validation, in the original order:
        /// date of birth / age, blood type, gender. Each failure carries its FieldName.
        /// </summary>
        IReadOnlyList<ServiceResult> ValidateRegistration(RegisterViewModel model);

        /// <summary>
        /// Duplicate checks, then creates the User and its Donor row (two saves, as before).
        /// Fails with FieldName "Email" or "UserName", or no field when the Donor role is missing.
        /// </summary>
        Task<ServiceResult> RegisterDonorAsync(RegisterViewModel model);

        /// <summary>
        /// Replaces the user's unused reset tokens with a new 30-minute token.
        /// Returns null when no user has this email.
        /// </summary>
        Task<PasswordResetToken?> CreatePasswordResetTokenAsync(string email);

        /// <summary>
        /// Checks the link and passwords, then sets the new password and marks the token used.
        /// Failures have no FieldName (model-level errors).
        /// </summary>
        Task<ServiceResult> ResetPasswordAsync(ResetPasswordViewModel model);
    }
}
