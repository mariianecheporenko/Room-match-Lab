using RoomMates.Services;

namespace RoomMates.Tests;

public class FuzzyMatchingServiceTests
{
    private readonly FuzzyMatchingService _service = new();

    [Theory]
    [InlineData("smooking", "smoking")]
    [InlineData("  CLEANLINESS ", "cleanliness")]
    public void FindsNamesWithinTwoEdits(string input, string existing) =>
        Assert.True(_service.IsDuplicateOrTypo(input, [existing]));

    [Fact]
    public void DoesNotFindDistantName() =>
        Assert.False(_service.IsDuplicateOrTypo("gardening", ["smoking"]));

    [Fact]
    public void ReturnsOriginalMatchingName() =>
        Assert.Equal("Smoking", _service.FindSimilarName("smooking", ["Smoking"]));
}
