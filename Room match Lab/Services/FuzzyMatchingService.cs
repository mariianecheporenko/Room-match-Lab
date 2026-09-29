namespace RoomMates.Services;

public class FuzzyMatchingService : IFuzzyMatchingService
{
    public bool IsDuplicateOrTypo(string inputName, IEnumerable<string> existingNames, int maxDistance = 2) =>
        FindSimilarName(inputName, existingNames, maxDistance) is not null;

    public string? FindSimilarName(string inputName, IEnumerable<string> existingNames, int maxDistance = 2)
    {
        ArgumentNullException.ThrowIfNull(inputName);
        ArgumentNullException.ThrowIfNull(existingNames);
        if (maxDistance < 0) throw new ArgumentOutOfRangeException(nameof(maxDistance));

        var normalizedInput = Normalize(inputName);
        foreach (var existingName in existingNames)
        {
            var normalizedExisting = Normalize(existingName);
            if (LevenshteinDistance(normalizedInput, normalizedExisting, maxDistance) <= maxDistance)
                return existingName;
        }

        return null;
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();

    private static int LevenshteinDistance(string left, string right, int cutoff)
    {
        if (Math.Abs(left.Length - right.Length) > cutoff) return cutoff + 1;
        if (left.Length > right.Length) (left, right) = (right, left);

        var previous = Enumerable.Range(0, left.Length + 1).ToArray();
        var current = new int[left.Length + 1];
        for (var row = 1; row <= right.Length; row++)
        {
            current[0] = row;
            var rowMinimum = row;
            for (var column = 1; column <= left.Length; column++)
            {
                current[column] = Math.Min(Math.Min(current[column - 1] + 1, previous[column] + 1),
                    previous[column - 1] + (left[column - 1] == right[row - 1] ? 0 : 1));
                rowMinimum = Math.Min(rowMinimum, current[column]);
            }
            if (rowMinimum > cutoff) return cutoff + 1;
            (previous, current) = (current, previous);
        }
        return previous[left.Length];
    }
}
