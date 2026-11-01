using Google.Apis.Auth;

namespace RoomMates.Services;

public interface IGoogleTokenValidator
{
    Task<GoogleJsonWebSignature.Payload> ValidateAsync(string idToken);
}

public sealed class GoogleTokenValidator(IConfiguration configuration) : IGoogleTokenValidator
{
    public Task<GoogleJsonWebSignature.Payload> ValidateAsync(string idToken)
    {
        var clientId = configuration["Authentication:Google:ClientId"]
            ?? configuration["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("Authentication:Google:ClientId is not configured.");

        return GoogleJsonWebSignature.ValidateAsync(
            idToken,
            new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [clientId]
            });
    }
}
