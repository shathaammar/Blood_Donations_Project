using Blood_Donations_Project.Common;
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

        public async Task<ServiceResult> SetUnitsAsync(int inventoryId, int units)
        {
            if (units < 0)
                return ServiceResult.Fail("Units must be >= 0");

            var inv = await _context.BloodInventories.FirstOrDefaultAsync(x => x.Id == inventoryId);
            if (inv == null)
                return ServiceResult.Fail("Inventory row not found");

            inv.UnitsAvailable = units;
            await _context.SaveChangesAsync();

            return ServiceResult.Ok("Units updated successfully");
        }

        public async Task<ServiceResult> AddUnitsAsync(int inventoryId, int amount)
        {
            if (amount <= 0)
                return ServiceResult.Fail("Amount must be > 0");

            var inv = await _context.BloodInventories.FirstOrDefaultAsync(x => x.Id == inventoryId);
            if (inv == null)
                return ServiceResult.Fail("Inventory row not found");

            inv.UnitsAvailable += amount;
            await _context.SaveChangesAsync();

            return ServiceResult.Ok($"+{amount} units added");
        }

        public async Task<ServiceResult> RemoveUnitsAsync(int inventoryId, int amount)
        {
            if (amount <= 0)
                return ServiceResult.Fail("Amount must be > 0");

            var inv = await _context.BloodInventories.FirstOrDefaultAsync(x => x.Id == inventoryId);
            if (inv == null)
                return ServiceResult.Fail("Inventory row not found");

            if (inv.UnitsAvailable < amount)
                return ServiceResult.Fail("Not enough units to remove");

            inv.UnitsAvailable -= amount;
            await _context.SaveChangesAsync();

            return ServiceResult.Ok($"-{amount} units removed");
        }

        public async Task<ServiceResult> TryDeductUnitsAsync(int bloodTypeId, int units)
        {
            // Defensive: protects the inventory from incorrect callers.
            if (units <= 0)
                return ServiceResult.Fail("Invalid quantity");

            var inventory = await _context.BloodInventories
                .FirstOrDefaultAsync(i => i.BloodTypeId == bloodTypeId);

            if (inventory == null)
                return ServiceResult.Fail("Inventory not found");

            if (inventory.UnitsAvailable < units)
                return ServiceResult.Fail("Not enough blood units available");

            // Tracked change only; saved by the caller's SaveChangesAsync.
            inventory.UnitsAvailable -= units;

            return ServiceResult.Ok();
        }
    }
}
