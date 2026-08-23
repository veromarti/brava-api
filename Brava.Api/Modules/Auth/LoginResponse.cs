namespace Brava.Api.Modules.Auth;

public record LoginResponse(string Token, DateTime ExpiresAtUtc);
