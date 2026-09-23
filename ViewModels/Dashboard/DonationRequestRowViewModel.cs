namespace Blood_Donations_Project.ViewModels.Dashboard
{
    /// <summary>
    /// Flat donation-request row for the admin and donor dashboards.
    /// Replaces the navigation properties the views used to traverse
    /// (User.FullName, ApprovedByNavigation.UserName, User.Donors[0].IsMedicalVerified).
    /// </summary>
    public class DonationRequestRowViewModel
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string? DonorFullName { get; set; }
        public DateOnly RequestDate { get; set; }
        public string? Status { get; set; }
        public DateOnly? ApprovedDate { get; set; }
        public string? ApprovedByUserName { get; set; }
        public bool IsDonorMedicalVerified { get; set; }
    }
}
