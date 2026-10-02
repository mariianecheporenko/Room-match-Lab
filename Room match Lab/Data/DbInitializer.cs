using RoomMates.Data;
using RoomMates.Models;

namespace Room_match_Lab.Data;

public static class DbInitializer
{
    public static void Seed(RoomMatesDbContext context)
    {
        if (context.Housings.Any())
        {
            return;
        }

        var tagQuiet = new Tag { Id = Guid.NewGuid(), Name = "Quiet Environment" };
        var tagIT = new Tag { Id = Guid.NewGuid(), Name = "IT Student" };
        var tagSports = new Tag { Id = Guid.NewGuid(), Name = "Sports" };
        var tagEarlyBird = new Tag { Id = Guid.NewGuid(), Name = "Early Bird" };
        context.Tags.AddRange(tagQuiet, tagIT, tagSports, tagEarlyBird);

        var critGuests = new LifestyleCriterion { Id = Guid.NewGuid(), Name = "Guests Frequency", Description = "How often friends visit" };
        var critCooking = new LifestyleCriterion { Id = Guid.NewGuid(), Name = "Cooking Frequency", Description = "Cooks at home daily" };
        context.LifestyleCriteria.AddRange(critGuests, critCooking);

        // Housings
        var housing1 = new Housing
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
            RequiredPartyTolerance = 2
        };

        var housing2 = new Housing
        {
            Id = Guid.NewGuid(),
            Title = "2-кімнатна квартира на Подолі для студентів",
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
            RequiredPartyTolerance = 4
        };

        context.Housings.AddRange(housing1, housing2);

        // Students
        var profileIdeal = new Profile
        {
            Id = Guid.NewGuid(),
            FullName = "Олександр Коваленко",
            Email = "o.kovalenko@knu.ua",
            AvatarUrl = "https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?auto=format&fit=crop&w=200",
            Budget = 9000,
            Cleanliness = 4,
            SleepSchedule = 4,
            PartyTolerance = 2,
            IsSmoker = false,
            OwnPets = true,
            PetTolerance = PetPolicy.Allowed
        };

        var profileOpposite = new Profile
        {
            Id = Guid.NewGuid(),
            FullName = "Максим Дмитренко",
            Email = "m.dmytrenko@knu.ua",
            AvatarUrl = "https://images.unsplash.com/photo-1570295999919-56ceb5ecca61?auto=format&fit=crop&w=200",
            Budget = 6000, 
            Cleanliness = 1,
            SleepSchedule = 1,
            PartyTolerance = 5,
            IsSmoker = true,
            OwnPets = false,
            PetTolerance = PetPolicy.NotAllowed
        };

        context.Profiles.AddRange(profileIdeal, profileOpposite);
        context.SaveChanges();
    }
}