using Microsoft.AspNetCore.Http;

namespace RoomMates.Services;

public interface IFileStorageService
{
    Task<string> UploadFileAsync(IFormFile file, string folderName = "roommates");
}
