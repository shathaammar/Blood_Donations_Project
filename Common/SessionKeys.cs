namespace Blood_Donations_Project.Common
{
    /// <summary>
    /// Session keys written at login and read by controllers, the session
    /// authorization filter and the layout. Values are unchanged.
    /// </summary>
    public static class SessionKeys
    {
        public const string UserId = "UserId";
        public const string UserRole = "UserRole";
    }
}
