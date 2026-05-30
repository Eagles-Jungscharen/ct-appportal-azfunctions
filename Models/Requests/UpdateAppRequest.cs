using EaglesJungscharen.Azure.AppPortal.Models.Dtos;

namespace EaglesJungscharen.Azure.AppPortal.Models.Requests;

public record UpdateAppRequest(
    string Name,
    string? Description,
    string Url,
    List<string> RedirectUris,
    List<RoleDto> Roles
);
