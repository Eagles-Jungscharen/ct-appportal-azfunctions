using EaglesJungscharen.Azure.AppPortal.Models.Dtos;
using EaglesJungscharen.Azure.AppPortal.Models.Entities;
using GuedesPlace.AzureTools.Tables;
using Microsoft.Extensions.DependencyInjection;

namespace EaglesJungscharen.Azure.AppPortal.Services;

public class AppService([FromKeyedServices("PortalStorage")] ExtendedAzureTableClientService tableService) : IAppService
{
    private readonly TypedAzureTableClient<AppEntity> _appTable = tableService.GetTypedTableClient<AppEntity>();
    private readonly TypedAzureTableClient<AppAssignmentEntity> _assignmentTable = tableService.GetTypedTableClient<AppAssignmentEntity>();

    public async Task<List<AppDto>> GetAppsForUserAsync(IEnumerable<string> groupIds)
    {
        var userGroups = new HashSet<string>(groupIds);

        // Alle Apps und Zuweisungen parallel laden
        var appResultsTask = _appTable.GetAllAsync();
        var assignmentResultsTask = _assignmentTable.GetAllAsync();
        await Task.WhenAll(appResultsTask, assignmentResultsTask);

        var apps = appResultsTask.Result.Select(r => r.Entity).ToList();
        var assignments = assignmentResultsTask.Result.Select(r => r.Entity).ToList();

        // Zuweisungen nach AppId gruppieren: AppId -> zugewiesene GroupIds
        var assignmentsByAppId = assignments
            .GroupBy(a => a.AppId)
            .ToDictionary(g => g.Key, g => g.Select(a => a.GroupId).ToHashSet());

        return apps
            .Where(app =>
            {
                // Apps ohne Zuweisung sind öffentlich — für alle Benutzer sichtbar
                if (!assignmentsByAppId.TryGetValue(app.Id, out var appGroups))
                    return true;

                // App sichtbar wenn mind. eine Gruppe des Benutzers übereinstimmt
                return appGroups.Overlaps(userGroups);
            })
            .Select(app => new AppDto(
                app.Id,
                app.Name,
                app.Description,
                app.Url,
                app.IconUrl,
                app.RedirectUris,
                []
            ))
            .ToList();
    }
}
