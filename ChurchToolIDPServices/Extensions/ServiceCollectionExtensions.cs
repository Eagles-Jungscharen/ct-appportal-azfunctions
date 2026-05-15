using EaglesJungscharen.Azure.AppPortal.ChurchToolIDPServices.Middleware;
using EaglesJungscharen.Azure.AppPortal.ChurchToolIDPServices.Models;
using EaglesJungscharen.Azure.AppPortal.ChurchToolIDPServices.Services;
using EaglesJungscharen.Azure.AppPortal.Middleware;
using GuedesPlace.AzureTools.Tables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Kiota.Abstractions.Authentication;

namespace EaglesJungscharen.Azure.AppPortal.ChurchToolIDPServices.Extensions;

public static class ServiceCollectionExtensions
{
    public const string KeyedServiceName = "ChurchToolIDPService";
    public static IServiceCollection AddChurchToolIDPServices(this IServiceCollection services, string churchToolUrl, string oidcAuthorityUrl, string churchToolIDPStorageConnectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(churchToolUrl, nameof(churchToolUrl));
        ArgumentException.ThrowIfNullOrWhiteSpace(oidcAuthorityUrl, nameof(oidcAuthorityUrl));
        ArgumentException.ThrowIfNullOrWhiteSpace(churchToolIDPStorageConnectionString, nameof(churchToolIDPStorageConnectionString));

        services.Configure<ChurchToolIDPConfig>(options =>
        {
            options.ChurchToolUrl = churchToolUrl;
            options.OidcAuthorityUrl = oidcAuthorityUrl;
        });
        var extendedTableClientService = new ExtendedAzureTableClientService(churchToolIDPStorageConnectionString);
        extendedTableClientService.CreateAndRegisterTableClient<UserLoginTokens>("UserLoginTokensTable");
        services.AddKeyedSingleton<ExtendedAzureTableClientService>(KeyedServiceName, extendedTableClientService);
        services.AddScoped<IChurchToolReferenceContext, FunctionChurchToolReferenceContext>();
        services.AddScoped<IUserTokenProvider, AzureTableUserTokenProvider>();
        services.AddScoped<IAuthenticationProvider, ChurchToolsUserAuthenticationProvider>();
        services.AddScoped<ChurchToolsClientFactory>();

        services.AddHttpClient<ChurchToolsClientFactory>(client =>
        {
            client.BaseAddress = new Uri($"{churchToolUrl}/api");
        });
        return services;
    }
}