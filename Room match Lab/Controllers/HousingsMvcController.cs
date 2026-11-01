using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoomMates.Data;
using RoomMates.Models;

[Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
public class HousingsMvcController(RoomMatesDbContext context) : Controller
{
    private readonly RoomMatesDbContext _context = context;

    public async Task<IActionResult> Index()
    {
        return View(await VisibleHousings()
            .OrderBy(housing => housing.Title)
            .ToListAsync());
    }

    public async Task<IActionResult> Details(Guid id)
    {
        var housing = await VisibleHousings()
            .Include(item => item.Landlord)
            .FirstOrDefaultAsync(item => item.Id == id);

        return housing is null ? NotFound() : View(housing);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id)
    {
        var housing = await VisibleHousings()
            .FirstOrDefaultAsync(item => item.Id == id);
        if (housing is not null && !CanManageHousing(housing)) return Forbid();
        return housing is null ? NotFound() : View(housing);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, [Bind("Id,Title,City,District,Street,BuildingNumber,ApartmentNumber,Latitude,Longitude,PricePerMonth,PetPolicy,HasExistingPets,AllowsSmoking,RequiredCleanliness,RequiredSleepSchedule,RequiredPartyTolerance")] Housing input)
    {
        if (id != input.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var housing = await VisibleHousings(asNoTracking: false)
            .FirstOrDefaultAsync(item => item.Id == id);
        if (housing is null)
        {
            return NotFound();
        }
        if (!CanManageHousing(housing)) return Forbid();

        housing.Title = input.Title;
        housing.City = input.City;
        housing.District = input.District;
        housing.Street = input.Street;
        housing.BuildingNumber = input.BuildingNumber;
        housing.ApartmentNumber = input.ApartmentNumber;
        housing.Latitude = input.Latitude;
        housing.Longitude = input.Longitude;
        housing.PricePerMonth = input.PricePerMonth;
        housing.PetPolicy = input.PetPolicy;
        housing.HasExistingPets = input.HasExistingPets;
        housing.AllowsSmoking = input.AllowsSmoking;
        housing.RequiredCleanliness = input.RequiredCleanliness;
        housing.RequiredSleepSchedule = input.RequiredSleepSchedule;
        housing.RequiredPartyTolerance = input.RequiredPartyTolerance;

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(Guid id)
    {
        var housing = await VisibleHousings()
            .FirstOrDefaultAsync(item => item.Id == id);
        if (housing is not null && !CanManageHousing(housing)) return Forbid();
        return housing is null ? NotFound() : View(housing);
    }

    [HttpGet]
    public IActionResult Create() =>
        string.IsNullOrWhiteSpace(User.FindFirstValue("TenantId")) ? Forbid() : View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("Title,City,District,Street,BuildingNumber,ApartmentNumber,Latitude,Longitude,PricePerMonth,PetPolicy,HasExistingPets,AllowsSmoking,RequiredCleanliness,RequiredSleepSchedule,RequiredPartyTolerance")] Housing housing)
    {
        var tenantId = User.FindFirstValue("TenantId");
        if (string.IsNullOrWhiteSpace(tenantId)) return Forbid();
        var currentProfile = await FindCurrentProfileAsync();
        if (currentProfile is null) return Forbid();
        if (!ModelState.IsValid)
        {
            return View(housing);
        }

        housing.Id = Guid.NewGuid();
        housing.TenantId = tenantId;
        housing.LandlordId = currentProfile.Id;
        housing.Landlord = currentProfile;
        _context.Housings.Add(housing);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private Task<Profile?> FindCurrentProfileAsync()
    {
        var profileIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(profileIdClaim, out var profileId))
            return _context.Profiles.FirstOrDefaultAsync(profile => profile.Id == profileId);

        var email = User.FindFirstValue(ClaimTypes.Email);
        return string.IsNullOrWhiteSpace(email)
            ? Task.FromResult<Profile?>(null)
            : _context.Profiles.FirstOrDefaultAsync(profile => profile.Email == email);
    }

    [HttpPost("HousingsMvc/Delete/{id:guid}")]
    [ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(Guid id)
    {
        var housing = await VisibleHousings().FirstOrDefaultAsync(item => item.Id == id);
        if (housing is not null)
        {
            if (!CanManageHousing(housing)) return Forbid();
            _context.Housings.Remove(housing);
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    private IQueryable<Housing> VisibleHousings(bool asNoTracking = true)
    {
        IQueryable<Housing> housings = _context.Housings;
        if (asNoTracking)
            housings = housings.AsNoTracking();
        var tenantId = User.FindFirstValue("TenantId");
        var isCrossUniversity = bool.TryParse(
            User.FindFirstValue("IsCrossUniversity"),
            out var parsedCrossUniversity) && parsedCrossUniversity;

        if (!isCrossUniversity && !string.IsNullOrWhiteSpace(tenantId))
            housings = housings.Where(housing => housing.TenantId == tenantId || housing.TenantId == null);

        return housings;
    }

    private bool CanManageHousing(Housing housing)
    {
        var tenantId = User.FindFirstValue("TenantId");
        return !string.IsNullOrWhiteSpace(tenantId) && housing.TenantId == tenantId;
    }
}
