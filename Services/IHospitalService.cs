using Blood_Donations_Project.ViewModels;
using Blood_Donations_Project.ViewModels.Hospitals;

namespace Blood_Donations_Project.Services
{
    public interface IHospitalService
    {
        Task<List<HospitalRowViewModel>> GetHospitalsAsync();
        Task<HospitalServiceResult> CreateHospitalAsync(HospitalCreate model);
        Task<HospitalEdit?> GetHospitalForEditAsync(int id);
        Task<HospitalServiceResult> UpdateHospitalAsync(HospitalEdit model);
        Task<HospitalServiceResult> DeleteHospitalAsync(int id, int currentUserId);
    }
}
