using System.ComponentModel.DataAnnotations;

namespace RoomMates.Models;

public class BookingRequest
{
    public Guid Id { get; set; }
    public Guid ProfileId { get; set; }
    public Profile Profile { get; set; } = null!;
    public Guid HousingId { get; set; }
    public Housing Housing { get; set; } = null!;
    public double MatchScore { get; set; }
    [Required, StringLength(40)] public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
