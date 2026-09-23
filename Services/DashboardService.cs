using Blood_Donations_Project.Common;
using Blood_Donations_Project.Models;
using Blood_Donations_Project.ViewModels.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace Blood_Donations_Project.Services
{
    public class DashboardService : IDashboardService
    {
        private readonly BloodDonationContext _context;
        private readonly IBloodRequestService _bloodRequestService;
        private readonly IDonorEligibilityService _eligibility;

        public DashboardService(
            BloodDonationContext context,
            IBloodRequestService bloodRequestService,
            IDonorEligibilityService eligibility)
        {
            _context = context;
            _bloodRequestService = bloodRequestService;
            _eligibility = eligibility;
        }

        public async Task<DashboardViewModel> GetDashboardAsync(int userId, string role, string? table, string? status)
        {
            var model = new DashboardViewModel { Role = role };

            if (role == AppRoles.Admin)
                model.Admin = await BuildAdminAsync(table, status);
            else if (role == AppRoles.Donor)
                model.Donor = await BuildDonorAsync(userId);
            // BloodBank users share the hospital dashboard.
            else if (role == AppRoles.Hospital || role == AppRoles.BloodBank)
                model.Hospital = await BuildHospitalAsync(userId);

            return model;
        }

        // ---------------- Admin ----------------

        private async Task<AdminDashboardViewModel> BuildAdminAsync(string? table, string? status)
        {
            var selectedTable = (table ?? "BloodRequests").Trim();
            var selectedStatus = (status ?? "All").Trim();

            var totalDonations = await _context.Donations.CountAsync();
            var totalBloodRequests = await _context.BloodRequests.CountAsync();
            var totalDonationRequests = await _context.DonationRequests.CountAsync();

            // Same query, filter and ordering as Admin/ManageBloodRequests (read-only).
            var bloodRequests = await _bloodRequestService.GetRequestsForAdminAsync(selectedStatus);

            var donationQuery = _context.DonationRequests.AsQueryable();

            if (!string.Equals(selectedStatus, "All", StringComparison.OrdinalIgnoreCase))
                donationQuery = donationQuery.Where(r => r.Status != null && r.Status.Trim() == selectedStatus);

            var donationRequests = await donationQuery
                .OrderByDescending(r => r.Id)
                .Select(ToDonationRequestRow())
                .ToListAsync();

            return new AdminDashboardViewModel
            {
                TotalDonations = totalDonations,
                TotalBloodRequests = totalBloodRequests,
                TotalDonationRequests = totalDonationRequests,
                TotalRequests = totalBloodRequests + totalDonationRequests,
                SelectedTable = selectedTable,
                SelectedStatus = selectedStatus,
                BloodRequests = bloodRequests,
                DonationRequests = donationRequests
            };
        }

        // ---------------- Donor ----------------

        private async Task<DonorDashboardViewModel> BuildDonorAsync(int userId)
        {
            var donor = await _context.Donors
                .Where(d => d.UserId == userId)
                .Select(d => new
                {
                    FullName = d.User != null ? d.User.FullName : null,
                    BloodTypeName = d.BloodType != null ? d.BloodType.TypeName : null
                })
                .FirstOrDefaultAsync();

            var lastApprovedDonationDate = await _context.Donations
                .Where(d => d.UserId == userId && d.Status == "Approved")
                .OrderByDescending(d => d.DonationDate)
                .Select(d => d.DonationDate)
                .FirstOrDefaultAsync();

            // Current submission rule, unchanged: latest DonationRequest date, any status.
            var lastRequestedDate = await _context.DonationRequests
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.RequestDate)
                .Select(r => (DateOnly?)r.RequestDate)
                .FirstOrDefaultAsync();

            var canDonate = _eligibility.GetSubmissionBlockedUntil(lastRequestedDate, DateTime.Now) == null;

            var requests = await _context.DonationRequests
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.Id)
                .Select(ToDonationRequestRow())
                .ToListAsync();

            return new DonorDashboardViewModel
            {
                HasDonorProfile = donor != null,
                DonorFullName = donor?.FullName,
                BloodTypeName = donor?.BloodTypeName,
                LastApprovedDonationDate = lastApprovedDonationDate,
                CanDonate = canDonate,
                Requests = requests,
                // Same counting rules the view used (trimmed, case-insensitive).
                TotalRequests = requests.Count,
                PendingRequests = requests.Count(x => HasStatus(x.Status, "Pending")),
                ApprovedRequests = requests.Count(x => HasStatus(x.Status, "Approved")),
                RejectedRequests = requests.Count(x => HasStatus(x.Status, "Rejected"))
            };
        }

        // ---------------- Hospital / BloodBank ----------------

        private async Task<HospitalDashboardViewModel> BuildHospitalAsync(int userId)
        {
            var user = await _context.Users
                .Where(u => u.UserId == userId)
                .Select(u => new { u.FullName, u.Email })
                .FirstOrDefaultAsync();

            var requests = await (
                from br in _context.BloodRequests
                where br.UserId == userId
                join bt in _context.BloodTypes on br.BloodTypeId equals bt.BloodTypeId into bts
                from bt in bts.DefaultIfEmpty()
                orderby br.Id descending
                select new BloodRequestRowViewModel
                {
                    Id = br.Id,
                    UserId = br.UserId,
                    BloodTypeId = br.BloodTypeId,
                    // Same output as the old view's GetBloodTypeName(): "-" / type name / id as text.
                    BloodTypeName = br.BloodTypeId == null
                        ? "-"
                        : (bt != null ? bt.TypeName : br.BloodTypeId.ToString()),
                    RequestDate = br.RequestDate,
                    Quantity = br.Quantity,
                    Status = br.Status
                }).ToListAsync();

            return new HospitalDashboardViewModel
            {
                FullName = user?.FullName,
                Email = user?.Email,
                Requests = requests
            };
        }

        // ---------------- Helpers ----------------

        private static System.Linq.Expressions.Expression<Func<DonationRequest, DonationRequestRowViewModel>> ToDonationRequestRow()
            => r => new DonationRequestRowViewModel
            {
                Id = r.Id,
                UserId = r.UserId,
                DonorFullName = r.User != null ? r.User.FullName : null,
                RequestDate = r.RequestDate,
                Status = r.Status,
                ApprovedDate = r.ApprovedDate,
                ApprovedByUserName = r.ApprovedByNavigation != null ? r.ApprovedByNavigation.UserName : null,
                // Old view: r.User?.Donors?.FirstOrDefault()?.IsMedicalVerified == true
                IsDonorMedicalVerified = r.User != null
                    && r.User.Donors.Select(d => d.IsMedicalVerified).FirstOrDefault()
            };

        private static bool HasStatus(string? status, string expected)
            => (status ?? "").Trim().Equals(expected, StringComparison.OrdinalIgnoreCase);
    }
}
