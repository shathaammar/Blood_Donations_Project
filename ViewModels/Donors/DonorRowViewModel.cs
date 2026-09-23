namespace Blood_Donations_Project.ViewModels.Donors
{
    /// <summary>
    /// One row of the Admin/Users donor table (no entity or password data).
    /// </summary>
    public class DonorRowViewModel
    {
        public int UserId { get; set; }
        public string? FullName { get; set; }
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? MobileNo { get; set; }
        public string? Address { get; set; }
        public string? BloodTypeName { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string? HealthStatus { get; set; }
        public string? Gender { get; set; }
    }
}
