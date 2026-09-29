using Microsoft.EntityFrameworkCore;
using RoomMates.Models;

namespace RoomMates.Data;

public class RoomMatesDbContext(DbContextOptions<RoomMatesDbContext> options) : DbContext(options)
{
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<Housing> Housings => Set<Housing>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<LifestyleCriterion> LifestyleCriteria => Set<LifestyleCriterion>();
    public DbSet<BookingRequest> BookingRequests => Set<BookingRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Profile>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.Budget).HasPrecision(12, 2);
            entity.Property(x => x.PetTolerance).HasConversion<string>();
            entity.ToTable(t => t.HasCheckConstraint("CK_Profiles_Cleanliness", "\"Cleanliness\" BETWEEN 1 AND 5"));
            entity.ToTable(t => t.HasCheckConstraint("CK_Profiles_SleepSchedule", "\"SleepSchedule\" BETWEEN 1 AND 5"));
            entity.ToTable(t => t.HasCheckConstraint("CK_Profiles_PartyTolerance", "\"PartyTolerance\" BETWEEN 1 AND 5"));
        });

        modelBuilder.Entity<Housing>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PricePerMonth).HasPrecision(12, 2);
            entity.Property(x => x.PetPolicy).HasConversion<string>();
            entity.ToTable(t => t.HasCheckConstraint("CK_Housings_RequiredCleanliness", "\"RequiredCleanliness\" BETWEEN 1 AND 5"));
            entity.ToTable(t => t.HasCheckConstraint("CK_Housings_RequiredSleepSchedule", "\"RequiredSleepSchedule\" BETWEEN 1 AND 5"));
            entity.ToTable(t => t.HasCheckConstraint("CK_Housings_RequiredPartyTolerance", "\"RequiredPartyTolerance\" BETWEEN 1 AND 5"));
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(100);
        });

        modelBuilder.Entity<ProfileTag>().HasKey(x => new { x.ProfileId, x.TagId });
        modelBuilder.Entity<ProfileTag>().HasOne(x => x.Profile).WithMany(x => x.ProfileTags).HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ProfileTag>().HasOne(x => x.Tag).WithMany(x => x.ProfileTags).HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<HousingTag>().HasKey(x => new { x.HousingId, x.TagId });
        modelBuilder.Entity<HousingTag>().HasOne(x => x.Housing).WithMany(x => x.HousingTags).HasForeignKey(x => x.HousingId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<HousingTag>().HasOne(x => x.Tag).WithMany(x => x.HousingTags).HasForeignKey(x => x.TagId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<LifestyleCriterion>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.ScaleMin).HasDefaultValue(1);
            entity.Property(x => x.ScaleMax).HasDefaultValue(5);
            entity.ToTable(t => t.HasCheckConstraint("CK_LifestyleCriteria_Scale", "\"ScaleMin\" < \"ScaleMax\""));
        });

        modelBuilder.Entity<ProfileCriterionValue>().HasKey(x => new { x.ProfileId, x.LifestyleCriterionId });
        modelBuilder.Entity<ProfileCriterionValue>().HasOne(x => x.Profile).WithMany(x => x.CustomCriterionValues).HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ProfileCriterionValue>().HasOne(x => x.LifestyleCriterion).WithMany(x => x.ProfileValues).HasForeignKey(x => x.LifestyleCriterionId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<HousingCriterionRequirement>().HasKey(x => new { x.HousingId, x.LifestyleCriterionId });
        modelBuilder.Entity<HousingCriterionRequirement>().HasOne(x => x.Housing).WithMany(x => x.CustomRequirements).HasForeignKey(x => x.HousingId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<HousingCriterionRequirement>().HasOne(x => x.LifestyleCriterion).WithMany(x => x.HousingRequirements).HasForeignKey(x => x.LifestyleCriterionId).OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BookingRequest>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.MatchScore).HasPrecision(5, 2);
            entity.Property(x => x.CreatedAt).HasColumnType("timestamp with time zone");
            entity.HasOne(x => x.Profile).WithMany(x => x.BookingRequests).HasForeignKey(x => x.ProfileId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Housing).WithMany(x => x.BookingRequests).HasForeignKey(x => x.HousingId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    public override int SaveChanges()
    {
        NormalizeUtcDates();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        NormalizeUtcDates();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void NormalizeUtcDates()
    {
        foreach (var entry in ChangeTracker.Entries<BookingRequest>())
        {
            if (entry.State == EntityState.Added && entry.Entity.CreatedAt == default)
                entry.Entity.CreatedAt = DateTime.UtcNow;

            if (entry.Entity.CreatedAt.Kind != DateTimeKind.Utc)
                entry.Entity.CreatedAt = DateTime.SpecifyKind(entry.Entity.CreatedAt, DateTimeKind.Utc);
        }
    }
}
