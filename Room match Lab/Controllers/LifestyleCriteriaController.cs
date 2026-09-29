using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomMates.Data;
using RoomMates.Models;
using RoomMates.Services;

namespace RoomMates.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LifestyleCriteriaController(RoomMatesDbContext dbContext, IFuzzyMatchingService fuzzyMatching) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LifestyleCriterion>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await dbContext.LifestyleCriteria.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<LifestyleCriterion>> Create(LifestyleCriterion criterion, CancellationToken cancellationToken)
    {
        criterion.Name = criterion.Name.Trim();
        if (criterion.Name.Length == 0) return BadRequest("Criterion name is required.");
        var similar = fuzzyMatching.FindSimilarName(criterion.Name,
            await dbContext.LifestyleCriteria.Select(x => x.Name).ToListAsync(cancellationToken));
        if (similar is not null) return Conflict(new { error = $"Similar criterion already exists: {similar}" });
        if (criterion.ScaleMin >= criterion.ScaleMax) return BadRequest("ScaleMin must be less than ScaleMax.");

        criterion.Id = Guid.NewGuid();
        dbContext.LifestyleCriteria.Add(criterion);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Created($"/api/lifestylecriteria/{criterion.Id}", criterion);
    }
}
