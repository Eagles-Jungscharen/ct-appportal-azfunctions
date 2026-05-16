using EaglesJungscharen.Azure.AppPortal.Models.Dtos;

namespace EaglesJungscharen.Azure.AppPortal.Services;

public interface IAppService
{
    Task<List<AppDto>> GetAppsForUserAsync(IEnumerable<string> groupIds);
}
