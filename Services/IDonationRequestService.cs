using Blood_Donations_Project.Common;
using Blood_Donations_Project.ViewModels.DonationRequests;

namespace Blood_Donations_Project.Services
{
    public interface IDonationRequestService
    {
        /// <summary>
        /// GET Donor/RequestDonation checks: no Pending request, and the submission
        /// waiting period (based on the latest DonationRequest date) has passed.
        /// </summary>
        Task<ServiceResult> CheckCanOpenRequestFormAsync(int donorUserId);

        /// <summary>
        /// Donor details for the request page, or null when the donor profile does not exist.
        /// </summary>
        Task<RequestDonationViewModel?> GetRequestFormAsync(int donorUserId);

        /// <summary>
        /// POST check 1: fails when the donor already has a Pending request.
        /// </summary>
        Task<ServiceResult> CheckNoPendingRequestForSubmitAsync(int donorUserId);

        /// <summary>
        /// POST check 2: fails while the submission waiting period is running.
        /// Kept separate from check 1 because the two failures currently redirect to different pages.
        /// </summary>
        Task<ServiceResult> CheckSubmissionWaitingPeriodForSubmitAsync(int donorUserId);

        Task<ServiceResult> CreateRequestAsync(int donorUserId);

        Task<ServiceResult> ApproveRequestAsync(int requestId, int adminUserId);

        Task<ServiceResult> RejectRequestAsync(int requestId, int adminUserId);
    }
}
