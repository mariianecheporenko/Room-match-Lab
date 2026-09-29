using System.ComponentModel.DataAnnotations;

namespace RoomMates.Models;

public class Profile
{
    public Guid Id { get; set; }

    [Required, StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(320)]
    public string Email { get; set; } = string.Empty;

    [Required, Url, StringLength(2048)]
    public string AvatarUrl { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal Budget { get; set; }

    public PetPolicy PetTolerance { get; set; } = PetPolicy.Allowed;
    public bool OwnPets { get; set; }
    public bool IsSmoker { get; set; }

    [Range(1, 5)] public int Cleanliness { get; set; } = 3;
    [Range(1, 5)] public int SleepSchedule { get; set; } = 3;
    [Range(1, 5)] public int PartyTolerance { get; set; } = 3;

    public ICollection<ProfileTag> ProfileTags { get; set; } = new List<ProfileTag>();
    public ICollection<ProfileCriterionValue> CustomCriterionValues { get; set; } = new List<ProfileCriterionValue>();
    public ICollection<BookingRequest> BookingRequests { get; set; } = new List<BookingRequest>();
}
