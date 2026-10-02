namespace RoomMates.Models;

public class PagedResult<T>
{
    public int TotalCount { get; init; }
    public int Skip { get; init; }
    public int Limit { get; init; }
    public string? NextLink { get; init; }
    public IEnumerable<T> Items { get; init; } = [];
}
