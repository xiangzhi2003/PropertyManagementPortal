public interface IS3Service
{
    Task<string> UploadFileAsync(IFormFile file);
    Task<Stream> DownloadFileAsync(string fileName);
    string GetPresignedUrl(string? fileName, double durationMinutes = 60);
}