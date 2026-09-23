using System.ComponentModel.DataAnnotations;
using Blood_Donations_Project.ViewModels.Shared;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Blood_Donations_Project.ViewModels.Profile
{
    /// <summary>
    /// Account/Profile page model (previously ViewModels.Profile).
    /// Posted form fields keep their names; display-only values and role flags
    /// are prepared by ProfileService and never bound from the request.
    /// </summary>
    public class ProfileViewModel
    {
        // ---------- Posted form fields ----------

        public int UserId { get; set; }

        [Required]
        public string? FullName { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        public string? MobileNo { get; set; }

        public string? Address { get; set; }

        public string? Gender { get; set; }

        public int? BloodTypeId { get; set; }

        public string? HealthStatus { get; set; }

        // ---------- Display-only (read-only inputs; reloaded, never bound) ----------

        [BindNever, ValidateNever]
        public string? UserName { get; set; }

        [BindNever, ValidateNever]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [BindNever, ValidateNever]
        public string? BloodTypeName { get; set; }

        /// <summary>The signed-in role (session value), shown in the read-only Role field.</summary>
        [BindNever, ValidateNever]
        public string? RoleName { get; set; }

        // ---------- Role decisions prepared by ProfileService ----------

        [BindNever, ValidateNever]
        public bool IsDonor { get; set; }

        [BindNever, ValidateNever]
        public bool IsAdmin { get; set; }

        /// <summary>Hospital or BloodBank: profile is read-only and managed by the Admin.</summary>
        [BindNever, ValidateNever]
        public bool IsManagedByAdmin { get; set; }

        [BindNever, ValidateNever]
        public bool CanEdit { get; set; }

        [BindNever, ValidateNever]
        public List<BloodTypeOptionViewModel> BloodTypeOptions { get; set; } = new();
    }
}
