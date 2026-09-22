namespace Blood_Donations_Project.Services
{
    /// <summary>
    /// Result type for HospitalService operations.
    /// Success: true, no error.
    /// Failure: false with optional error message and field name for validation.
    /// </summary>
    public class HospitalServiceResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public string? FieldName { get; set; }

        public static HospitalServiceResult Ok() => new() { Success = true };
        public static HospitalServiceResult Fail(string message, string? fieldName = null) 
            => new() { Success = false, Error = message, FieldName = fieldName };
    }
}
