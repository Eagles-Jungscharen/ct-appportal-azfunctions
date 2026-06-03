using Azure.Storage.Blobs;
using EaglesJungscharen.Azure.ChurchToolIDPServices.Extensions;
using EaglesJungscharen.Azure.ChurchToolIDPServices.Middleware;
using EaglesJungscharen.Azure.AppPortal.Models;
using EaglesJungscharen.Azure.AppPortal.Models.Entities;
using EaglesJungscharen.Azure.AppPortal.Services;
using GuedesPlace.AzureTools.Tables;
using GuedesPlace.AzureTools.Configuration.Extensions;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Configuration.CheckConfigurationValuesAvailable(new[]
{
    "CHURCHTOOL_IDP_BASE_URL",
    "CHURCHTOOL_IDP_FUNCTION_KEY",
    "CHURCHTOOL_URL",
    "OIDC_AUTHORITY_URL",
    "CHURCHTOOL_IDP_STORAGE_CONNECTION_STRING",
    "CHURCHTOOL_ADMIN_GROUP_ID"
});
builder.Services.Configure<PortalConfiguration>(config=>{
    config.ChurchToolAdminGroupId = builder.Configuration["CHURCHTOOL_ADMIN_GROUP_ID"]!;
});

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
builder.Services.AddChurchToolIDPServices(
    churchToolUrl: builder.Configuration["CHURCHTOOL_URL"] ?? throw new InvalidOperationException("CHURCHTOOL_URL is not configured."),
    oidcAuthorityUrl: builder.Configuration["OIDC_AUTHORITY_URL"] ?? throw new InvalidOperationException("OIDC_AUTHORITY_URL is not configured."),
    churchToolIDPStorageConnectionString: builder.Configuration["CHURCHTOOL_IDP_STORAGE_CONNECTION_STRING"] ?? throw new InvalidOperationException("CHURCHTOOL_IDP_STORAGE_CONNECTION_STRING is not configured.")
);
builder.Services.AddScoped<IMeService, MeService>();
builder.Services.AddScoped<IGroupService, GroupService>();
builder.Services.AddScoped<IChurchtoolIdpService, ChurchtoolIdpService>();

// Table Storage für Portal-Tabellen (Apps, AppAssignments)
var portalTableService = new ExtendedAzureTableClientService(
    builder.Configuration["AzureWebJobsStorage"] ?? throw new InvalidOperationException("AzureWebJobsStorage ist nicht konfiguriert."));
portalTableService.CreateAndRegisterTableClient<AppEntity>("Apps");
portalTableService.CreateAndRegisterTableClient<AppAssignmentEntity>("AppAssignments");
builder.Services.AddKeyedSingleton<ExtendedAzureTableClientService>("PortalStorage", portalTableService);
builder.Services.AddScoped<IAppService, AppService>();

// Blob Storage für Icons (Container "app-icons" wird beim ersten Upload erstellt)
var blobServiceClient = new BlobServiceClient(
    builder.Configuration["AzureWebJobsStorage"] ?? throw new InvalidOperationException("AzureWebJobsStorage ist nicht konfiguriert."));
builder.Services.AddSingleton(blobServiceClient);
builder.Services.AddScoped<IIconService, IconService>();

builder.UseMiddleware<JwtValidationMiddleware>();
builder.UseMiddleware<ChurchToolReferenceMiddleware>();


builder.Build().Run();
