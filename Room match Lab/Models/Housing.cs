using System.ComponentModel.DataAnnotations;

namespace RoomMates.Models;

public class Housing
{
    public Guid Id { get; set; }

    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(120)] public string City { get; set; } = string.Empty;
    [StringLength(120)] public string? District { get; set; }
    [Required, StringLength(200)] public string Street { get; set; } = string.Empty;
    [Required, StringLength(30)] public string BuildingNumber { get; set; } = string.Empty;
    [StringLength(30)] public string? ApartmentNumber { get; set; }

    public double Latitude { get; set; }
    public double Longitude { get; set; }

    [Range(0, double.MaxValue)] public decimal PricePerMonth { get; set; }
    public PetPolicy PetPolicy { get; set; } = PetPolicy.Allowed;
    public bool HasExistingPets { get; set; }
    public bool AllowsSmoking { get; set; }

    [Range(1, 5)] public int RequiredCleanliness { get; set; } = 3;
    [Range(1, 5)] public int RequiredSleepSchedule { get; set; } = 3;
    [Range(1, 5)] public int RequiredPartyTolerance { get; set; } = 3;

    public ICollection<HousingCriterionRequirement> CustomRequirements { get; set; } = new List<HousingCriterionRequirement>();
    public ICollection<HousingTag> HousingTags { get; set; } = new List<HousingTag>();
    public ICollection<BookingRequest> BookingRequests { get; set; } = new List<BookingRequest>();
}
