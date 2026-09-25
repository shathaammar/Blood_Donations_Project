namespace Blood_Donations_Project.Common
{
    /// <summary>
    /// Status values stored on blood requests, donation requests and donations,
    /// exactly as saved in the database.
    /// </summary>
    public static class RequestStatuses
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
    }
}
