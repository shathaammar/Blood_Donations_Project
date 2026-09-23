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
    }
}
