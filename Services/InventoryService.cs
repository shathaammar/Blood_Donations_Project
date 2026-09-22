using Blood_Donations_Project.Models;
using Microsoft.EntityFrameworkCore;

namespace Blood_Donations_Project.Services
{
    public class InventoryService : IInventoryService
    {
        private readonly BloodDonationContext _context;

        public InventoryService(BloodDonationContext context)
        {
            _context = context;
        }

        public async Task<List<BloodInventory>> GetInventoryAsync()
        {
            return await _context.BloodInventories
                .Include(i => i.BloodType)
                .OrderBy(i => i.BloodType!.TypeName)
                .ToListAsync();
        }

        public async Task<(bool success, string message)> SetUnitsAsync(int inventoryId, int units)
        {
            if (units < 0)
                return (false, "Units must be >= 0");

            var inv = await _context.BloodInventories.FirstOrDefaultAsync(x => x.Id == inventoryId);
            if (inv == null)
                return (false, "Inventory row not found");

            inv.UnitsAvailable = units;
            await _context.SaveChangesAsync();

            return (true, "Units updated successfully");
        }

        public async Task<(bool success, string message)> AddUnitsAsync(int inventoryId, int amount)
        {
            if (amount <= 0)
                return (false, "Amount must be > 0");

            var inv = await _context.BloodInventories.FirstOrDefaultAsync(x => x.Id == inventoryId);
            if (inv == null)
                return (false, "Inventory row not found");

            inv.UnitsAvailable += amount;
            await _context.SaveChangesAsync();

            return (true, $"+{amount} units added");
        }

        public async Task<(bool success, string message)> RemoveUnitsAsync(int inventoryId, int amount)
        {
            if (amount <= 0)
                return (false, "Amount must be > 0");

            var inv = await _context.BloodInventories.FirstOrDefaultAsync(x => x.Id == inventoryId);
            if (inv == null)
                return (false, "Inventory row not found");

            if (inv.UnitsAvailable < amount)
                return (false, "Not enough units to remove");

            inv.UnitsAvailable -= amount;
            await _context.SaveChangesAsync();

            return (true, $"-{amount} units removed");
        }
    }
}
