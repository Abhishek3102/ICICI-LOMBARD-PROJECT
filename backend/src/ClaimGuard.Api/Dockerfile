# Multi-stage Alpine build (~85 MB runtime image)
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY ClaimGuard.sln ./
COPY src/ClaimGuard.Domain/ClaimGuard.Domain.csproj src/ClaimGuard.Domain/
COPY src/ClaimGuard.Application/ClaimGuard.Application.csproj src/ClaimGuard.Application/
COPY src/ClaimGuard.Infrastructure/ClaimGuard.Infrastructure.csproj src/ClaimGuard.Infrastructure/
COPY src/ClaimGuard.Api/ClaimGuard.Api.csproj src/ClaimGuard.Api/
COPY tests/ClaimGuard.UnitTests/ClaimGuard.UnitTests.csproj tests/ClaimGuard.UnitTests/
RUN dotnet restore src/ClaimGuard.Api/ClaimGuard.Api.csproj

COPY src/ src/
RUN dotnet publish src/ClaimGuard.Api/ClaimGuard.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENTRYPOINT ["dotnet", "ClaimGuard.Api.dll"]
