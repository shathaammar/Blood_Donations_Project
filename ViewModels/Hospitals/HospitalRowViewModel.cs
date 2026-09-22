namespace Blood_Donations_Project.ViewModels.Hospitals
{
    public class HospitalRowViewModel
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = "";
        public string UserName { get; set; } = "";
        public string Email { get; set; } = "";
        public string? MobileNo { get; set; }
        public string? Address { get; set; }
    }
}
