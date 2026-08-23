using System.Text;
using Brava.Api.Modules.Auth;
using Brava.Api.Modules.Brands;
using Brava.Api.Modules.Categories;
using Brava.Api.Modules.Products;
using Brava.Api.Modules.Products.Images;
using Brava.Api.Modules.Products.Variants;
using Brava.Application;
using Brava.Domain.Admins;
using Brava.Infrastructure.Persistence;
using Brava.Infrastructure.Persistence.Seeding;
using Brava.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Secrets (JWT signing key, R2 credentials) go here instead of
// appsettings.{Environment}.json, which is committed to git. This file is
// covered by the .gitignore pattern "appsettings.*.local.json" — create it
// locally, it's never pushed. In Production, the equivalent secrets come
// from Railway environment variables instead (same pattern as
// ConnectionStrings already uses), so this file is Development-only.
builder.Configuration.AddJsonFile(
    $"appsettings.{builder.Environment.EnvironmentName}.local.json", optional: true, reloadOnChange: true);

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
// Config is validated lazily inside the client (see CloudflareR2ImageStorage) so
// the app can still start before R2 is set up — only image endpoints fail.
builder.Services.AddSingleton<IImageStorage, CloudflareR2ImageStorage>();

// Same signing key AuthEndpoints.Login uses to issue tokens — read once here
// so a misconfigured deployment fails at startup, not on the first login.
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"]
    ?? throw new InvalidOperationException("Configuration 'Jwt:SigningKey' is not set.");
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
        };
    });
builder.Services.AddAuthorization();

// The admin UI (brava-webpage) calls this API directly from the browser for
// writes (create/edit/deactivate, image upload), so those requests need CORS,
// not just the JWT check. "Cors:AllowedOrigins" isn't set in appsettings —
// add it (a Railway env var in Production, appsettings.Development.local.json
// locally) once the frontend has a real deployed URL; localhost:3000 covers
// local dev today.
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:3000"];
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod()));

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

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapProductEndpoints();
app.MapVariantEndpoints();
app.MapImageEndpoints();
app.MapBrandEndpoints();
app.MapCategoryEndpoints();
app.MapAuthEndpoints();

app.Run();



