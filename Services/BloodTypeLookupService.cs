using Blood_Donations_Project.Models;
using Blood_Donations_Project.ViewModels.Shared;
using Microsoft.EntityFrameworkCore;

namespace Blood_Donations_Project.Services
{
    public class BloodTypeLookupService : IBloodTypeLookupService
    {
        private readonly BloodDonationContext _context;

        public BloodTypeLookupService(BloodDonationContext context)
        {
            _context = context;
        }

        public async Task<List<BloodTypeOptionViewModel>> GetBloodTypeOptionsAsync()
        {
            // Same query and (unordered) sequence as the previous per-service copies.
            return await _context.BloodTypes
                .Select(bt => new BloodTypeOptionViewModel
                {
                    BloodTypeId = bt.BloodTypeId,
                    TypeName = bt.TypeName
                })
                .ToListAsync();
        }
    }
}
