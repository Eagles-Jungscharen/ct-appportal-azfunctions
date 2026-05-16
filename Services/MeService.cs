using System.Security.Claims;
using EaglesJungscharen.Azure.AppPortal.Middleware;
using EaglesJungscharen.Azure.AppPortal.Models.Dtos;
using Microsoft.Extensions.Logging;

namespace EaglesJungscharen.Azure.AppPortal.Services;

public class MeService : IMeService
{
    private readonly ChurchToolsClientFactory _clientFactory;
    private readonly ILogger<MeService> _logger;

    public MeService(ChurchToolsClientFactory clientFactory, ILogger<MeService> logger)
    {
        _clientFactory = clientFactory;
        _logger = logger;
    }

    public async Task<MeDto> GetMeDtoAsync(ClaimsPrincipal user, string userId)
    {
        // Anzeigename aus dem name-Claim lesen
        var firstName = user.FindFirstValue("firstname") ?? userId;
        var lastName = user.FindFirstValue("lastname") ?? "";
        var displayName = $"{firstName} {lastName}".Trim();

        // TODO: CHURCHTOOL_USERINFO_URL konfigurieren
        // Sobald der Endpoint bekannt ist, hier den HTTP-Call implementieren:
        //
        //   var client = _clientFactory.CreateClient("ChurchtoolIdp");
        //   var response = await client.GetAsync($"CHURCHTOOL_USERINFO_URL/{userId}");
        //   var userInfo = await response.Content.ReadFromJsonAsync<ChurchtoolUserInfoDto>();
        //   isAdmin = userInfo?.IsAdmin ?? false;
        //   groups = userInfo?.Groups ?? [];
        //
        // Vorerst Platzhalterwerte verwenden:
        var ctClient = _clientFactory.Create();
        var whoamiResponse = await ctClient.Whoami.GetAsWhoamiGetResponseAsync();
        var whoami = whoamiResponse?.Data;
        _logger.LogInformation("CHURCHTOOL WhoAmI: UserId={UserId}, DisplayName={DisplayName}", whoami?.Id, whoami != null ? whoami.FirstName + " " + whoami.LastName : "N/A");
        var myGroups = await ctClient.Persons[whoami?.Id ?? 0].Groups.GetAsGroupsGetResponseAsync();
        var groups = myGroups?.Data
            ?.Where(g => !string.IsNullOrEmpty(g.Group?.DomainIdentifier))
            .Select(g => new GroupDto(g.Group!.DomainIdentifier!, g.Group.Title ?? string.Empty))
            .ToList() ?? [];
        groups.ForEach(g => _logger.LogInformation("CHURCHTOOL Group: Id={GroupId}, Name={GroupName}", g.Id, g.Title));
        var isAdmin = false;

        return new MeDto(userId, displayName, isAdmin, groups);
    }
}
