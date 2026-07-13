using CPMS.BLL.Accounts;
using Google.Apis.Auth;

namespace CPMS.API.Services;

public sealed class GoogleIdentityVerifier(IConfiguration configuration) : IGoogleIdentityVerifier
{
    public async Task<string?> VerifyEmailAsync(string idToken, CancellationToken cancellationToken)
    {
        var clientId = configuration["Authentication:Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
            throw new InvalidOperationException("Google login is not configured.");
        try
        {
            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken,
                new GoogleJsonWebSignature.ValidationSettings { Audience = [clientId] });
            return payload.EmailVerified ? payload.Email : null;
        }
        catch (InvalidJwtException) { return null; }
    }
}
