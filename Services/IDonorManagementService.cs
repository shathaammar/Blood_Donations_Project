using Blood_Donations_Project.Common;
using Blood_Donations_Project.ViewModels.Donors;

namespace Blood_Donations_Project.Services
{
    public interface IDonorManagementService
    {
        Task<List<DonorRowViewModel>> GetDonorsAsync();

        Task<List<BloodTypeOptionViewModel>> GetBloodTypeOptionsAsync();

        Task<EditDonorViewModel?> GetDonorForEditAsync(int userId);

        Task<ServiceResult> UpdateDonorAsync(EditDonorViewModel model);

        Task<ServiceResult> DeleteDonorAsync(int userId, int? currentUserId);

        Task<ServiceResult> VerifyMedicalAsync(int donorUserId, int adminUserId);
    }
}
