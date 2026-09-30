using FluentAssertions;
using RoomMates.Models;
using RoomMates.Services;

namespace RoomMates.Tests;

public class CompatibilityServiceTests
{
    private readonly CompatibilityService _service = new();

    [Fact]
    public void CalculateMatchScore_ReturnsOneHundredForPerfectCompatibleMatch()
    {
        // Arrange
        var profile = new Profile { Budget = 1_000, Cleanliness = 4, SleepSchedule = 2, PartyTolerance = 5 };
        var housing = new Housing
        {
            PricePerMonth = 900,
            RequiredCleanliness = 4,
            RequiredSleepSchedule = 2,
            RequiredPartyTolerance = 5,
            AllowsSmoking = true
        };

        // Act
        var score = _service.CalculateMatchScore(profile, housing);

        // Assert
        score.Should().Be(100.0);
    }

    [Fact]
    public void CalculateMatchScore_ReturnsZeroForPolarOppositeCoreScales()
    {
        // Arrange
        var profile = new Profile
        {
            Budget = 1_000,
            Cleanliness = 1,
            SleepSchedule = 1,
            PartyTolerance = 1
        };
        var housing = new Housing
        {
            PricePerMonth = 500,
            RequiredCleanliness = 5,
            RequiredSleepSchedule = 5,
            RequiredPartyTolerance = 5
        };

        // Act
        var score = _service.CalculateMatchScore(profile, housing);

        // Assert
        score.Should().Be(0.0);
    }

    [Fact]
    public void CalculateMatchScore_ReturnsZeroWhenBudgetIsBelowPrice()
    {
        // Arrange
        var profile = new Profile { Budget = 499 };
        var housing = new Housing { PricePerMonth = 500 };

        // Act
        var score = _service.CalculateMatchScore(profile, housing);

        // Assert
        score.Should().Be(0.0);
    }

    [Fact]
    public void CalculateMatchScore_ReturnsZeroForSmokingConflict()
    {
        // Arrange
        var profile = new Profile { Budget = 1_000, IsSmoker = true };
        var housing = new Housing { PricePerMonth = 500, AllowsSmoking = false };

        // Act
        var score = _service.CalculateMatchScore(profile, housing);

        // Assert
        score.Should().Be(0.0);
    }

    [Fact]
    public void CalculateMatchScore_ReturnsZeroWhenOwnPetsAreNotAllowed()
    {
        // Arrange
        var profile = new Profile { Budget = 1_000, OwnPets = true };
        var housing = new Housing { PricePerMonth = 500, PetPolicy = PetPolicy.NotAllowed };

        // Act
        var score = _service.CalculateMatchScore(profile, housing);

        // Assert
        score.Should().Be(0.0);
    }

    [Fact]
    public void CalculateMatchScore_ReturnsZeroWhenProfileDoesNotTolerateExistingPets()
    {
        // Arrange
        var profile = new Profile { Budget = 1_000, PetTolerance = PetPolicy.NotAllowed };
        var housing = new Housing
        {
            PricePerMonth = 500,
            PetPolicy = PetPolicy.Allowed,
            HasExistingPets = true
        };

        // Act
        var score = _service.CalculateMatchScore(profile, housing);

        // Assert
        score.Should().Be(0.0);
    }

    [Fact]
    public void CalculateMatchScore_AppliesFivePointConditionalPetPenalty()
    {
        // Arrange
        var profile = new Profile { Budget = 1_000, OwnPets = true };
        var housing = new Housing { PricePerMonth = 500, PetPolicy = PetPolicy.Conditional };

        // Act
        var score = _service.CalculateMatchScore(profile, housing);

        // Assert
        score.Should().Be(95.0);
    }

    [Fact]
    public void CalculateMatchScore_AddsFourPointsForTwoMatchingTags()
    {
        // Arrange
        var firstTagId = Guid.NewGuid();
        var secondTagId = Guid.NewGuid();
        var profileWithoutTags = new Profile { Budget = 1_000, Cleanliness = 1 };
        var housingWithoutTags = new Housing { PricePerMonth = 500, RequiredCleanliness = 3 };
        var profile = new Profile { Budget = 1_000, Cleanliness = 1 };
        profile.ProfileTags.Add(new ProfileTag { TagId = firstTagId });
        profile.ProfileTags.Add(new ProfileTag { TagId = secondTagId });

        var housing = new Housing { PricePerMonth = 500, RequiredCleanliness = 3 };
        housing.HousingTags.Add(new HousingTag { TagId = firstTagId });
        housing.HousingTags.Add(new HousingTag { TagId = secondTagId });

        // Act
        var baseScore = _service.CalculateMatchScore(profileWithoutTags, housingWithoutTags);
        var score = _service.CalculateMatchScore(profile, housing);

        // Assert
        score.Should().BeApproximately(baseScore + 4.0, 0.0001);
    }

    [Fact]
    public void CalculateMatchScore_ClampsExtremeBonusToOneHundred()
    {
        // Arrange
        var profile = new Profile { Budget = 1_000 };
        var housing = new Housing { PricePerMonth = 500 };
        for (var index = 0; index < 3; index++)
        {
            var tagId = Guid.NewGuid();
            profile.ProfileTags.Add(new ProfileTag { TagId = tagId });
            housing.HousingTags.Add(new HousingTag { TagId = tagId });
        }

        // Act
        var score = _service.CalculateMatchScore(profile, housing);

        // Assert
        score.Should().Be(100.0);
    }

    [Fact]
    public void CalculateMatchScore_ClampsExtremePenaltyToZero()
    {
        // Arrange
        var profile = new Profile
        {
            Budget = 1_000,
            OwnPets = true,
            Cleanliness = 1,
            SleepSchedule = 1,
            PartyTolerance = 1
        };
        var housing = new Housing
        {
            PricePerMonth = 500,
            PetPolicy = PetPolicy.Conditional,
            RequiredCleanliness = 5,
            RequiredSleepSchedule = 5,
            RequiredPartyTolerance = 5
        };

        // Act
        var score = _service.CalculateMatchScore(profile, housing);

        // Assert
        score.Should().Be(0.0);
    }
}
