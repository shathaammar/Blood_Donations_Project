using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Blood_Donations_Project.ViewModels.Donors
{
    /// <summary>
    /// Admin edit form for a donor (Views/Admin/EditUser.cshtml).
    /// Field names match the previous EditUser ViewModel so the posted form is unchanged.
    /// </summary>
    public class EditDonorViewModel
    {
        public int UserId { get; set; }

        [Required]
        public string? FullName { get; set; }

        [Required]
        public string? UserName { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        public string? MobileNo { get; set; }

        public string? Address { get; set; }

        [DataType(DataType.Date)]
        public DateTime DateOfBirth { get; set; }

        public string? HealthStatus { get; set; }

        public int? BloodTypeId { get; set; }

        public string? Gender { get; set; }

        /// <summary>
        /// Dropdown options. Filled by the controller on GET and on invalid POST;
        /// never bound from or validated against the posted form.
        /// </summary>
        [BindNever]
        [ValidateNever]
        public List<BloodTypeOptionViewModel> BloodTypeOptions { get; set; } = new();
    }
}
