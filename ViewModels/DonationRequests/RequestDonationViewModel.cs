namespace Blood_Donations_Project.ViewModels.DonationRequests
{
    /// <summary>
    /// Donor information shown on Views/Donor/RequestDonation.cshtml.
    /// The form itself posts no fields (only the anti-forgery token).
    /// Values stay nullable so the view keeps its "-" fallbacks.
    /// </summary>
    public class RequestDonationViewModel
    {
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? MobileNo { get; set; }
        public string? BloodTypeName { get; set; }
    }
}
