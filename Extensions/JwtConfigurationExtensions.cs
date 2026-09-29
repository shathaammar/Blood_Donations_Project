using System.Text;

namespace Blood_Donations_Project.Extensions
{
    /// <summary>
    /// Reads the JWT signing key from configuration and fails fast at startup when it is
    /// missing or too short for HMAC-SHA256. The key is never stored in appsettings.json:
    /// Development uses appsettings.Development.json; other environments use the
    /// Jwt__Key environment variable (or any other configuration provider).
    /// </summary>
    public static class JwtConfigurationExtensions
    {
        public const int MinimumKeyBytes = 32; // 256 bits for HS256

        public static string GetRequiredJwtKey(this IConfiguration configuration)
        {
            var key = configuration["Jwt:Key"];

            if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < MinimumKeyBytes)
                throw new InvalidOperationException(
                    $"Jwt:Key is not configured or is shorter than {MinimumKeyBytes} bytes. " +
                    "Set it with the Jwt__Key environment variable (or in appsettings.Development.json for local development).");

            return key;
        }
    }
}
