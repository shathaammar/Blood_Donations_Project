using Blood_Donations_Project.Models;

namespace Blood_Donations_Project.Services
{
    public interface IInventoryService
    {
        Task<List<BloodInventory>> GetInventoryAsync();
        Task<(bool success, string message)> SetUnitsAsync(int inventoryId, int units);
        Task<(bool success, string message)> AddUnitsAsync(int inventoryId, int amount);
        Task<(bool success, string message)> RemoveUnitsAsync(int inventoryId, int amount);
    }
}
