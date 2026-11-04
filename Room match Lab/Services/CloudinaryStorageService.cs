using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;

namespace RoomMates.Services;

public sealed class CloudinaryStorageService : IFileStorageService
{
    private readonly string? _cloudName;
    private readonly string? _apiKey;
    private readonly string? _apiSecret;

    public CloudinaryStorageService(IConfiguration configuration)
    {
        var cloudinarySettings = configuration.GetSection("Cloudinary");
        _cloudName = cloudinarySettings["CloudName"];
        _apiKey = cloudinarySettings["ApiKey"];
        _apiSecret = cloudinarySettings["ApiSecret"];
    }

    public async Task<string> UploadFileAsync(IFormFile file, string folderName = "roommates")
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Length <= 0)
            throw new ArgumentException("The uploaded file is empty.", nameof(file));
        if (string.IsNullOrWhiteSpace(_cloudName) ||
            string.IsNullOrWhiteSpace(_apiKey) ||
            string.IsNullOrWhiteSpace(_apiSecret))
            throw new InvalidOperationException("Cloudinary:CloudName, Cloudinary:ApiKey, and Cloudinary:ApiSecret must be configured before uploading files.");

        var cloudinary = new Cloudinary(new Account(_cloudName, _apiKey, _apiSecret));
        await using var stream = file.OpenReadStream();
        var uploadResult = await cloudinary.UploadAsync(new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = folderName
        });

        if (uploadResult.Error is not null)
            throw new InvalidOperationException($"Cloudinary upload failed: {uploadResult.Error.Message}");

        return uploadResult.SecureUrl.ToString();
    }
}
