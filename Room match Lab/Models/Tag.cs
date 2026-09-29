using System.ComponentModel.DataAnnotations;

namespace RoomMates.Models;

public class Tag
{
    public Guid Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    public ICollection<ProfileTag> ProfileTags { get; set; } = new List<ProfileTag>();
    public ICollection<HousingTag> HousingTags { get; set; } = new List<HousingTag>();
}

public class ProfileTag
{
    public Guid ProfileId { get; set; }
    public Profile Profile { get; set; } = null!;
    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}

public class HousingTag
{
    public Guid HousingId { get; set; }
    public Housing Housing { get; set; } = null!;
    public Guid TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}
