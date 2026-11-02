using RoomMates.Models;

namespace RoomMates.Services;

public interface ITelegramNotificationService
{
    Task<bool> SendBookingNotificationAsync(
        BookingRequest bookingRequest,
        string studentName,
        string housingTitle,
        CancellationToken cancellationToken = default);

    Task<bool> SendLandlordApplicationNotificationAsync(
        Profile profile,
        CancellationToken cancellationToken = default);
}
