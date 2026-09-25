namespace Blood_Donations_Project.Services
{
    /// <summary>
    /// A newly created password-reset token, used only by the controller to build
    /// the reset link. Email is the value stored for the user (not the typed value).
    /// </summary>
    public sealed record PasswordResetToken(string? Email, string Token);
}
