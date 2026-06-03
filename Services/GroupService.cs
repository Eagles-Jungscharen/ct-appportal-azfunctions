using EaglesJungscharen.Azure.ChurchToolIDPServices.Services;
using EaglesJungscharen.Azure.AppPortal.Models.Dtos;
using Microsoft.Extensions.Logging;

namespace EaglesJungscharen.Azure.AppPortal.Services;

public class GroupService(ChurchToolsClientFactory clientFactory, ILogger<GroupService> logger) : IGroupService
{
    private readonly ChurchToolsClientFactory _clientFactory = clientFactory;
    private readonly ILogger<GroupService> _logger = logger;

    public async Task<List<GroupDto>> GetGroupsAsync()
    {
        var ctClient = _clientFactory.Create();
        var response = await ctClient.Groups.GetAsGroupsGetResponseAsync(config => { config.QueryParameters.Limit = 200; });
        // Gruppen aus ChurchTools laden; DomainIdentifier = "GROUP_<id>" (ChurchTools-Konvention)
        var groups = response?.Data
            ?.Where(g => g.Id.HasValue)
            .Select(g => new GroupDto($"{g.Id}", g.Name ?? string.Empty))
            .ToList() ?? [];
        _logger.LogInformation("{Count} Gruppe(n) aus ChurchTools geladen.", groups.Count);
        return groups;
    }
}
