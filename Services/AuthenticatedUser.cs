namespace Blood_Donations_Project.Services
{
    /// <summary>
    /// The user whose credentials were just verified. Holds only what the
    /// controller needs for the session and the JWT (never the password hash).
    /// Values are passed through exactly as stored (null stays null).
    /// </summary>
    public sealed record AuthenticatedUser(int UserId, string? Email, string? RoleName);
}
