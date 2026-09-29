using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomMates.Data;
using RoomMates.Models;
using RoomMates.Services;

namespace RoomMates.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TagsController(RoomMatesDbContext dbContext, IFuzzyMatchingService fuzzyMatching) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Tag>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await dbContext.Tags.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<Tag>> Create(Tag tag, CancellationToken cancellationToken)
    {
        tag.Name = tag.Name.Trim();
        if (tag.Name.Length == 0) return BadRequest("Tag name is required.");
        var similar = fuzzyMatching.FindSimilarName(tag.Name,
            await dbContext.Tags.Select(x => x.Name).ToListAsync(cancellationToken));
        if (similar is not null) return Conflict(new { error = $"Similar criterion already exists: {similar}" });

        tag.Id = Guid.NewGuid();
        dbContext.Tags.Add(tag);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Created($"/api/tags/{tag.Id}", tag);
    }
}
