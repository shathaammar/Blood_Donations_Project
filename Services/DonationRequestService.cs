using Blood_Donations_Project.Common;
using Blood_Donations_Project.Models;
using Blood_Donations_Project.ViewModels.DonationRequests;
using Microsoft.EntityFrameworkCore;

namespace Blood_Donations_Project.Services
{
    public class DonationRequestService : IDonationRequestService
    {
        private readonly BloodDonationContext _context;
        private readonly IDonorEligibilityService _eligibility;
        private readonly IInventoryService _inventoryService;

        public DonationRequestService(
            BloodDonationContext context,
            IDonorEligibilityService eligibility,
            IInventoryService inventoryService)
        {
            _context = context;
            _eligibility = eligibility;
            _inventoryService = inventoryService;
        }

        // ---------------- Submission (donor side) ----------------

        public async Task<ServiceResult> CheckCanOpenRequestFormAsync(int donorUserId)
        {
            if (await HasPendingRequestAsync(donorUserId))
                return ServiceResult.Fail("You already have a pending donation request! Please wait for admin Approval.");

            var blockedUntil = await GetSubmissionBlockedUntilAsync(donorUserId);
            if (blockedUntil.HasValue)
                return ServiceResult.Fail($"You can request again after {blockedUntil.Value:dd/MM/yyyy}.");

            return ServiceResult.Ok();
        }

        public async Task<RequestDonationViewModel?> GetRequestFormAsync(int donorUserId)
        {
            var donor = await _context.Donors
                .Include(d => d.User)
                .Include(d => d.BloodType)
                .FirstOrDefaultAsync(d => d.UserId == donorUserId);

            if (donor == null)
                return null;

            return new RequestDonationViewModel
            {
                FullName = donor.User?.FullName,
                Email = donor.User?.Email,
                MobileNo = donor.User?.MobileNo,
                BloodTypeName = donor.BloodType?.TypeName
            };
        }

        public async Task<ServiceResult> CheckNoPendingRequestForSubmitAsync(int donorUserId)
        {
            if (await HasPendingRequestAsync(donorUserId))
                return ServiceResult.Fail("You already have a pending donation request! Please wait for admin decision.");

            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> CheckSubmissionWaitingPeriodForSubmitAsync(int donorUserId)
        {
            var blockedUntil = await GetSubmissionBlockedUntilAsync(donorUserId);
            if (blockedUntil.HasValue)
                // Existing message text preserved exactly (including the spacing typo).
                return ServiceResult.Fail($"You can reque  st again after {blockedUntil.Value:dd/MM/yyyy}.");

            return ServiceResult.Ok();
        }

        public async Task<ServiceResult> CreateRequestAsync(int donorUserId)
        {
            _context.DonationRequests.Add(new DonationRequest
            {
                UserId = donorUserId,
                RequestDate = DateOnly.FromDateTime(DateTime.Now),
                Status = "Pending"
            });

            await _context.SaveChangesAsync();

            return ServiceResult.Ok("Donation request submitted successfully!");
        }

        private Task<bool> HasPendingRequestAsync(int donorUserId)
            => _context.DonationRequests.AnyAsync(r => r.UserId == donorUserId && r.Status == "Pending");

        public async Task<DateTime?> GetSubmissionBlockedUntilAsync(int donorUserId)
        {
            // Submission rule: latest DonationRequest date, regardless of its status.
            var lastRequestedDate = await _context.DonationRequests
                .Where(r => r.UserId == donorUserId)
                .OrderByDescending(r => r.RequestDate)
                .Select(r => (DateOnly?)r.RequestDate)
                .FirstOrDefaultAsync();

            return _eligibility.GetSubmissionBlockedUntil(lastRequestedDate, DateTime.Now);
        }

        // ---------------- Decision (admin side) ----------------

        public async Task<ServiceResult> ApproveRequestAsync(int requestId, int adminUserId)
        {
            var req = await _context.DonationRequests
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (req == null)
                return ServiceResult.NotFound("Request not found");

            if (req.Status != "Pending")
                return ServiceResult.Fail("Already processed");

            var donor = await _context.Donors
                .FirstOrDefaultAsync(d => d.UserId == req.UserId);

            if (donor == null)
                return ServiceResult.Fail("Donor profile not found");

            // Approval rule: uses Donor.LastDonationDate (not the request date).
            // Null-safe: an orphaned request user is treated as "Date of birth missing." instead of throwing.
            var dateOfBirth = req.User?.DateOfBirth ?? default;
            var now = DateTime.Now;
            var eligibility = _eligibility.CheckApprovalEligibility(
                dateOfBirth, donor.IsMedicalVerified, donor.LastDonationDate, now);

            if (!eligibility.Success)
                return eligibility;

            var today = DateOnly.FromDateTime(now);

            req.Status = "Approved";
            req.ApprovedBy = adminUserId;
            req.ApprovedDate = today;

            _context.Donations.Add(new Donation
            {
                UserId = req.UserId,
                ApprovedBy = adminUserId,
                DonationDate = today,
                Status = "Approved"
            });

            // Existing behavior: no blood type or no inventory row => approval still
            // succeeds, stock is simply not increased.
            if (donor.BloodTypeId.HasValue)
                await _inventoryService.TryAddDonatedUnitAsync(donor.BloodTypeId.Value);

            donor.LastDonationDate = today;

            // Single save: request status/approval info, Donation row,
            // LastDonationDate and the inventory +1 (same scoped DbContext).
            await _context.SaveChangesAsync();

            return ServiceResult.Ok("Donor request approved.");
        }

        public async Task<ServiceResult> RejectRequestAsync(int requestId, int adminUserId)
        {
            var req = await _context.DonationRequests
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (req == null)
                return ServiceResult.NotFound("Request not found");

            if (req.Status != "Pending")
                return ServiceResult.Fail("Already processed");

            req.Status = "Rejected";
            req.ApprovedBy = adminUserId;
            req.ApprovedDate = DateOnly.FromDateTime(DateTime.Now);

            await _context.SaveChangesAsync();

            return ServiceResult.Ok("Donor request rejected.");
        }
    }
}
