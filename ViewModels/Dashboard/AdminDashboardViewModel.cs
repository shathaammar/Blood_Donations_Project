namespace Blood_Donations_Project.ViewModels.Dashboard
{
    public class AdminDashboardViewModel
    {
        public int TotalDonations { get; set; }
        public int TotalBloodRequests { get; set; }
        public int TotalDonationRequests { get; set; }
        public int TotalRequests { get; set; }

        /// <summary>"BloodRequests" or "DonorRequests" (trimmed query-string value).</summary>
        public string SelectedTable { get; set; } = "BloodRequests";

        /// <summary>"All" or an exact status (trimmed query-string value).</summary>
        public string SelectedStatus { get; set; } = "All";

        public List<BloodRequestRowViewModel> BloodRequests { get; set; } = new();
        public List<DonationRequestRowViewModel> DonationRequests { get; set; } = new();
    }
}
