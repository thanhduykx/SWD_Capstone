namespace CPMS.API.Contracts.Accounts;

public sealed record LoginRequest(string? Username, string Password);
public sealed record GoogleLoginRequest(string IdToken);
public sealed record RefreshRequest(string RefreshToken);
public sealed record TokenResponse(string AccessToken, string RefreshToken, DateTime RefreshTokenExpiresAt);
