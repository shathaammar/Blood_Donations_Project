using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Blood_Donations_Project.Filters
{
    /// <summary>
    /// Session-based authorization for the current (pre-cookie-auth) login system.
    ///
    /// - No roles: any logged-in user (session has a valid UserId and a non-empty UserRole).
    /// - With roles: the session role must match one of them exactly.
    /// - Anonymous: page requests are redirected to Account/Login; Ajax actions get 401 JSON.
    /// - Wrong role: page requests are redirected to Admin/Dashboard (the shared, role-aware
    ///   dashboard every logged-in role can open); Ajax actions get 403 JSON.
    ///
    /// When the attribute is on both the controller and the action, only the most specific
    /// one (the action's) is applied, so an action can loosen or tighten the controller rule.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class SessionAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        private readonly string[] _roles;

        public SessionAuthorizeAttribute(params string[] roles)
        {
            _roles = roles ?? Array.Empty<string>();
        }

        /// <summary>
        /// Set to true on actions called with fetch/Ajax that return JSON,
        /// so they get a status code + JSON body instead of an HTML redirect.
        /// </summary>
        public bool Ajax { get; set; }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            if (!IsMostSpecific(context))
                return;

            var session = context.HttpContext.Session;
            var userIdStr = session.GetString("UserId");
            // Login stores the role name exactly as it is in the Roles table.
            var role = session.GetString("UserRole");

            if (!int.TryParse(userIdStr, out _) || string.IsNullOrWhiteSpace(role))
            {
                context.Result = Ajax
                    ? new JsonResult(new { success = false, message = "Not authenticated. Please login again." })
                    { StatusCode = StatusCodes.Status401Unauthorized }
                    : new RedirectToActionResult("Login", "Account", null);
                return;
            }

            if (_roles.Length > 0 && !_roles.Contains(role, StringComparer.Ordinal))
            {
                context.Result = Ajax
                    ? new JsonResult(new { success = false, message = "Not authorized" })
                    { StatusCode = StatusCodes.Status403Forbidden }
                    : new RedirectToActionResult("Dashboard", "Admin", null);
            }
        }

        // Action-level attribute wins over controller-level attribute.
        private bool IsMostSpecific(AuthorizationFilterContext context)
        {
            var closest = context.ActionDescriptor.FilterDescriptors
                .Where(d => d.Filter is SessionAuthorizeAttribute)
                .OrderByDescending(d => d.Scope)
                .FirstOrDefault();

            return closest == null || ReferenceEquals(closest.Filter, this);
        }
    }
}
