using FluentAssertions;
using RoomMates.Services;

namespace RoomMates.Tests;

public class FuzzyMatchingServiceTests
{
    private readonly FuzzyMatchingService _service = new();

    [Fact]
    public void IsDuplicateOrTypo_NullOrEmptyInputs_HandlesEachInput()
    {
        _service.IsDuplicateOrTypo(null!, ["Quiet"]).Should().BeFalse();
        _service.IsDuplicateOrTypo("Quiet", null!).Should().BeFalse();
        _service.IsDuplicateOrTypo("", ["Quiet"]).Should().BeFalse();
        _service.IsDuplicateOrTypo("   ", ["Quiet"]).Should().BeFalse();
        _service.IsDuplicateOrTypo("Quiet", []).Should().BeFalse();
    }

    [Fact]
    public void IsDuplicateOrTypo_NegativeMaximumDistance_Throws()
    {
        Action act = () => _service.IsDuplicateOrTypo("Quiet", ["Quiet"], maxDistance: -1);
        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("maxDistance");
    }

    [Theory]
    [InlineData("  Quiet  ", "quiet")]
    [InlineData("quiet", "QUIET")]
    public void IsDuplicateOrTypo_ExactMatch_IgnoresWhitespaceAndCase(string input, string existing)
    {
        _service.IsDuplicateOrTypo(input, [existing]).Should().BeTrue();
    }

    [Theory]
    [InlineData("cat", "cats")] // insertion
    [InlineData("cats", "cat")] // deletion and longer-left swap
    [InlineData("cat", "cut")] // substitution
    [InlineData("form", "from")] // transposition (two edits)
    [InlineData("quiet", "quite")]
    [InlineData("quiet", "quick")] // two replacements
    public void IsDuplicateOrTypo_LevenshteinDistanceAtMostTwo_ReturnsTrue(string input, string existing)
    {
        _service.IsDuplicateOrTypo(input, [existing]).Should().BeTrue();
    }

    [Theory]
    [InlineData("cat", "dogs")]
    [InlineData("quiet", "loud")]
    public void IsDuplicateOrTypo_LevenshteinDistanceThreeOrMore_ReturnsFalse(string input, string existing)
    {
        _service.IsDuplicateOrTypo(input, [existing]).Should().BeFalse();
    }
}
