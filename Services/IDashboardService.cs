using Blood_Donations_Project.ViewModels.Dashboard;

namespace Blood_Donations_Project.Services
{
    /// <summary>
    /// Read-only aggregation for the shared /Admin/Dashboard page. Performs no database writes.
    /// </summary>
    public interface IDashboardService
    {
        /// <summary>
        /// Builds the dashboard for the given role: Admin, Donor, Hospital or BloodBank
        /// (exact, case-sensitive match as before). Any other role gets no child model.
        /// </summary>
        Task<DashboardViewModel> GetDashboardAsync(int userId, string role, string? table, string? status);
    }
}
