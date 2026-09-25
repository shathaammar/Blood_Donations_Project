namespace Blood_Donations_Project.ViewModels.Inventory
{
    /// <summary>
    /// One row of the Admin/BloodAvailability stock table.
    /// </summary>
    public class InventoryRowViewModel
    {
        /// <summary>BloodInventory.Id (used by the set/add/remove buttons).</summary>
        public int Id { get; set; }

        /// <summary>Blood type name; null only if the blood type row is missing.</summary>
        public string? BloodTypeName { get; set; }

        public int UnitsAvailable { get; set; }
    }
}
