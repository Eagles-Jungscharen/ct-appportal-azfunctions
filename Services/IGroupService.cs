using EaglesJungscharen.Azure.AppPortal.Models.Dtos;

namespace EaglesJungscharen.Azure.AppPortal.Services;

public interface IGroupService
{
    Task<List<GroupDto>> GetGroupsAsync();
}
