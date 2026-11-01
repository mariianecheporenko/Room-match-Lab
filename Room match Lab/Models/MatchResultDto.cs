using System.Text.Json.Serialization;

namespace RoomMates.Models;

public sealed class MatchResultDto
{
    public double MatchScore { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? DetailedStats { get; init; }
}
