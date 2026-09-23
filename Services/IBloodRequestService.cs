using Blood_Donations_Project.Common;
using Blood_Donations_Project.ViewModels;
using Blood_Donations_Project.ViewModels.BloodRequests;
using Blood_Donations_Project.ViewModels.Shared;

namespace Blood_Donations_Project.Services
{
    public interface IBloodRequestService
    {
        Task<List<BloodTypeOptionViewModel>> GetBloodTypeOptionsAsync();

        Task<ServiceResult> CreateRequestAsync(int hospitalUserId, RequestBloodViewModel model);

        /// <summary>
        /// Admin list of blood requests. "All" (any case) returns every request;
        /// any other value filters by exact status.
        /// </summary>
        Task<List<BloodRequestRowTable>> GetRequestsForAdminAsync(string status);

        Task<ServiceResult> ApproveRequestAsync(int requestId);

        Task<ServiceResult> RejectRequestAsync(int requestId);
    }
}
