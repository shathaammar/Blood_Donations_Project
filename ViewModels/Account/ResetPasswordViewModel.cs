namespace Blood_Donations_Project.ViewModels.Account
{
    public class ResetPasswordViewModel
    {
        public string Email { get; set; } = "";
        public string Token { get; set; } = "";

        public string NewPassword { get; set; } = "";
        public string ConfirmPassword { get; set; } = "";
    }
}
