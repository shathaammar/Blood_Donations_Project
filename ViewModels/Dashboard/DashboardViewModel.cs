namespace Blood_Donations_Project.ViewModels.Dashboard
{
    /// <summary>
    /// Model for the shared /Admin/Dashboard page. Exactly one child is set,
    /// chosen by role; none is set for an unknown role.
    /// </summary>
    public class DashboardViewModel
    {
        public string Role { get; set; } = "";

        public AdminDashboardViewModel? Admin { get; set; }
        public DonorDashboardViewModel? Donor { get; set; }
        public HospitalDashboardViewModel? Hospital { get; set; }
    }
}
