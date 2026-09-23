namespace Blood_Donations_Project.ViewModels.Dashboard
{
    public class DonorDashboardViewModel
    {
        // Donor information (loaded as before; not currently displayed by the view).
        public bool HasDonorProfile { get; set; }
        public string? DonorFullName { get; set; }
        public string? BloodTypeName { get; set; }
        public DateOnly? LastApprovedDonationDate { get; set; }

        /// <summary>Current submission waiting-period rule (latest request date, any status).</summary>
        public bool CanDonate { get; set; }

        public List<DonationRequestRowViewModel> Requests { get; set; } = new();

        public int TotalRequests { get; set; }
        public int PendingRequests { get; set; }
        public int ApprovedRequests { get; set; }
        public int RejectedRequests { get; set; }
    }
}
