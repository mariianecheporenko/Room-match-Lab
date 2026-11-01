using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using RoomMates.Data;
using RoomMates.Models;

namespace RoomMates.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class TagsController(RoomMatesDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Tag>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await dbContext.Tags.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken));

    [HttpPost]
    public IActionResult Create() => Forbid();
}
