namespace Blood_Donations_Project.Services
{
    /// <summary>
    /// Creates the JWT written to the "jwt" cookie at login.
    /// Reads Jwt:Key, Jwt:Issuer and Jwt:Audience from configuration.
    /// </summary>
    public interface IJwtTokenService
    {
        /// <summary>
        /// Signed token for the user; expires in 7 days with Remember Me, otherwise 2 hours.
        /// Throws when Jwt:Key is missing.
        /// </summary>
        string CreateToken(AuthenticatedUser user, bool rememberMe);
    }
}
