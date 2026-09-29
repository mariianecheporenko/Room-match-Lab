using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomMates.Data;
using RoomMates.Models;

namespace RoomMates.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HousingsController(RoomMatesDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Housing>>> GetAll(
        [FromQuery] int skip = 0,
        [FromQuery] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        skip = Math.Max(0, skip);
        limit = Math.Clamp(limit, 1, 100);
        var housings = await dbContext.Housings.AsNoTracking()
            .Include(x => x.CustomRequirements).ThenInclude(x => x.LifestyleCriterion)
            .Include(x => x.HousingTags).ThenInclude(x => x.Tag)
            .OrderBy(x => x.Title)
            .Skip(skip)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return Ok(housings);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Housing>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var housing = await dbContext.Housings.AsNoTracking()
            .Include(x => x.CustomRequirements).ThenInclude(x => x.LifestyleCriterion)
            .Include(x => x.HousingTags).ThenInclude(x => x.Tag)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        return housing is null ? NotFound() : Ok(housing);
    }

    [HttpPost]
    public async Task<ActionResult<Housing>> Create(Housing housing, CancellationToken cancellationToken)
    {
        housing.Id = Guid.NewGuid();
        dbContext.Housings.Add(housing);
        await dbContext.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = housing.Id }, housing);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, Housing housing, CancellationToken cancellationToken)
    {
        if (id != housing.Id) return BadRequest("Route id must match the housing id.");
        if (!await dbContext.Housings.AnyAsync(x => x.Id == id, cancellationToken)) return NotFound();
        dbContext.Entry(housing).State = EntityState.Modified;
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var housing = await dbContext.Housings.FindAsync([id], cancellationToken);
        if (housing is null) return NotFound();
        dbContext.Housings.Remove(housing);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
