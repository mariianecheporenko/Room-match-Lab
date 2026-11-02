
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomMates.Models;
using RoomMates.Data;
using RoomMates.Services;
using RoomMates.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
public class ProfilesMVCController : Controller
{
    private readonly RoomMatesDbContext _context;
    private readonly ITelegramNotificationService _telegramService;
    private readonly IHubContext<BookingHub>? _hubContext;

    public ProfilesMVCController(
        RoomMatesDbContext context,
        ITelegramNotificationService telegramService,
        IHubContext<BookingHub>? hubContext = null)
    {
        _context = context;
        _telegramService = telegramService;
        _hubContext = hubContext;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestLandlordStatus(CancellationToken cancellationToken)
    {
        if (!CurrentProfileId(out var profileId)) return Unauthorized();
        var profile = await _context.Profiles.FirstOrDefaultAsync(item => item.Id == profileId, cancellationToken);
        if (profile is null) return NotFound();
        if (profile.Role == "Admin")
        {
            TempData["ProfileMessage"] = "Admin accounts do not need landlord approval.";
            return RedirectToAction(nameof(Index));
        }
        if (profile.Role == "Landlord")
        {
            TempData["ProfileMessage"] = profile.IsApproved
                ? "Your account is already an approved landlord."
                : "Your landlord application is already pending admin approval.";
            return RedirectToAction(nameof(Index));
        }

        profile.Role = "Landlord";
        profile.IsApproved = false;
        await _context.SaveChangesAsync(cancellationToken);
        var claims = User.Claims
            .Where(claim => claim.Type != ClaimTypes.Role && claim.Type != "IsApproved")
            .Append(new Claim(ClaimTypes.Role, "Landlord"))
            .Append(new Claim("IsApproved", "False"));
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
        await _telegramService.SendLandlordApplicationNotificationAsync(profile, cancellationToken);
        if (_hubContext is not null)
            await _hubContext.Clients.Group(BookingHub.AdminsGroup).SendAsync(
                "NewLandlordApplication",
                new NewLandlordApplicationEvent(profile.Id, profile.Email, profile.FullName, profile.Role, profile.IsApproved),
                cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Admin(CancellationToken cancellationToken)
    {
        var profiles = await _context.Profiles.AsNoTracking()
            .OrderBy(profile => profile.FullName)
            .ToListAsync(cancellationToken);
        return View(profiles);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveLandlord(Guid id, CancellationToken cancellationToken)
    {
        var profile = await _context.Profiles.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (profile is null) return NotFound(new { error = "User not found." });
        if (profile.Role != "Landlord")
            return BadRequest(new { error = "Only landlord applicants can be approved." });

        profile.IsApproved = true;
        await _context.SaveChangesAsync(cancellationToken);
        if (_hubContext is not null)
        {
            var statusUpdate = new LandlordStatusUpdatedEvent(
                profile.Id, profile.Email, profile.Role, profile.IsApproved, profile.FullName);
            await _hubContext.Clients.Group(BookingHub.AdminsGroup)
                .SendAsync("LandlordStatusUpdated", statusUpdate, cancellationToken);
            await _hubContext.Clients.User(profile.Id.ToString())
                .SendAsync("LandlordStatusUpdated", statusUpdate, cancellationToken);
        }
        return Ok(new { id = profile.Id, name = profile.FullName, isApproved = profile.IsApproved });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeLandlord(Guid id, CancellationToken cancellationToken)
    {
        var profile = await _context.Profiles.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (profile is null) return NotFound(new { error = "User not found." });
        if (profile.Role != "Landlord")
            return BadRequest(new { error = "This account does not have the landlord role." });

        profile.Role = "Student";
        profile.IsApproved = true;
        await _context.SaveChangesAsync(cancellationToken);
        if (_hubContext is not null)
        {
            var statusUpdate = new LandlordStatusUpdatedEvent(
                profile.Id, profile.Email, profile.Role, profile.IsApproved, profile.FullName);
            await _hubContext.Clients.Group(BookingHub.AdminsGroup)
                .SendAsync("LandlordStatusUpdated", statusUpdate, cancellationToken);
            await _hubContext.Clients.User(profile.Id.ToString())
                .SendAsync("LandlordStatusUpdated", statusUpdate, cancellationToken);
        }
        return Ok(new { id = profile.Id, name = profile.FullName, role = profile.Role, isApproved = profile.IsApproved });
    }

    // GET: PROFILES
    public async Task<IActionResult> Index()    
    {
        if (!CurrentProfileId(out var profileId)) return Unauthorized();
        return View(await _context.Profiles.Where(profile => profile.Id == profileId).ToListAsync());
    }

    // GET: PROFILES/Details/5
    public async Task<IActionResult> Details(System.Guid? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        if (!CurrentProfileId(out var currentProfileId)) return Unauthorized();
        if (id != currentProfileId) return Forbid();

        var profile = await _context.Profiles
            .FirstOrDefaultAsync(m => m.Id == id);
        if (profile == null)
        {
            return NotFound();
        }

        return View(profile);
    }

    // GET: PROFILES/Create
    [AllowAnonymous]
    public IActionResult Create()
    {
        return View();
    }

    public IActionResult Password()
    {
        return View();
    }

    // GET: PROFILES/Edit/5
    public async Task<IActionResult> Edit(System.Guid? id)
    {
        if (!CurrentProfileId(out var currentProfileId)) return Unauthorized();
        if (id != currentProfileId) return Forbid();
        if (id == null)
        {
            return NotFound();
        }

        var profile = await _context.Profiles
            .Include(existingProfile => existingProfile.ProfileTags)
            .FirstOrDefaultAsync(existingProfile => existingProfile.Id == id);
        if (profile == null)
        {
            return NotFound();
        }
        ViewBag.AvailableTags = await _context.Tags.AsNoTracking().OrderBy(tag => tag.Name).ToListAsync();
        return View(profile);
    }

    // POST: PROFILES/Edit/5
    // To protect from overposting attacks, enable the specific properties you want to bind to.
    // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        System.Guid? id,
        [Bind("Id,FullName,Email,AvatarUrl,Budget,PetTolerance,OwnPets,IsSmoker,Cleanliness,SleepSchedule,PartyTolerance")] Profile profile,
        Guid[]? tagIds)
    {
        if (!CurrentProfileId(out var currentProfileId)) return Unauthorized();
        if (id != currentProfileId || profile.Id != currentProfileId) return Forbid();
        if (id != profile.Id)
        {
            return NotFound();
        }

        var selectedTagIds = (tagIds ?? []).Distinct().ToArray();
        if (selectedTagIds.Length > 5)
            ModelState.AddModelError(string.Empty, "A profile can have at most five tags.");

        var tags = await _context.Tags.AsNoTracking().OrderBy(tag => tag.Name).ToListAsync();
        if (selectedTagIds.Any(selectedId => tags.All(tag => tag.Id != selectedId)))
            ModelState.AddModelError(string.Empty, "One or more selected tags are invalid.");

        if (ModelState.IsValid)
        {
            try
            {
                var existing = await _context.Profiles
                    .Include(existingProfile => existingProfile.ProfileTags)
                    .FirstOrDefaultAsync(existingProfile => existingProfile.Id == profile.Id);
                if (existing is null)
                {
                    return NotFound();
                }

                existing.FullName = profile.FullName;
                existing.Email = profile.Email;
                existing.AvatarUrl = profile.AvatarUrl;
                existing.Budget = profile.Budget;
                existing.PetTolerance = profile.PetTolerance;
                existing.OwnPets = profile.OwnPets;
                existing.IsSmoker = profile.IsSmoker;
                existing.Cleanliness = profile.Cleanliness;
                existing.SleepSchedule = profile.SleepSchedule;
                existing.PartyTolerance = profile.PartyTolerance;
                foreach (var link in existing.ProfileTags.Where(link => !selectedTagIds.Contains(link.TagId)).ToList())
                    existing.ProfileTags.Remove(link);
                var existingTagIds = existing.ProfileTags.Select(link => link.TagId).ToHashSet();
                foreach (var selectedId in selectedTagIds.Where(selectedId => !existingTagIds.Contains(selectedId)))
                    existing.ProfileTags.Add(new ProfileTag { ProfileId = existing.Id, TagId = selectedId });
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
        ViewBag.AvailableTags = tags;
        profile.ProfileTags = selectedTagIds
            .Select(tagId => new ProfileTag { ProfileId = profile.Id, TagId = tagId })
            .ToList();
        return View(profile);
    }

    // GET: PROFILES/Delete/5
    public async Task<IActionResult> Delete(System.Guid? id)
    {
        if (!CurrentProfileId(out var currentProfileId)) return Unauthorized();
        if (id != currentProfileId) return Forbid();
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
        if (!CurrentProfileId(out var currentProfileId)) return Unauthorized();
        if (id != currentProfileId) return Forbid();
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

    private bool CurrentProfileId(out Guid profileId) =>
        Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out profileId);
}
