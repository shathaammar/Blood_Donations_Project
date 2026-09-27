using Blood_Donations_Project.Common;

namespace Blood_Donations_Project.Extensions
{
    /// <summary>
    /// Typed access to the signed-in user stored in the session.
    /// Same parsing as the previous inline code: int.TryParse on the stored UserId
    /// (null when missing or not a number) and the raw UserRole string.
    /// </summary>
    public static class CurrentUserSessionExtensions
    {
        /// <summary>The signed-in user's id, or null when missing or not a valid integer.</summary>
        public static int? GetUserId(this ISession session)
            => int.TryParse(session.GetString(SessionKeys.UserId), out var userId) ? userId : null;

        /// <summary>The signed-in user's role name exactly as stored (may be null or empty).</summary>
        public static string? GetUserRole(this ISession session)
            => session.GetString(SessionKeys.UserRole);

        /// <summary>Stores the signed-in user (role first, then id, as before).</summary>
        public static void SetCurrentUser(this ISession session, int userId, string role)
        {
            session.SetString(SessionKeys.UserRole, role);
            session.SetString(SessionKeys.UserId, userId.ToString());
        }
    }
}
