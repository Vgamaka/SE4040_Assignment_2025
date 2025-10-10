# Backend API (apps/backend)

Place the ASP.NET Core Web API solution here.

## Dev
dotnet restore
dotnet run

## IIS Publish (later)
dotnet publish -c Release -o ../publish

## appsettings.Development.json (example)
{
  ""Mongo"": { ""ConnectionString"": ""mongodb://localhost:27017"", ""Database"": ""evcharge"" },
  ""Jwt"": { ""Secret"": ""dev-secret"", ""Issuer"": ""evcharge-api"", ""Audience"": ""evcharge-clients"", ""ExpiryMinutes"": 60 },
  ""Cors"": { ""AllowedOrigins"": [ ""http://localhost:5173"" ] }
}
