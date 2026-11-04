using Microsoft.AspNetCore.Mvc;
using RoomMates.Services;

namespace Room_match_Lab.Controllers;

[ApiController]
[Route("api/files")]
public sealed class FilesController(IFileStorageService fileStorageService) : ControllerBase
{
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload([FromForm] IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "A non-empty file is required." });

        var url = await fileStorageService.UploadFileAsync(file);
        return Ok(new { url });
    }
}
