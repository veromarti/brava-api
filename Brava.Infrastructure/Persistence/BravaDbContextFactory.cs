using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Brava.Infrastructure.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> build the context without booting the web host —
/// Brava.Api's Program.cs throws on runtime secrets (Jwt:SigningKey, R2) that
/// migrations don't need. Design-time only; never resolved at runtime.
///
/// <c>migrations add</c> doesn't open a connection, so the fallback string is
/// enough there. For <c>database update</c>, point at the real database with
/// <c>ConnectionStrings__BravaDb</c> in the environment (the fallback already
/// matches appsettings.Development.json for local use).
/// </summary>
public sealed class BravaDbContextFactory : IDesignTimeDbContextFactory<BravaDbContext>
{
    public BravaDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__BravaDb")
            ?? "Host=localhost;Port=5432;Database=brava;Username=brava;Password=brava_dev_only";

        var options = new DbContextOptionsBuilder<BravaDbContext>()
            .UseNpgsql(connectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new BravaDbContext(options);
    }
}
