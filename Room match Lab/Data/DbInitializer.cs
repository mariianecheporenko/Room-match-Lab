using Microsoft.EntityFrameworkCore;
using RoomMates.Data;
using RoomMates.Models;

namespace Room_match_Lab.Data;


public static class DbInitializer
{
    public static void Seed(RoomMatesDbContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Database.Migrate();

        var tags = SeedTags(context);
        var criteria = SeedCriteria(context);
        var profiles = SeedProfiles(context);

        SeedProfileTags(context, profiles, tags);
        SeedHousings(context, profiles);
        SeedHousingTags(context, tags);
    }

    private static Dictionary<string, Tag> SeedTags(RoomMatesDbContext context)
    {
        var names = new[] { "Quiet Environment", "IT Student", "Sports", "Early Bird", "Pet Lover" };
        var tags = new Dictionary<string, Tag>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            var tag = context.Tags.SingleOrDefault(item => item.Name == name);
            if (tag is null)
            {
                tag = new Tag { Id = Guid.NewGuid(), Name = name };
                context.Tags.Add(tag);
            }
            tags.Add(name, tag);
        }

        context.SaveChanges();
        return tags;
    }

    private static Dictionary<string, LifestyleCriterion> SeedCriteria(RoomMatesDbContext context)
    {
        var definitions = new Dictionary<string, string>
        {
            ["Guests Frequency"] = "How often friends visit",
            ["Cooking Frequency"] = "How often the resident cooks at home"
        };
        var criteria = new Dictionary<string, LifestyleCriterion>(StringComparer.Ordinal);
        foreach (var (name, description) in definitions)
        {
            var criterion = context.LifestyleCriteria.SingleOrDefault(item => item.Name == name);
            if (criterion is null)
            {
                criterion = new LifestyleCriterion
                {
                    Id = Guid.NewGuid(),
                    Name = name,
                    Description = description,
                    ScaleMin = 1,
                    ScaleMax = 5
                };
                context.LifestyleCriteria.Add(criterion);
            }
            criteria.Add(name, criterion);
        }

        context.SaveChanges();
        return criteria;
    }

    private static Dictionary<string, Profile> SeedProfiles(RoomMatesDbContext context)
    {
        var profiles = new[]
        {
            new Profile
            {
                FullName = "Олександр Коваленко (КНУ Ideal)",
                Email = "student.knu@knu.ua",
                TenantId = "KNU",
                EnableDetailedAnalytics = true,
                IsCrossUniversity = false,
                AvatarUrl = "https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?auto=format&fit=crop&w=200",
                Budget = 9000,
                Cleanliness = 4,
                SleepSchedule = 4,
                PartyTolerance = 2,
                IsSmoker = false,
                OwnPets = false,
                PetTolerance = PetPolicy.Allowed
            },
            new Profile
            {
                FullName = "Ігор Сидоренко (КПІ)",
                Email = "student.kpi@kpi.ua",
                TenantId = "KPI",
                EnableDetailedAnalytics = false,
                IsCrossUniversity = false,
                AvatarUrl = "https://images.unsplash.com/photo-1570295999919-56ceb5ecca61?auto=format&fit=crop&w=200",
                Budget = 12000,
                Cleanliness = 3,
                SleepSchedule = 2,
                PartyTolerance = 4,
                IsSmoker = true,
                OwnPets = false,
                PetTolerance = PetPolicy.Allowed
            },
            new Profile
            {
                FullName = "Марія Бойко (Cross-University Open)",
                Email = "cross.student@knu.ua",
                TenantId = "KNU",
                EnableDetailedAnalytics = true,
                IsCrossUniversity = true,
                AvatarUrl = "https://images.unsplash.com/photo-1494790108377-be9c29b29330?auto=format&fit=crop&w=200",
                Budget = 10000,
                Cleanliness = 4,
                SleepSchedule = 3,
                PartyTolerance = 3,
                IsSmoker = false,
                OwnPets = false,
                PetTolerance = PetPolicy.Allowed
            },
            new Profile
            {
                FullName = "Максим Дмитренко (Вступник / Low Budget)",
                Email = "freshman@example.ua",
                TenantId = null,
                EnableDetailedAnalytics = false,
                IsCrossUniversity = false,
                AvatarUrl = "https://images.unsplash.com/photo-1522075469751-3a6694fb2f61?auto=format&fit=crop&w=200",
                Budget = 5000,
                Cleanliness = 1,
                SleepSchedule = 1,
                PartyTolerance = 5,
                IsSmoker = true,
                OwnPets = false,
                PetTolerance = PetPolicy.NotAllowed
            },
            new Profile
            {
                FullName = "Анна Ткаченко (З котом)",
                Email = "pet.owner@knu.ua",
                TenantId = "KNU",
                EnableDetailedAnalytics = true,
                IsCrossUniversity = false,
                AvatarUrl = "https://images.unsplash.com/photo-1544005313-94ddf0286df2?auto=format&fit=crop&w=200",
                Budget = 10000,
                Cleanliness = 4,
                SleepSchedule = 4,
                PartyTolerance = 2,
                IsSmoker = false,
                OwnPets = true,
                PetTolerance = PetPolicy.Allowed
            },
            new Profile
            {
                FullName = "Власник житла НаУКМА",
                Email = "landlord.naukma@example.ua",
                TenantId = "NaUKMA",
                EnableDetailedAnalytics = false,
                IsCrossUniversity = false,
                AvatarUrl = "https://images.unsplash.com/photo-1500648767791-00dcc994a43e?auto=format&fit=crop&w=200",
                Budget = 0,
                Cleanliness = 3,
                SleepSchedule = 3,
                PartyTolerance = 3,
                IsSmoker = false,
                OwnPets = false,
                PetTolerance = PetPolicy.Allowed
            },
            new Profile
            {
                FullName = "Власник загального житла",
                Email = "landlord.general@example.ua",
                TenantId = null,
                EnableDetailedAnalytics = false,
                IsCrossUniversity = true,
                AvatarUrl = "https://images.unsplash.com/photo-1506794778202-cad84cf45f1d?auto=format&fit=crop&w=200",
                Budget = 0,
                Cleanliness = 3,
                SleepSchedule = 3,
                PartyTolerance = 3,
                IsSmoker = false,
                OwnPets = false,
                PetTolerance = PetPolicy.Allowed
            }
        };

        var result = new Dictionary<string, Profile>(StringComparer.OrdinalIgnoreCase);
        foreach (var seed in profiles)
        {
            var profile = context.Profiles.SingleOrDefault(item => item.Email == seed.Email);
            if (profile is null)
            {
                seed.Id = Guid.NewGuid();
                profile = seed;
                context.Profiles.Add(profile);
            }
            else
            {
                // Keep the named demo accounts useful for deterministic scenarios,
                // while retaining credentials and any unrelated profile data.
                profile.FullName = seed.FullName;
                profile.TenantId = seed.TenantId;
                profile.EnableDetailedAnalytics = seed.EnableDetailedAnalytics;
                profile.IsCrossUniversity = seed.IsCrossUniversity;
                profile.AvatarUrl = seed.AvatarUrl;
                profile.Budget = seed.Budget;
                profile.Cleanliness = seed.Cleanliness;
                profile.SleepSchedule = seed.SleepSchedule;
                profile.PartyTolerance = seed.PartyTolerance;
                profile.IsSmoker = seed.IsSmoker;
                profile.OwnPets = seed.OwnPets;
                profile.PetTolerance = seed.PetTolerance;
            }
            result.Add(profile.Email, profile);
        }

        context.SaveChanges();
        return result;
    }

    private static void SeedProfileTags(
        RoomMatesDbContext context,
        IReadOnlyDictionary<string, Profile> profiles,
        IReadOnlyDictionary<string, Tag> tags)
    {
        AddProfileTags(context, profiles["student.knu@knu.ua"], tags["Quiet Environment"], tags["IT Student"]);
        AddProfileTags(context, profiles["student.kpi@kpi.ua"], tags["Sports"]);
        AddProfileTags(context, profiles["cross.student@knu.ua"], tags["Quiet Environment"], tags["Early Bird"]);
        AddProfileTags(context, profiles["freshman@example.ua"], tags["IT Student"]);
        AddProfileTags(context, profiles["pet.owner@knu.ua"], tags["Quiet Environment"], tags["IT Student"], tags["Pet Lover"]);
        context.SaveChanges();
    }

    private static void AddProfileTags(RoomMatesDbContext context, Profile profile, params Tag[] tags)
    {
        foreach (var tag in tags)
        {
            var exists = context.Set<ProfileTag>().Any(item => item.ProfileId == profile.Id && item.TagId == tag.Id);
            if (!exists)
                context.Set<ProfileTag>().Add(new ProfileTag { ProfileId = profile.Id, TagId = tag.Id });
        }
    }

    private static void SeedHousings(RoomMatesDbContext context, IReadOnlyDictionary<string, Profile> profiles)
    {
        var housings = new[]
        {
            NewHousing("Світла кімната біля ВДНГ / КНУ", "KNU", 7500, false, PetPolicy.Allowed,
                "вул. Ломоносова", "55", "Голосіївський", 50.3845, 30.4851, 4, 4, 2),
            NewHousing("2-кімнатна квартира на Подолі для студентів КПІ", "KPI", 11000, true, PetPolicy.Conditional,
                "вул. Спаська", "12", "Подільський", 50.4655, 30.5180, 3, 2, 4),
            NewHousing("Затишна студія біля НаУКМА (Без тварин)", "NaUKMA", 9500, false, PetPolicy.NotAllowed,
                "вул. Іллінська", "8", "Подільський", 50.4670, 30.5220, 5, 5, 1),
            NewHousing("Загальна квартира для студентів, центр Києва", null, 8500, false, PetPolicy.Conditional,
                "вул. Хрещатик", "10", "Шевченківський", 50.4501, 30.5234, 3, 3, 3),
            NewHousing("Кімната для перевірки заборони куріння", "KNU", 4500, false, PetPolicy.Allowed,
                "вул. Васильківська", "21", "Голосіївський", 50.3940, 30.5070, 1, 1, 5),
            NewHousing("Квартира за домовленістю щодо тварин", "KNU", 8500, true, PetPolicy.Conditional,
                "вул. Велика Васильківська", "42", "Печерський", 50.4250, 30.5200, 4, 4, 2)
        };

        var landlordEmailsByHousing = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Світла кімната біля ВДНГ / КНУ"] = "student.knu@knu.ua",
            ["2-кімнатна квартира на Подолі для студентів КПІ"] = "student.kpi@kpi.ua",
            ["Затишна студія біля НаУКМА (Без тварин)"] = "landlord.naukma@example.ua",
            ["Загальна квартира для студентів, центр Києва"] = "landlord.general@example.ua",
            ["Кімната для перевірки заборони куріння"] = "cross.student@knu.ua",
            ["Квартира за домовленістю щодо тварин"] = "pet.owner@knu.ua"
        };

        foreach (var seed in housings)
        {
            var landlord = profiles[landlordEmailsByHousing[seed.Title]];
            var housing = context.Housings.SingleOrDefault(item => item.Title == seed.Title);
            if (housing is null)
            {
                seed.Id = Guid.NewGuid();
                seed.LandlordId = landlord.Id;
                seed.Landlord = landlord;
                context.Housings.Add(seed);
            }
            else if (housing.LandlordId != landlord.Id)
            {
                // Keep demo listings assigned to their scenario-specific owners.
                housing.LandlordId = landlord.Id;
                housing.Landlord = landlord;
            }
        }

        context.SaveChanges();
    }

    private static Housing NewHousing(
        string title,
        string? tenantId,
        decimal price,
        bool allowsSmoking,
        PetPolicy petPolicy,
        string street,
        string buildingNumber,
        string district,
        double latitude,
        double longitude,
        int cleanliness,
        int sleepSchedule,
        int partyTolerance) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        TenantId = tenantId,
        City = "Київ",
        Street = street,
        BuildingNumber = buildingNumber,
        District = district,
        Latitude = latitude,
        Longitude = longitude,
        PricePerMonth = price,
        AllowsSmoking = allowsSmoking,
        PetPolicy = petPolicy,
        RequiredCleanliness = cleanliness,
        RequiredSleepSchedule = sleepSchedule,
        RequiredPartyTolerance = partyTolerance
    };

    private static void SeedHousingTags(RoomMatesDbContext context, IReadOnlyDictionary<string, Tag> tags)
    {
        var tagNamesByHousing = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Світла кімната біля ВДНГ / КНУ"] = ["Quiet Environment", "IT Student"],
            ["2-кімнатна квартира на Подолі для студентів КПІ"] = ["Sports"],
            ["Затишна студія біля НаУКМА (Без тварин)"] = ["Quiet Environment"],
            ["Загальна квартира для студентів, центр Києва"] = ["Quiet Environment", "IT Student"],
            ["Кімната для перевірки заборони куріння"] = ["IT Student"],
            ["Квартира за домовленістю щодо тварин"] = ["Quiet Environment", "IT Student", "Pet Lover"]
        };

        foreach (var (title, tagNames) in tagNamesByHousing)
        {
            var housing = context.Housings.Single(item => item.Title == title);
            foreach (var tagName in tagNames)
            {
                var tag = tags[tagName];
                var exists = context.Set<HousingTag>().Any(item => item.HousingId == housing.Id && item.TagId == tag.Id);
                if (!exists)
                    context.Set<HousingTag>().Add(new HousingTag { HousingId = housing.Id, TagId = tag.Id });
            }
        }

        context.SaveChanges();
    }
}
