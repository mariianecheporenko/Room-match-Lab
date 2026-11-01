using System;
using System.Collections.Generic;
using System.Linq;
using RoomMates.Data;
using RoomMates.Models;

namespace Room_match_Lab.Data;

public static class DbInitializer
{
    public static void Seed(RoomMatesDbContext context)
    {
        // 1. Теги (Soft Traits / Інтереси)
        var tagQuiet = new Tag { Id = Guid.NewGuid(), Name = "Quiet Environment" };
        var tagIT = new Tag { Id = Guid.NewGuid(), Name = "IT Student" };
        var tagSports = new Tag { Id = Guid.NewGuid(), Name = "Sports" };
        var tagEarlyBird = new Tag { Id = Guid.NewGuid(), Name = "Early Bird" };
        var tagPetLover = new Tag { Id = Guid.NewGuid(), Name = "Pet Lover" };

        if (!context.Tags.Any())
        {
            context.Tags.AddRange(tagQuiet, tagIT, tagSports, tagEarlyBird, tagPetLover);
        }

        // 2. Кастомні критерії шкал (Dynamic Criteria)
        var critGuests = new LifestyleCriterion { Id = Guid.NewGuid(), Name = "Guests Frequency", Description = "How often friends visit" };
        var critCooking = new LifestyleCriterion { Id = Guid.NewGuid(), Name = "Cooking Frequency", Description = "Cooks at home daily" };

        if (!context.LifestyleCriteria.Any())
        {
            context.LifestyleCriteria.AddRange(critGuests, critCooking);
        }

        // 3. Тестові варіанти житла (Покривають різні тенанти, ціни, куріння та правила щодо тварин)

        // КНУ: Не дозволено куріння, Тварини дозволені, Середня ціна
        var housingKnuAllowedPets = new Housing
        {
            Id = Guid.NewGuid(),
            Title = "Світла кімната біля ВДНГ / КНУ",
            PricePerMonth = 7500,
            City = "Київ",
            Street = "вул. Ломоносова",
            BuildingNumber = "55",
            District = "Голосіївський",
            Latitude = 50.3845,
            Longitude = 30.4851,
            AllowsSmoking = false,
            PetPolicy = PetPolicy.Allowed,
            RequiredCleanliness = 4,
            RequiredSleepSchedule = 4,
            RequiredPartyTolerance = 2,
            TenantId = "KNU"
        };

        // КПІ: Дозволено куріння, Тварини за домовленістю (Conditional), Висока ціна
        var housingKpiConditionalPets = new Housing
        {
            Id = Guid.NewGuid(),
            Title = "2-кімнатна квартира на Подолі для студентів КПІ",
            PricePerMonth = 11000,
            City = "Київ",
            Street = "вул. Спаська",
            BuildingNumber = "12",
            District = "Подільський",
            Latitude = 50.4655,
            Longitude = 30.5180,
            AllowsSmoking = true,
            PetPolicy = PetPolicy.Conditional,
            RequiredCleanliness = 3,
            RequiredSleepSchedule = 2,
            RequiredPartyTolerance = 4,
            TenantId = "KPI"
        };

        // НаУКМА: Не дозволено куріння, Тварини СУВОРО ЗАБОРОНЕНІ (Dealbreaker testing)
        var housingNaukmaNoPets = new Housing
        {
            Id = Guid.NewGuid(),
            Title = "Затишна студія біля НаУКМА (Без тварин)",
            PricePerMonth = 9500,
            City = "Київ",
            Street = "вул. Іллінська",
            BuildingNumber = "8",
            District = "Подільський",
            Latitude = 50.4670,
            Longitude = 30.5220,
            AllowsSmoking = false,
            PetPolicy = PetPolicy.NotAllowed,
            RequiredCleanliness = 5,
            RequiredSleepSchedule = 5,
            RequiredPartyTolerance = 1,
            TenantId = "NaUKMA"
        };

        // Загальне житло (TenantId = null): Доступне абсолютно усім
        var housingGeneral = new Housing
        {
            Id = Guid.NewGuid(),
            Title = "Загальна квартира для студентів, центр Києва",
            PricePerMonth = 8500,
            City = "Київ",
            Street = "вул. Хрещатик",
            BuildingNumber = "10",
            District = "Шевченківський",
            Latitude = 50.4501,
            Longitude = 30.5234,
            AllowsSmoking = false,
            PetPolicy = PetPolicy.Conditional,
            RequiredCleanliness = 3,
            RequiredSleepSchedule = 3,
            RequiredPartyTolerance = 3,
            TenantId = null
        };

        if (!context.Housings.Any(h => h.Title == housingKnuAllowedPets.Title))
            context.Housings.Add(housingKnuAllowedPets);

        if (!context.Housings.Any(h => h.Title == housingKpiConditionalPets.Title))
            context.Housings.Add(housingKpiConditionalPets);

        if (!context.Housings.Any(h => h.Title == housingNaukmaNoPets.Title))
            context.Housings.Add(housingNaukmaNoPets);

        if (!context.Housings.Any(h => h.Title == housingGeneral.Title))
            context.Housings.Add(housingGeneral);

        // 4. Профілі студентів (Покривають усі комбінації токенів, прав та dealbreaker-сценаріїв)

        // Сценарій 1: Ідеальний студент КНУ (KNU, Analytics Enabled, Pets Allowed, Budget OK)
        var profileIdealKnu = new Profile
        {
            Id = Guid.NewGuid(),
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
            OwnPets = true,
            PetTolerance = PetPolicy.Allowed
        };

        // Сценарій 2: Студент КПІ (KPI, Cross-University Disabled, Analytics Disabled)
        var profileKpi = new Profile
        {
            Id = Guid.NewGuid(),
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
        };

        // Сценарій 3: Студент КНУ з увімкненим IsCrossUniversity = true (Бачить і КНУ, і КПІ, і загальні)
        var profileCrossUni = new Profile
        {
            Id = Guid.NewGuid(),
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
        };

        // Сценарій 4: Першокурсник без універу (TenantId = null, Низький бюджет — Dealbreaker test)
        var profileFreshmanLowBudget = new Profile
        {
            Id = Guid.NewGuid(),
            FullName = "Максим Дмитренко (Вступник / Low Budget)",
            Email = "freshman@example.ua",
            TenantId = null,
            EnableDetailedAnalytics = false,
            IsCrossUniversity = false,
            AvatarUrl = "https://images.unsplash.com/photo-1522075469751-3a6694fb2f61?auto=format&fit=crop&w=200",
            Budget = 5000, // Нижче вартості будь-якої квартири -> MatchScore 0.0%
            Cleanliness = 1,
            SleepSchedule = 1,
            PartyTolerance = 5,
            IsSmoker = true,
            OwnPets = false,
            PetTolerance = PetPolicy.NotAllowed
        };

        // Сценарій 5: Власник тварини (Тест на Dealbreaker із житлом NoPets та штрафу Conditional)
        var profilePetOwner = new Profile
        {
            Id = Guid.NewGuid(),
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
        };

        // Додавання або оновлення профілів
        SeedOrUpdateProfile(context, profileIdealKnu);
        SeedOrUpdateProfile(context, profileKpi);
        SeedOrUpdateProfile(context, profileCrossUni);
        SeedOrUpdateProfile(context, profileFreshmanLowBudget);
        SeedOrUpdateProfile(context, profilePetOwner);

        context.SaveChanges();
    }

    private static void SeedOrUpdateProfile(RoomMatesDbContext context, Profile seededProfile)
    {
        var existing = context.Profiles.FirstOrDefault(p => p.Email == seededProfile.Email);
        if (existing is null)
        {
            context.Profiles.Add(seededProfile);
        }
        else
        {
            existing.FullName = seededProfile.FullName;
            existing.TenantId = seededProfile.TenantId;
            existing.EnableDetailedAnalytics = seededProfile.EnableDetailedAnalytics;
            existing.IsCrossUniversity = seededProfile.IsCrossUniversity;
            existing.Budget = seededProfile.Budget;
            existing.Cleanliness = seededProfile.Cleanliness;
            existing.SleepSchedule = seededProfile.SleepSchedule;
            existing.PartyTolerance = seededProfile.PartyTolerance;
            existing.IsSmoker = seededProfile.IsSmoker;
            existing.OwnPets = seededProfile.OwnPets;
            existing.PetTolerance = seededProfile.PetTolerance;
        }
    }
}