using Blood_Donations_Project.Common;
using Blood_Donations_Project.Models;
using Blood_Donations_Project.ViewModels.Inventory;
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

        public async Task<List<InventoryRowViewModel>> GetInventoryAsync()
        {
            // Same ordering as before; projected in the query instead of loading entities.
            return await _context.BloodInventories
                .OrderBy(i => i.BloodType!.TypeName)
                .Select(i => new InventoryRowViewModel
                {
                    Id = i.Id,
                    BloodTypeName = i.BloodType != null ? i.BloodType.TypeName : null,
                    UnitsAvailable = i.UnitsAvailable
                })
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

        public async Task<bool> TryAddDonatedUnitAsync(int bloodTypeId)
        {
            var inventory = await _context.BloodInventories
                .FirstOrDefaultAsync(i => i.BloodTypeId == bloodTypeId);

            if (inventory == null)
                return false;

            // Tracked change only; saved by the caller's SaveChangesAsync.
            inventory.UnitsAvailable += 1;
            return true;
        }
    }
}
