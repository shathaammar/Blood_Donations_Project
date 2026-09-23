namespace Blood_Donations_Project.ViewModels.Dashboard
{
    /// <summary>
    /// Flat blood-request row (previously ViewModels.BloodRequestRowTable).
    /// Used by the admin and hospital dashboards and by Admin/ManageBloodRequests.
    /// </summary>
    public class BloodRequestRowViewModel
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public string? UserName { get; set; }
        public int? BloodTypeId { get; set; }
        public string? BloodTypeName { get; set; }
        public DateOnly? RequestDate { get; set; }
        public int? Quantity { get; set; }
        public string? Status { get; set; }
    }
}
