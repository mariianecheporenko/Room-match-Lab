namespace RoomMates.Services;

public interface IFuzzyMatchingService
{
    bool IsDuplicateOrTypo(string inputName, IEnumerable<string> existingNames, int maxDistance = 2);
    string? FindSimilarName(string inputName, IEnumerable<string> existingNames, int maxDistance = 2);
}
