using Blood_Donations_Project.Common;
using Blood_Donations_Project.Models;
using Blood_Donations_Project.ViewModels.BloodRequests;
using Blood_Donations_Project.ViewModels.Dashboard;
using Blood_Donations_Project.ViewModels.Shared;
using Microsoft.EntityFrameworkCore;

namespace Blood_Donations_Project.Services
{
    public class BloodRequestService : IBloodRequestService
    {
        private readonly BloodDonationContext _context;
        private readonly IInventoryService _inventoryService;
        private readonly IBloodTypeLookupService _bloodTypeLookup;

        public BloodRequestService(
            BloodDonationContext context,
            IInventoryService inventoryService,
            IBloodTypeLookupService bloodTypeLookup)
        {
            _context = context;
            _inventoryService = inventoryService;
            _bloodTypeLookup = bloodTypeLookup;
        }

        public Task<List<BloodTypeOptionViewModel>> GetBloodTypeOptionsAsync()
            => _bloodTypeLookup.GetBloodTypeOptionsAsync();

        public async Task<ServiceResult> CreateRequestAsync(int hospitalUserId, RequestBloodViewModel model)
        {
            var bloodRequest = new BloodRequest
            {
                UserId = hospitalUserId,
                BloodTypeId = model.BloodTypeId,
                Quantity = model.Quantity,
                RequestDate = DateOnly.FromDateTime(DateTime.Now),
                Status = RequestStatuses.Pending
            };

            _context.BloodRequests.Add(bloodRequest);
            await _context.SaveChangesAsync();

            return ServiceResult.Ok();
        }

        public async Task<List<BloodRequestRowViewModel>> GetRequestsForAdminAsync(string status)
        {
            var query =
                from br in _context.BloodRequests
                join u in _context.Users on br.UserId equals u.UserId into users
                from u in users.DefaultIfEmpty()
                join bt in _context.BloodTypes on br.BloodTypeId equals bt.BloodTypeId into bts
                from bt in bts.DefaultIfEmpty()
                select new BloodRequestRowViewModel
                {
                    Id = br.Id,
                    UserId = br.UserId,
                    UserName = u != null ? u.FullName : "-",
                    BloodTypeId = br.BloodTypeId,
                    BloodTypeName = bt != null ? bt.TypeName : "-",
                    RequestDate = br.RequestDate,
                    Quantity = br.Quantity,
                    Status = br.Status
                };

            if (!string.Equals(status, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(x => x.Status != null && x.Status.Trim() == status);
            }

            return await query.OrderByDescending(x => x.Id).ToListAsync();
        }

        public async Task<ServiceResult> ApproveRequestAsync(int requestId)
        {
            var req = await _context.BloodRequests.FirstOrDefaultAsync(br => br.Id == requestId);
            if (req == null)
                return ServiceResult.NotFound("Request not found");

            if (!string.Equals(req.Status, RequestStatuses.Pending, StringComparison.OrdinalIgnoreCase))
                return ServiceResult.Fail("Already processed");

            if (req.BloodTypeId == null)
                return ServiceResult.Fail("Invalid blood type");

            var units = req.Quantity ?? 0;
            if (units <= 0)
                return ServiceResult.Fail("Invalid quantity");

            // Checks and subtracts stock on the tracked inventory row (no save yet).
            // On failure nothing has been modified, so nothing is saved.
            var stock = await _inventoryService.TryDeductUnitsAsync(req.BloodTypeId.Value, units);
            if (!stock.Success)
                return stock;

            req.Status = RequestStatuses.Approved;

            // One save for both the inventory deduction and the status change
            // (InventoryService shares this request's scoped DbContext).
            await _context.SaveChangesAsync();

            return ServiceResult.Ok("Request approved successfully");
        }

        public async Task<ServiceResult> RejectRequestAsync(int requestId)
        {
            var req = await _context.BloodRequests.FirstOrDefaultAsync(br => br.Id == requestId);
            if (req == null)
                return ServiceResult.NotFound("Request not found");

            if (!string.Equals(req.Status, RequestStatuses.Pending, StringComparison.OrdinalIgnoreCase))
                return ServiceResult.Fail("Already processed");

            req.Status = RequestStatuses.Rejected;

            await _context.SaveChangesAsync();

            return ServiceResult.Ok("Request rejected");
        }
    }
}
