using Amazon.S3;
using Amazon.S3.Model;

public class S3Service : IS3Service
{
    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;

    // The SDK client and configuration are automatically injected here
    public S3Service(IAmazonS3 s3Client, IConfiguration config)
    {
        _s3Client = s3Client;
        // Reads from your appsettings.json or environment variables
        _bucketName = config["S3Config:BucketName"] ?? throw new ArgumentNullException("BucketName missing");
    }

    public async Task<string> UploadFileAsync(IFormFile file)
    {
        var request = new PutObjectRequest
        {
            BucketName = _bucketName,
            Key = Guid.NewGuid().ToString() + "_" + file.FileName, // Prevents duplicate name overwrites
            InputStream = file.OpenReadStream(),
            ContentType = file.ContentType
        };

        await _s3Client.PutObjectAsync(request);
        return request.Key; // Return the unique S3 filename to save in your database
    }

    public string GetPresignedUrl(string? fileName, double durationMinutes = 60)
    {
        if (string.IsNullOrEmpty(fileName)) return string.Empty;
        
        // Generates a temporary, secure link without downloading the file payload to your server
        return _s3Client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = fileName,
            Expires = DateTime.UtcNow.AddMinutes(durationMinutes)
        });
    }

    public async Task<Stream> DownloadFileAsync(string fileName)
    {
        var request = new GetObjectRequest
        {
            BucketName = _bucketName,
            Key = fileName
        };

        var response = await _s3Client.GetObjectAsync(request);
        return response.ResponseStream;
    }
}