using Brava.Application;
using Brava.Domain.Admins;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Brava.Api.Modules.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/auth/login", Login);
        return app;
    }

    // Decided: single longer-lived token (no refresh token pair) — see the
    // conversation this was scaffolded in. Still yours to write:
    //
    //   1. Look up the Admin by request.Email (db.Admins.FirstOrDefaultAsync).
    //      If not found OR IsActive is false, return Unauthorized — don't
    //      reveal which case it was (that tells an attacker whether an email
    //      exists at all).
    //   2. Verify the password: hasher.VerifyHashedPassword(admin,
    //      admin.PasswordHash, request.Password). Handle all three results —
    //      Failed (Unauthorized), Success, and SuccessRehashNeeded (still log
    //      the admin in, but also re-hash and save the new hash — the point
    //      of that case is upgrading old hashes transparently on next login).
    //   3. Build the token. This is the actual "how do I create a JWT in
    //      .NET" piece — look at Microsoft.IdentityModel.JsonWebTokens'
    //      JsonWebTokenHandler and SecurityTokenDescriptor. Claims should
    //      include at least the admin's Id and Role (AdminRole) — Role is
    //      what a future [Authorize(...)] check on write endpoints reads.
    //   4. Where does the signing key come from? It must NOT be hardcoded —
    //      read it from configuration (appsettings.Development.json locally,
    //      a Railway env var in production), same pattern as ConnectionStrings
    //      already uses. You'll need to add that config key yourself and
    //      decide the token's expiry (you chose "single longer-lived token" —
    //      pick the actual number of hours).
    private static async Task<Results<Ok<LoginResponse>, UnauthorizedHttpResult>> Login(
        LoginRequest request, IBravaDbContext db, IPasswordHasher<Admin> hasher)
    {
        throw new NotImplementedException();
    }
}
