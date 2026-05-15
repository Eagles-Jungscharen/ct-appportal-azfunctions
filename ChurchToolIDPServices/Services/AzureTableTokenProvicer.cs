using EaglesJungscharen.Azure.AppPortal.ChurchToolIDPServices.Models;
using GuedesPlace.AzureTools.Tables;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EaglesJungscharen.Azure.AppPortal.ChurchToolIDPServices.Services;

public interface IUserTokenProvider
{
    Task<string> GetTokenForCurrentUserAsync();
}
public class AzureTableUserTokenProvider(IChurchToolReferenceContext churchToolReferenceContext, ILogger<AzureTableUserTokenProvider> logger, [FromKeyedServices(EaglesJungscharen.Azure.AppPortal.ChurchToolIDPServices.Extensions.ServiceCollectionExtensions.KeyedServiceName)] ExtendedAzureTableClientService extendedTableClientService) : IUserTokenProvider
{
    private readonly TypedAzureTableClient<UserLoginTokens> _tableClient = extendedTableClientService.GetTypedTableClient<UserLoginTokens>();
    private readonly IChurchToolReferenceContext _churchToolReferenceContext = churchToolReferenceContext;
    private readonly ILogger<AzureTableUserTokenProvider> _logger = logger;

    public async Task<string> GetTokenForCurrentUserAsync()
    {
        var churchToolIDPReferenceId = _churchToolReferenceContext.ChurchToolIDPReferenceId;
        _logger.LogInformation("Fetching login token for ChurchToolIDPReferenceId: {ReferenceId}", churchToolIDPReferenceId);

        var entity = await _tableClient.GetByIdAsync(churchToolIDPReferenceId, "USER_TOKEN");
        return entity.Entity.LoginToken;
    }
}
