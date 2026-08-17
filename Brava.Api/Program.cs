using Brava.Api.Modules.Auth;
using Brava.Api.Modules.Brands;
using Brava.Api.Modules.Products;
using Brava.Application;
using Brava.Domain.Admins;
using Brava.Infrastructure.Persistence;
using Brava.Infrastructure.Persistence.Seeding;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Railway assigns the listen port at runtime via PORT; bind to it explicitly.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://+:{port}");
}

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("BravaDb")
    ?? throw new InvalidOperationException("Connection string 'BravaDb' is not configured.");
builder.Services.AddDbContext<BravaDbContext>(options =>
    options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());
// Application depends on IBravaDbContext, not the concrete EF Core type —
// this is what keeps the dependency arrow pointing the right way.
builder.Services.AddScoped<IBravaDbContext>(sp => sp.GetRequiredService<BravaDbContext>());
builder.Services.AddScoped<IPasswordHasher<Admin>, PasswordHasher<Admin>>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // ADR-0006: seed from /data/*.csv in Development. Idempotent, keyed on slug.
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<BravaDbContext>();
        // /data lives at the repo root, one level up from this project now that
        // Brava.Api is one of several sibling projects.
        var dataDirectory = Path.Combine(app.Environment.ContentRootPath, "..", "data");
        await CatalogCsvSeeder.SeedAsync(db, dataDirectory);
    }
}

// Railway terminates TLS at its edge and proxies plain HTTP to the container.
// Without trusting X-Forwarded-Proto, UseHttpsRedirection() below would redirect
// every request, forever (the app never sees a request it believes is already HTTPS).
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
// Railway's edge proxy isn't in a known/loopback range, so the default trust
// list would ignore its headers. Only Railway's private network can reach this
// container, so trusting any proxy here is safe.
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

app.UseHttpsRedirection();

app.MapProductEndpoints();
app.MapBrandEndpoints();
app.MapAuthEndpoints();

app.Run();



