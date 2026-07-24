namespace PropertyManagementPortal.Services
{
    // Object keys arrive from the client after a direct-to-S3 upload, so they cannot
    // be trusted blindly before being written to the database. Shared by every
    // controller accepting a photo through the serverless upload flow.
    public static class PhotoKeyValidator
    {
        private const string Prefix = "photos/";
        private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];

        public static bool IsValid(string? key)
        {
            if (string.IsNullOrWhiteSpace(key)) return false;
            key = key.Trim();

            if (!key.StartsWith(Prefix, StringComparison.Ordinal)) return false;
            if (key.Length > 300 || key.Contains("..") || key.Contains('\\')) return false;

            var extension = Path.GetExtension(key).ToLowerInvariant();
            return AllowedExtensions.Contains(extension);
        }
    }
}
