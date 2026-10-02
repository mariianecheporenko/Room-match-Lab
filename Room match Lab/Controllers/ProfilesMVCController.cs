
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomMates.Models;
using RoomMates.Data;

public class ProfilesMVCController : Controller
{
    private readonly RoomMatesDbContext _context;

    public ProfilesMVCController(RoomMatesDbContext context)
    {
        _context = context;
    }

    // GET: PROFILES
    public async Task<IActionResult> Index()    
    {
        return View(await _context.Profiles.ToListAsync());
    }

    // GET: PROFILES/Details/5
    public async Task<IActionResult> Details(System.Guid? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var profile = await _context.Profiles
            .FirstOrDefaultAsync(m => m.Id == id);
        if (profile == null)
        {
            return NotFound();
        }

        return View(profile);
    }

    // GET: PROFILES/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: PROFILES/Create
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("FullName,Email,AvatarUrl,Budget,PetTolerance,OwnPets,IsSmoker,Cleanliness,SleepSchedule,PartyTolerance")] Profile profile)
    {
        if (ModelState.IsValid)
        {
            profile.Id = Guid.NewGuid();
            _context.Add(profile);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        return View(profile);
    }

    // GET: PROFILES/Edit/5
    public async Task<IActionResult> Edit(System.Guid? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var profile = await _context.Profiles.FindAsync(id);
        if (profile == null)
        {
            return NotFound();
        }
        return View(profile);
    }

    // POST: PROFILES/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(System.Guid? id, [Bind("Id,FullName,Email,AvatarUrl,Budget,PetTolerance,OwnPets,IsSmoker,Cleanliness,SleepSchedule,PartyTolerance,ProfileTags,CustomCriterionValues,BookingRequests")] Profile profile)
    {
        if (id != profile.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(profile);
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProfileExists(profile.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }
        return View(profile);
    }

    // GET: PROFILES/Delete/5
    public async Task<IActionResult> Delete(System.Guid? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var profile = await _context.Profiles
            .FirstOrDefaultAsync(m => m.Id == id);
        if (profile == null)
        {
            return NotFound();
        }

        return View(profile);
    }

    // POST: PROFILES/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(System.Guid? id)
    {
        var profile = await _context.Profiles.FindAsync(id);
        if (profile != null)
        {
            _context.Profiles.Remove(profile);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private bool ProfileExists(System.Guid? id)
    {
        return _context.Profiles.Any(e => e.Id == id);
    }
}
