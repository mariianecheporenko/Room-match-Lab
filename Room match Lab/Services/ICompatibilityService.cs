using RoomMates.Models;

namespace RoomMates.Services;

public interface ICompatibilityService
{
    double CalculateMatchScore(Profile profile, Housing housing);
}
