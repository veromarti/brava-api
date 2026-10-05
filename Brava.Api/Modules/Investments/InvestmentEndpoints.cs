using Brava.Application;
using Brava.Domain.Investments;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;

namespace Brava.Api.Modules.Investments;

public static class InvestmentEndpoints
{
    private const int MaxDescriptionLength = 200;

    public static IEndpointRouteBuilder MapInvestmentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/investments", GetInvestments).RequireAuthorization();
        app.MapPost("/api/investments", CreateInvestment).RequireAuthorization();
        app.MapDelete("/api/investments/{id:guid}", DeleteInvestment).RequireAuthorization();
        return app;
    }

    // `from`/`to` filter on Date (the day the cost was incurred), same
    // inclusive-day-range convention as GetOrderMetrics so the two agree
    // when Métricas shows them side by side for the same period.
    private static async Task<Results<Ok<List<InvestmentDto>>, BadRequest<string>>> GetInvestments(
        IBravaDbContext db, DateTime? from, DateTime? to)
    {
        var fromUtc = from is null ? (DateTime?)null : DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Utc);
        var toUtc = to is null ? (DateTime?)null : DateTime.SpecifyKind(to.Value.Date, DateTimeKind.Utc);
        if (fromUtc is not null && toUtc is not null && fromUtc > toUtc)
        {
            return TypedResults.BadRequest("'from' no puede ser posterior a 'to'.");
        }

        var query = db.Investments.AsQueryable();
        if (fromUtc is not null)
        {
            query = query.Where(i => i.Date >= fromUtc.Value);
        }
        if (toUtc is not null)
        {
            query = query.Where(i => i.Date < toUtc.Value.AddDays(1));
        }

        var investments = await query
            .OrderByDescending(i => i.Date)
            .ThenByDescending(i => i.CreatedAt)
            .Select(i => new InvestmentDto(i.Id, i.Description, i.Amount, i.Date))
            .ToListAsync();

        return TypedResults.Ok(investments);
    }

    private static async Task<Results<Created<InvestmentDto>, BadRequest<string>>> CreateInvestment(
        CreateInvestmentRequest request, IBravaDbContext db)
    {
        var description = request.Description.Trim();
        if (description.Length == 0)
        {
            return TypedResults.BadRequest("La descripción es obligatoria.");
        }
        if (description.Length > MaxDescriptionLength)
        {
            return TypedResults.BadRequest($"La descripción no puede superar {MaxDescriptionLength} caracteres.");
        }
        if (request.Amount <= 0)
        {
            return TypedResults.BadRequest("El monto debe ser mayor a 0.");
        }

        var now = DateTime.UtcNow;
        var investment = new Investment
        {
            Description = description,
            Amount = request.Amount,
            Date = DateTime.SpecifyKind(request.Date.Date, DateTimeKind.Utc),
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Investments.Add(investment);
        await db.SaveChangesAsync();

        return TypedResults.Created(
            $"/api/investments/{investment.Id}",
            new InvestmentDto(investment.Id, investment.Description, investment.Amount, investment.Date));
    }

    private static async Task<Results<NoContent, NotFound<string>>> DeleteInvestment(Guid id, IBravaDbContext db)
    {
        var investment = await db.Investments.FirstOrDefaultAsync(i => i.Id == id);
        if (investment is null)
        {
            return TypedResults.NotFound($"Inversión '{id}' no encontrada.");
        }

        db.Investments.Remove(investment);
        await db.SaveChangesAsync();
        return TypedResults.NoContent();
    }
}
