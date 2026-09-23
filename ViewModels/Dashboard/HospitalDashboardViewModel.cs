namespace Blood_Donations_Project.ViewModels.Dashboard
{
    /// <summary>Dashboard for the Hospital and BloodBank roles.</summary>
    public class HospitalDashboardViewModel
    {
        // User information (not currently displayed by the view).
        public string? FullName { get; set; }
        public string? Email { get; set; }

        /// <summary>
        /// The user's own blood requests. BloodTypeName is already resolved:
        /// "-" when no blood type, the id as text when the type no longer exists.
        /// </summary>
        public List<BloodRequestRowViewModel> Requests { get; set; } = new();
    }
}
