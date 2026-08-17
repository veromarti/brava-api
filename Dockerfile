FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first (not the rest of the source) so this restore layer
# only invalidates when a project's package references actually change.
COPY Brava.Domain/Brava.Domain.csproj Brava.Domain/
COPY Brava.Application/Brava.Application.csproj Brava.Application/
COPY Brava.Infrastructure/Brava.Infrastructure.csproj Brava.Infrastructure/
COPY Brava.Api/Brava.Api.csproj Brava.Api/
RUN dotnet restore Brava.Api/Brava.Api.csproj

COPY . .
RUN dotnet publish Brava.Api/Brava.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "Brava.Api.dll"]
