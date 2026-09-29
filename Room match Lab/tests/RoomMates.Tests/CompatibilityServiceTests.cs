using RoomMates.Models;
using RoomMates.Services;

namespace RoomMates.Tests;

public class CompatibilityServiceTests
{
    private readonly CompatibilityService _service = new();

    [Fact]
    public void ReturnsZeroWhenBudgetCannotCoverRent()
    {
        var profile = new Profile { Budget = 500 };
        var housing = new Housing { PricePerMonth = 501 };
        Assert.Equal(0, _service.CalculateMatchScore(profile, housing));
    }

    [Fact]
    public void ReturnsZeroForPetAndSmokingDealbreakers()
    {
        Assert.Equal(0, _service.CalculateMatchScore(
            new Profile { Budget = 1000, OwnPets = true },
            new Housing { PricePerMonth = 500, PetPolicy = PetPolicy.NotAllowed }));
        Assert.Equal(0, _service.CalculateMatchScore(
            new Profile { Budget = 1000, PetTolerance = PetPolicy.NotAllowed },
            new Housing { PricePerMonth = 500, HasExistingPets = true }));
        Assert.Equal(0, _service.CalculateMatchScore(
            new Profile { Budget = 1000, IsSmoker = true },
            new Housing { PricePerMonth = 500, AllowsSmoking = false }));
    }

    [Fact]
    public void IdenticalCoreAndCustomCriteriaScoreOneHundred()
    {
        var criterionId = Guid.NewGuid();
        var profile = new Profile { Budget = 1000 };
        profile.CustomCriterionValues.Add(new ProfileCriterionValue { LifestyleCriterionId = criterionId, Value = 2 });
        var housing = new Housing { PricePerMonth = 500 };
        housing.CustomRequirements.Add(new HousingCriterionRequirement { LifestyleCriterionId = criterionId, TargetValue = 2 });

        Assert.Equal(100, _service.CalculateMatchScore(profile, housing));
    }

    [Fact]
    public void ScoresCustomCriteriaUsingExpandedScaleAndAddsTagBonus()
    {
        var criterionId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        var profile = new Profile { Budget = 1000 };
        profile.CustomCriterionValues.Add(new ProfileCriterionValue { LifestyleCriterionId = criterionId, Value = 1 });
        profile.ProfileTags.Add(new ProfileTag { TagId = tagId });
        var housing = new Housing { PricePerMonth = 500 };
        housing.CustomRequirements.Add(new HousingCriterionRequirement { LifestyleCriterionId = criterionId, TargetValue = 5 });
        housing.HousingTags.Add(new HousingTag { TagId = tagId });

        Assert.Equal(77, _service.CalculateMatchScore(profile, housing));
    }

    [Fact]
    public void ConditionalPetPolicyAllowsMatchWithFivePointPenalty()
    {
        var profile = new Profile { Budget = 1000, OwnPets = true };
        var housing = new Housing { PricePerMonth = 500, PetPolicy = PetPolicy.Conditional };
        Assert.Equal(95, _service.CalculateMatchScore(profile, housing));
    }
}
