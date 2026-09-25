using System.ComponentModel.DataAnnotations;

namespace Blood_Donations_Project.ViewModels.Hospitals
{
    /// <summary>
    /// Admin "Edit Hospital" form (Views/Admin/EditHospital.cshtml).
    /// Previously ViewModels.HospitalEdit; properties and validation are unchanged.
    /// </summary>
    public class HospitalEditViewModel
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        public string UserName { get; set; } = "";

        [Required]
        public string FullName { get; set; } = "";

        [Required, EmailAddress]
        public string Email { get; set; } = "";

        public string? MobileNo { get; set; }
        public string? Address { get; set; }
    }
}
