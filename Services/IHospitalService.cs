using Blood_Donations_Project.Common;
using Blood_Donations_Project.ViewModels;
using Blood_Donations_Project.ViewModels.Hospitals;

namespace Blood_Donations_Project.Services
{
    public interface IHospitalService
    {
        Task<List<HospitalRowViewModel>> GetHospitalsAsync();
        Task<ServiceResult> CreateHospitalAsync(HospitalCreate model);
        Task<HospitalEdit?> GetHospitalForEditAsync(int id);
        Task<ServiceResult> UpdateHospitalAsync(HospitalEdit model);
        Task<ServiceResult> DeleteHospitalAsync(int id, int currentUserId);
    }
}
