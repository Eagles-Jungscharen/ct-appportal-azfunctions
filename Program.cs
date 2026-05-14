using EaglesJungscharen.Azure.AppPortal.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// In-Memory-Cache für MeDto und andere kurzlebige Daten
builder.Services.AddMemoryCache();

// JWT Bearer Authentifizierung via OIDC Authority (Churchtool IDP)
var oidcAuthority = builder.Configuration["OIDC_AUTHORITY"]
    ?? throw new InvalidOperationException("Konfigurationsschlüssel 'OIDC_AUTHORITY' fehlt.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = oidcAuthority;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
        };
    });

builder.Services.AddAuthorization();

// HTTP-Client für das Churchtool IDP Backend
var idpBaseUrl = builder.Configuration["CHURCHTOOL_IDP_BASE_URL"];
var idpFunctionKey = builder.Configuration["CHURCHTOOL_IDP_FUNCTION_KEY"];

if (!string.IsNullOrWhiteSpace(idpBaseUrl))
{
    builder.Services.AddHttpClient("ChurchtoolIdp", client =>
    {
        client.BaseAddress = new Uri(idpBaseUrl);
        if (!string.IsNullOrWhiteSpace(idpFunctionKey))
        {
            client.DefaultRequestHeaders.Add("x-functions-key", idpFunctionKey);
        }
    });
}

// JWT-Validierungs-Middleware in der Functions-Worker-Pipeline registrieren
builder.UseMiddleware<JwtValidationMiddleware>();

builder.Build().Run();
