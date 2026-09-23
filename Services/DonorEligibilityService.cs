using Blood_Donations_Project.Common;

namespace Blood_Donations_Project.Services
{
    public class DonorEligibilityService : IDonorEligibilityService
    {
        public const int MinimumAge = 18;
        public const int WaitingPeriodMonths = 3;

        public int CalculateAge(DateTime dateOfBirth, DateTime today)
        {
            var age = today.Year - dateOfBirth.Year;
            if (dateOfBirth.Date > today.AddYears(-age)) age--;
            return age;
        }

        public bool IsOldEnough(DateTime dateOfBirth, DateTime today)
            => CalculateAge(dateOfBirth, today) >= MinimumAge;

        public DateTime GetNextAllowedDate(DateOnly lastDate)
            => lastDate.ToDateTime(TimeOnly.MinValue).AddMonths(WaitingPeriodMonths);

        public DateTime? GetSubmissionBlockedUntil(DateOnly? lastRequestDate, DateTime now)
        {
            if (!lastRequestDate.HasValue)
                return null;

            var nextAllowed = GetNextAllowedDate(lastRequestDate.Value);
            return now < nextAllowed ? nextAllowed : null;
        }

        public ServiceResult CheckApprovalEligibility(DateTime dateOfBirth, bool isMedicalVerified, DateOnly? lastDonationDate, DateTime now)
        {
            if (dateOfBirth == default)
                return ServiceResult.Fail("Date of birth missing.");

            if (!IsOldEnough(dateOfBirth, now.Date))
                return ServiceResult.Fail("Donor must be 18+.");

            if (!isMedicalVerified)
                return ServiceResult.Fail("Medical data not verified.");

            if (lastDonationDate.HasValue)
            {
                var nextAllowed = GetNextAllowedDate(lastDonationDate.Value);

                if (now < nextAllowed)
                    return ServiceResult.Fail($"Can donate again after {nextAllowed:dd/MM/yyyy}");
            }

            return ServiceResult.Ok();
        }
    }
}
