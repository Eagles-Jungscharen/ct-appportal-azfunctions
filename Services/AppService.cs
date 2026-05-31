using EaglesJungscharen.Azure.AppPortal.Models.Dtos;
using EaglesJungscharen.Azure.AppPortal.Models.Entities;
using EaglesJungscharen.Azure.AppPortal.Models.Requests;
using GuedesPlace.AzureTools.Tables;
using Microsoft.Extensions.DependencyInjection;

namespace EaglesJungscharen.Azure.AppPortal.Services;

public class AppService([FromKeyedServices("PortalStorage")] ExtendedAzureTableClientService tableService) : IAppService
{
    private const string AppPartitionKey = "App";
    private const string AssignmentPartitionKey = "Assignment";
    private readonly TypedAzureTableClient<AppEntity> _appTable = tableService.GetTypedTableClient<AppEntity>();
    private readonly TypedAzureTableClient<AppAssignmentEntity> _assignmentTable = tableService.GetTypedTableClient<AppAssignmentEntity>();

    public async Task<List<AppDto>> GetAppsForUserAsync(IEnumerable<string> groupIds)
    {
        var userGroups = new HashSet<string>(groupIds);

        // Alle Apps und Zuweisungen parallel laden
        var appResultsTask = _appTable.GetAllAsync(AppPartitionKey);
        var assignmentResultsTask = _assignmentTable.GetAllAsync(AssignmentPartitionKey);
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
            .Select(app => ToDto(app))
            .ToList();
    }

    public async Task<List<AppDto>> GetAllAppsAsync()
    {
        var results = await _appTable.GetAllAsync(AppPartitionKey);
        return results.Select(r => ToDto(r.Entity)).ToList();
    }

    public async Task<AppDto?> GetAppByIdAsync(string id)
    {
        var result = await _appTable.GetByIdAsync(id, AppPartitionKey);
        return result is null ? null : ToDto(result.Entity);
    }

    public async Task<AppDto> CreateAppAsync(CreateAppRequest request)
    {
        var entity = new AppEntity
        {
            Id = Guid.NewGuid().ToString(),
            Name = request.Name,
            Description = request.Description,
            Url = request.Url,
            RedirectUris = request.RedirectUris,
        };
        await _appTable.InsertOrReplaceAsync(rowKey: entity.Id, partitionKey: AppPartitionKey, entity);
        return ToDto(entity);
    }

    public async Task<AppDto?> UpdateAppAsync(string id, UpdateAppRequest request)
    {
        var existing = await _appTable.GetByIdAsync(id, AppPartitionKey);
        if (existing is null)
            return null;

        var entity = existing.Entity;
        entity.Name = request.Name;
        entity.Description = request.Description;
        entity.Url = request.Url;
        entity.RedirectUris = request.RedirectUris;

        await _appTable.InsertOrReplaceAsync(rowKey: entity.Id, partitionKey: AppPartitionKey, entity);
        return ToDto(entity);
    }

    public async Task<bool> DeleteAppAsync(string id)
    {
        var existing = await _appTable.GetByIdAsync(id, AppPartitionKey);
        if (existing is null)
            return false;

        // App löschen
        await _appTable.DeleteEntityAsync(rowKey: id, partitionKey: AppPartitionKey);

        // Alle Zuweisungen dieser App löschen
        var assignments = (await _assignmentTable.GetAllAsync(AssignmentPartitionKey))
            .Select(r => r.Entity)
            .Where(a => a.AppId == id)
            .ToList();

        foreach (var assignment in assignments)
            await _assignmentTable.DeleteEntityAsync(rowKey: assignment.Id, partitionKey: AssignmentPartitionKey);

        return true;
    }

    public async Task<List<string>> GetAssignmentsAsync(string appId)
    {
        var results = await _assignmentTable.GetAllAsync(AssignmentPartitionKey);
        return results
            .Select(r => r.Entity)
            .Where(a => a.AppId == appId)
            .Select(a => a.GroupId)
            .ToList();
    }

    public async Task AssignGroupsAsync(string id, List<string> groupIds)
    {
        // Bestehende Zuweisungen für diese App löschen
        var existing = (await _assignmentTable.GetAllAsync(AssignmentPartitionKey))
            .Select(r => r.Entity)
            .Where(a => a.AppId == id)
            .ToList();

        foreach (var assignment in existing)
            await _assignmentTable.DeleteEntityAsync(rowKey: assignment.Id, partitionKey: AssignmentPartitionKey);

        // Neue Zuweisungen erstellen
        foreach (var groupId in groupIds)
        {
            var entity = new AppAssignmentEntity
            {
                Id = Guid.NewGuid().ToString(),
                AppId = id,
                GroupId = groupId
            };
            await _assignmentTable.InsertOrReplaceAsync(rowKey: entity.Id, partitionKey: AssignmentPartitionKey, entity);
        }
    }

    public async Task<bool> SetIconContentTypeAsync(string id, string contentType)
    {
        var existing = await _appTable.GetByIdAsync(id, AppPartitionKey);
        if (existing is null)
            return false;

        existing.Entity.IconContentType = contentType;
        await _appTable.InsertOrReplaceAsync(rowKey: id, partitionKey: AppPartitionKey, existing.Entity);
        return true;
    }

    private static AppDto ToDto(AppEntity app) =>
        new(app.Id, app.Name, app.Description, app.Url, app.IconContentType != null, app.RedirectUris);
}
