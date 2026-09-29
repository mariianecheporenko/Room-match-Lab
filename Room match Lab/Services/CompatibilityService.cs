using RoomMates.Models;

namespace RoomMates.Services;

public class CompatibilityService : ICompatibilityService
{
    private const double TagBonusPercent = 2.0;

    public double CalculateMatchScore(Profile profile, Housing housing)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(housing);

        if (profile.Budget < housing.PricePerMonth ||
            (profile.OwnPets && housing.PetPolicy == PetPolicy.NotAllowed) ||
            (profile.PetTolerance == PetPolicy.NotAllowed && housing.HasExistingPets) ||
            (profile.IsSmoker && !housing.AllowsSmoking))
            return 0.0;

        var difference = Math.Abs(profile.Cleanliness - housing.RequiredCleanliness)
                       + Math.Abs(profile.SleepSchedule - housing.RequiredSleepSchedule)
                       + Math.Abs(profile.PartyTolerance - housing.RequiredPartyTolerance);
        var maxDifference = 12.0;

        var profileValues = profile.CustomCriterionValues.ToDictionary(x => x.LifestyleCriterionId);
        foreach (var requirement in housing.CustomRequirements)
        {
            if (!profileValues.TryGetValue(requirement.LifestyleCriterionId, out var value)) continue;
            difference += Math.Abs(value.Value - requirement.TargetValue);
            maxDifference += 4.0;
        }

        var score = maxDifference == 0 ? 100 : 100.0 * (1.0 - difference / maxDifference);
        var profileTagIds = profile.ProfileTags.Select(x => x.TagId).ToHashSet();
        var sharedTagCount = housing.HousingTags.Count(x => profileTagIds.Contains(x.TagId));
        score += sharedTagCount * TagBonusPercent;

        if (housing.PetPolicy == PetPolicy.Conditional && profile.OwnPets)
            score -= 5.0;

        return Math.Clamp(score, 0.0, 100.0);
    }
}
