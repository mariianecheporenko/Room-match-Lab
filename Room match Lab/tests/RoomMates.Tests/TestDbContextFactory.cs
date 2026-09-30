using Microsoft.EntityFrameworkCore;
using RoomMates.Data;

namespace RoomMates.Tests;

internal static class TestDbContextFactory
{
    public static RoomMatesDbContext CreateInMemory()
    {
        var options = new DbContextOptionsBuilder<RoomMatesDbContext>()
            .UseInMemoryDatabase($"RoomMatesTests-{Guid.NewGuid()}")
            .Options;

        return new RoomMatesDbContext(options);
    }
}
