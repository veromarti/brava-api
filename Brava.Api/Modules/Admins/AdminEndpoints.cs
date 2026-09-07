using Brava.Application;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Brava.Api.Modules.Admins;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/admins", GetAdmins).RequireAuthorization();
        return app;
    }

    // Active admins only — the "¿quién tomó este pedido?" picker on order
    // creation shouldn't offer a deactivated account as an option.
    private static async Task<Ok<List<AdminListItemDto>>> GetAdmins(IBravaDbContext db)
    {
        var admins = await db.Admins
            .Where(a => a.IsActive)
            .OrderBy(a => a.Email)
            .Select(a => new AdminListItemDto(a.Id, a.Email))
            .ToListAsync();

        return TypedResults.Ok(admins);
    }
}
