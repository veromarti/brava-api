using System.Security.Claims;
using System.Text;
using Brava.Application;
using Brava.Domain.Admins;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Brava.Api.Modules.Auth;

public static class AuthEndpoints
{
    // Single longer-lived token, no refresh pair (decided earlier). 12 hours —
    // covers a workday for the 3 admins without a silent background refresh
    // flow that doesn't exist yet.
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromHours(12);

    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", Login);
        return app;
    }

    // Written directly under time pressure (see conversation) rather than by
    // Vero — flagged for her review since auth is normally a category she
    // writes herself.
    private static async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult>> Login(
        LoginRequest request, IBravaDbContext db, IPasswordHasher<Admin> hasher, IConfiguration configuration)
    {
        // Emails are stored lowercase by convention (same reasoning as
        // ADR-0002's slug lookups); normalize the incoming value the same way.
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var admin = await db.Admins.FirstOrDefaultAsync(a => a.Email == normalizedEmail);

        // Same response whether the email doesn't exist or the account is
        // deactivated — don't let the response shape confirm which admin
        // emails exist.
        if (admin is null || !admin.IsActive)
        {
            return TypedResults.Unauthorized();
        }

        var verification = hasher.VerifyHashedPassword(admin, admin.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return TypedResults.Unauthorized();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            admin.PasswordHash = hasher.HashPassword(admin, request.Password);
            admin.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        var signingKey = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Configuration 'Jwt:SigningKey' is not set.");

        var expiresAtUtc = DateTime.UtcNow.Add(TokenLifetime);
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, admin.Id.ToString()),
                new(ClaimTypes.Email, admin.Email),
                new(ClaimTypes.Role, admin.Role.ToString()),
            }),
            Expires = expiresAtUtc,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
                SecurityAlgorithms.HmacSha256Signature),
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return TypedResults.Ok(new LoginResponse(token, expiresAtUtc));
    }
}
