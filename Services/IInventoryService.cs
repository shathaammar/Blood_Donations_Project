using Blood_Donations_Project.Common;
using Blood_Donations_Project.Models;

namespace Blood_Donations_Project.Services
{
    public interface IInventoryService
    {
        Task<List<BloodInventory>> GetInventoryAsync();
        Task<ServiceResult> SetUnitsAsync(int inventoryId, int units);
        Task<ServiceResult> AddUnitsAsync(int inventoryId, int amount);
        Task<ServiceResult> RemoveUnitsAsync(int inventoryId, int amount);

        /// <summary>
        /// Finds the inventory row for a blood type, checks stock and subtracts the units
        /// on the tracked entity. Does NOT call SaveChangesAsync: the caller saves it
        /// together with its own changes through the same scoped DbContext.
        /// Fails without changing anything when units &lt;= 0, the row is missing or stock is too low.
        /// </summary>
        Task<ServiceResult> TryDeductUnitsAsync(int bloodTypeId, int units);
    }
}
