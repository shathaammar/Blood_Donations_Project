namespace Blood_Donations_Project.Common
{
    /// <summary>
    /// Shared result type for service operations.
    ///
    /// - Ok:       Success = true, optional message.
    /// - Fail:     Success = false, message, optional FieldName for ModelState association.
    /// - NotFound: Success = false, IsNotFound = true (controllers map this to NotFound()).
    /// </summary>
    public class ServiceResult
    {
        private ServiceResult(bool success, string message, string? fieldName, bool isNotFound)
        {
            Success = success;
            Message = message;
            FieldName = fieldName;
            IsNotFound = isNotFound;
        }

        public bool Success { get; }

        public string Message { get; }

        /// <summary>
        /// Form field the failure belongs to (e.g. "Email"); null for model-level errors.
        /// </summary>
        public string? FieldName { get; }

        public bool IsNotFound { get; }

        public static ServiceResult Ok(string message = "")
            => new(true, message, null, false);

        public static ServiceResult Fail(string message, string? fieldName = null)
            => new(false, message, fieldName, false);

        public static ServiceResult NotFound(string message = "")
            => new(false, message, null, true);
    }
}
