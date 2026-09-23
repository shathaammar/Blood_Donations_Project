using Blood_Donations_Project.Common;

namespace Blood_Donations_Project.Services
{
    /// <summary>
    /// Pure donor-eligibility rules (no database access).
    /// "now" is passed in so the rules are deterministic and easy to unit test.
    /// </summary>
    public interface IDonorEligibilityService
    {
        int CalculateAge(DateTime dateOfBirth, DateTime today);

        bool IsOldEnough(DateTime dateOfBirth, DateTime today);

        /// <summary>
        /// The waiting period after a given date (currently 3 months).
        /// </summary>
        DateTime GetNextAllowedDate(DateOnly lastDate);

        /// <summary>
        /// Submission rule: based on the most recent DonationRequest date (any status).
        /// Returns the date the donor may request again, or null when a new request is allowed now.
        /// </summary>
        DateTime? GetSubmissionBlockedUntil(DateOnly? lastRequestDate, DateTime now);

        /// <summary>
        /// Approval rule: date of birth present, 18+, medical data verified,
        /// and the waiting period after Donor.LastDonationDate has passed.
        /// </summary>
        ServiceResult CheckApprovalEligibility(DateTime dateOfBirth, bool isMedicalVerified, DateOnly? lastDonationDate, DateTime now);
    }
}
