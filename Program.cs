using EaglesJungscharen.Azure.AppPortal.Middleware;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Kiota.Abstractions.Authentication;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// In-Memory-Cache für MeDto und andere kurzlebige Daten
builder.Services.AddMemoryCache();

// IHttpClientFactory immer registrieren (Named Client "ChurchtoolIdp" wird bedingt hinzugefügt)
builder.Services.AddHttpClient();

// HTTP-Client für das Churchtool IDP Backend
var idpBaseUrl = builder.Configuration["CHURCHTOOL_IDP_BASE_URL"];
var idpFunctionKey = builder.Configuration["CHURCHTOOL_IDP_FUNCTION_KEY"];
var churchtoolUserInfoUrl = builder.Configuration["CHURCHTOOL_USERINFO_URL"];

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
// Token-Validierung erfolgt direkt via JsonWebTokenHandler + OIDC Discovery
builder.UseMiddleware<JwtValidationMiddleware>();
builder.UseMiddleware<ChurchToolReferenceMiddleware>();
builder.Logging.AddFilter("Microsoft.IdentityModel", LogLevel.Debug);

builder.Services.AddScoped<IChurchToolReferenceContext, FunctionChurchToolReferenceContext>();
builder.Services.AddScoped<IUserTokenProvider, AzureTableUserTokenProvider>();
builder.Services.AddScoped<IAuthenticationProvider, ChurchToolsUserAuthenticationProvider>();
builder.Services.AddScoped<ChurchToolsClientFactory>();

builder.Services.AddHttpClient<ChurchToolsClientFactory>(client =>
{
    client.BaseAddress = new Uri($"{churchtoolUserInfoUrl}/api");
});


builder.Build().Run();
