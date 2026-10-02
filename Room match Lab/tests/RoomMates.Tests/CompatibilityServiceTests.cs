using FluentAssertions;
using RoomMates.Models;
using RoomMates.Services;

namespace RoomMates.Tests;

public class CompatibilityServiceTests
{
    private readonly CompatibilityService _service = new();

    [Fact]
    public void CalculateMatchScore_NullArguments_Throws()
    {
        Action nullProfile = () => _service.CalculateMatchScore(null!, new Housing());
        Action nullHousing = () => _service.CalculateMatchScore(new Profile(), null!);

        nullProfile.Should().Throw<ArgumentNullException>().WithParameterName("profile");
        nullHousing.Should().Throw<ArgumentNullException>().WithParameterName("housing");
    }

    [Fact]
    public void CalculateMatchScore_BudgetOverLimit_IsDealbreaker()
    {
        var score = _service.CalculateMatchScore(new Profile { Budget = 5_000 }, new Housing { PricePerMonth = 6_000 });
        score.Should().Be(0.0);
    }

    [Fact]
    public void CalculateMatchScore_SmokerNotAllowed_IsDealbreaker()
    {
        var score = _service.CalculateMatchScore(
            new Profile { Budget = 5_000, IsSmoker = true },
            new Housing { PricePerMonth = 1_000, AllowsSmoking = false });
        score.Should().Be(0.0);
    }

    [Fact]
    public void CalculateMatchScore_OwnPetsNotAllowed_IsDealbreaker()
    {
        var score = _service.CalculateMatchScore(
            new Profile { Budget = 5_000, OwnPets = true },
            new Housing { PricePerMonth = 1_000, PetPolicy = PetPolicy.NotAllowed });
        score.Should().Be(0.0);
    }

    [Fact]
    public void CalculateMatchScore_ProfileCannotTolerateExistingPets_IsDealbreaker()
    {
        var score = _service.CalculateMatchScore(
            new Profile { Budget = 5_000, PetTolerance = PetPolicy.NotAllowed },
            new Housing { PricePerMonth = 1_000, PetPolicy = PetPolicy.Allowed, HasExistingPets = true });
        score.Should().Be(0.0);
    }

    [Fact]
    public void CalculateMatchScore_PerfectLifestyleAndSufficientBudget_ReturnsOneHundred()
    {
        var profile = new Profile { Budget = 5_000, Cleanliness = 4, SleepSchedule = 2, PartyTolerance = 5 };
        var housing = new Housing
        {
            PricePerMonth = 5_000,
            RequiredCleanliness = 4,
            RequiredSleepSchedule = 2,
            RequiredPartyTolerance = 5
        };

        _service.CalculateMatchScore(profile, housing).Should().Be(100.0);
    }

    [Fact]
    public void CalculateMatchScore_OppositeLifestyleScales_HasZeroBaseScore()
    {
        var profile = new Profile { Budget = 5_000, Cleanliness = 1, SleepSchedule = 1, PartyTolerance = 1 };
        var housing = new Housing
        {
            PricePerMonth = 1_000,
            RequiredCleanliness = 5,
            RequiredSleepSchedule = 5,
            RequiredPartyTolerance = 5
        };

        _service.CalculateMatchScore(profile, housing).Should().Be(0.0);
    }

    [Fact]
    public void CalculateMatchScore_NoSharedTags_AddsNoBonus()
    {
        var profile = new Profile { Budget = 1_000 };
        profile.ProfileTags.Add(new ProfileTag { TagId = Guid.NewGuid() });
        var housing = new Housing { PricePerMonth = 500 };
        housing.HousingTags.Add(new HousingTag { TagId = Guid.NewGuid() });

        _service.CalculateMatchScore(profile, housing).Should().Be(100.0);
    }

    [Fact]
    public void CalculateMatchScore_TwoSharedTags_AddsFourPercentagePoints()
    {
        var firstTag = Guid.NewGuid();
        var secondTag = Guid.NewGuid();
        var profile = new Profile { Budget = 1_000, Cleanliness = 1 };
        profile.ProfileTags.Add(new ProfileTag { TagId = firstTag });
        profile.ProfileTags.Add(new ProfileTag { TagId = secondTag });
        var housing = new Housing { PricePerMonth = 500, RequiredCleanliness = 3 };
        housing.HousingTags.Add(new HousingTag { TagId = firstTag });
        housing.HousingTags.Add(new HousingTag { TagId = secondTag });

        _service.CalculateMatchScore(profile, housing).Should().BeApproximately(87.3333333333, 0.000001);
    }

    [Fact]
    public void CalculateMatchScore_OwnPetsWithConditionalPolicy_DeductsFivePoints()
    {
        var profile = new Profile { Budget = 1_000, OwnPets = true };
        var housing = new Housing { PricePerMonth = 500, PetPolicy = PetPolicy.Conditional };
        _service.CalculateMatchScore(profile, housing).Should().Be(95.0);
    }

    [Fact]
    public void CalculateMatchScore_NoOwnPetsAndPetsProhibited_HasNoPetPenalty()
    {
        var profile = new Profile { Budget = 1_000, OwnPets = false };
        var housing = new Housing { PricePerMonth = 500, PetPolicy = PetPolicy.NotAllowed };
        _service.CalculateMatchScore(profile, housing).Should().Be(100.0);
    }

    [Fact]
    public void CalculateMatchScore_ExcessiveTagBonus_ClampsToOneHundred()
    {
        var profile = new Profile { Budget = 1_000 };
        var housing = new Housing { PricePerMonth = 500 };
        for (var i = 0; i < 3; i++)
        {
            var tagId = Guid.NewGuid();
            profile.ProfileTags.Add(new ProfileTag { TagId = tagId });
            housing.HousingTags.Add(new HousingTag { TagId = tagId });
        }

        _service.CalculateMatchScore(profile, housing).Should().Be(100.0);
    }

    [Fact]
    public void CalculateMatchScore_PetPenaltyAtZeroBase_ClampsAtZero()
    {
        var profile = new Profile
        {
            Budget = 1_000, OwnPets = true,
            Cleanliness = 1, SleepSchedule = 1, PartyTolerance = 1
        };
        var housing = new Housing
        {
            PricePerMonth = 500, PetPolicy = PetPolicy.Conditional,
            RequiredCleanliness = 5, RequiredSleepSchedule = 5, RequiredPartyTolerance = 5
        };

        _service.CalculateMatchScore(profile, housing).Should().Be(0.0);
    }

    [Fact]
    public void CalculateMatchScore_CustomRequirements_OnlyMatchingProfileCriteriaAffectScore()
    {
        var criterionId = Guid.NewGuid();
        var unmatchedCriterionId = Guid.NewGuid();
        var profile = new Profile { Budget = 1_000 };
        profile.CustomCriterionValues.Add(new ProfileCriterionValue { LifestyleCriterionId = criterionId, Value = 1 });
        var housing = new Housing { PricePerMonth = 500 };
        housing.CustomRequirements.Add(new HousingCriterionRequirement { LifestyleCriterionId = criterionId, TargetValue = 5 });
        housing.CustomRequirements.Add(new HousingCriterionRequirement { LifestyleCriterionId = unmatchedCriterionId, TargetValue = 5 });

        _service.CalculateMatchScore(profile, housing).Should().Be(75.0);
    }
}
