using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.HttpOverrides;

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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
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



app.MapGet("/api/products", () =>
{
    var products = new[]
    {
        new Product(1, "Base Maybelline", "base-maybelline"),
        new Product(2, "Brillo Atenea", "brillo-atenea"),
    };
    return Results.Ok(products);
});

app.MapGet("/api/products/{slug}", Results<Ok<Product>, NotFound> (string slug) =>
{
    var products = new[]
    {
        new Product(1, "Base Maybelline", "base-maybelline"),
        new Product(2, "Brillo Atenea", "brillo-atenea"),
    };

    var normalizedSlug = slug.ToLowerInvariant();
    
    var product = products.FirstOrDefault(
        p => p.Slug == normalizedSlug
    );
    
    if (product is null)
    {
        return TypedResults.NotFound();
    }

    return TypedResults.Ok(product);
});


app.Run();

record Product(int Id, string Name, string Slug);

