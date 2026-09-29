using System.ComponentModel.DataAnnotations;

namespace RoomMates.Models;

public class LifestyleCriterion
{
    public Guid Id { get; set; }

    [Required, StringLength(120)] public string Name { get; set; } = string.Empty;
    [StringLength(1000)] public string Description { get; set; } = string.Empty;
    public int ScaleMin { get; set; } = 1;
    public int ScaleMax { get; set; } = 5;
    public ICollection<ProfileCriterionValue> ProfileValues { get; set; } = new List<ProfileCriterionValue>();
    public ICollection<HousingCriterionRequirement> HousingRequirements { get; set; } = new List<HousingCriterionRequirement>();
}

public class ProfileCriterionValue
{
    public Guid ProfileId { get; set; }
    public Profile Profile { get; set; } = null!;
    public Guid LifestyleCriterionId { get; set; }
    public LifestyleCriterion LifestyleCriterion { get; set; } = null!;
    [Range(1, 5)] public int Value { get; set; }
}

public class HousingCriterionRequirement
{
    public Guid HousingId { get; set; }
    public Housing Housing { get; set; } = null!;
    public Guid LifestyleCriterionId { get; set; }
    public LifestyleCriterion LifestyleCriterion { get; set; } = null!;
    [Range(1, 5)] public int TargetValue { get; set; }
}
