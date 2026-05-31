using EaglesJungscharen.Azure.AppPortal.Models.Dtos;
using EaglesJungscharen.Azure.AppPortal.Models.Requests;

namespace EaglesJungscharen.Azure.AppPortal.Services;

public interface IAppService
{
    Task<List<AppDto>> GetAppsForUserAsync(IEnumerable<string> groupIds);
    Task<List<AppDto>> GetAllAppsAsync();
    Task<AppDto?> GetAppByIdAsync(string id);
    Task<AppDto> CreateAppAsync(CreateAppRequest request);
    Task<AppDto?> UpdateAppAsync(string id, UpdateAppRequest request);
    Task<bool> DeleteAppAsync(string id);
    Task<List<string>> GetAssignmentsAsync(string appId);
    Task AssignGroupsAsync(string id, List<string> groupIds);
    Task<bool> SetIconContentTypeAsync(string id, string contentType);
}
