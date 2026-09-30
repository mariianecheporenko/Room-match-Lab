using FluentAssertions;
using RoomMates.Services;

namespace RoomMates.Tests;

public class FuzzyMatchingServiceTests
{
    private readonly FuzzyMatchingService _service = new();

    [Theory]
    [InlineData("Smoking", "Smoking")]
    [InlineData("Smoking", "SMOKING")]
    [InlineData("SMOKING", "smoking")]
    [InlineData("  Cat  ", "Cat")]
    [InlineData("Catt", "Cat")]
    [InlineData("Smooking", "Smoking")]
    [InlineData("Pparty", "Party")]
    public void IsDuplicateOrTypo_ReturnsTrueForExactNormalizedOrNearbyNames(
        string inputName,
        string existingName)
    {
        // Arrange
        var existingNames = new[] { existingName };

        // Act
        var result = _service.IsDuplicateOrTypo(inputName, existingNames);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsDuplicateOrTypo_ReturnsFalseWhenNamesAreMoreThanTwoEditsApart()
    {
        // Arrange
        const string inputName = "Quiet";
        var existingNames = new[] { "Parties" };

        // Act
        var result = _service.IsDuplicateOrTypo(inputName, existingNames);

        // Assert
        result.Should().BeFalse();
    }
}
