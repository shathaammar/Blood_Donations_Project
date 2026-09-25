using Blood_Donations_Project.ViewModels.Shared;

namespace Blood_Donations_Project.Services
{
    /// <summary>
    /// Shared read-only lookup for blood type dropdowns
    /// (Register, Profile, Edit Donor, Request Blood).
    /// </summary>
    public interface IBloodTypeLookupService
    {
        /// <summary>All blood types, in the database's natural (unordered) sequence.</summary>
        Task<List<BloodTypeOptionViewModel>> GetBloodTypeOptionsAsync();
    }
}
