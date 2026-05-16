namespace EaglesJungscharen.Azure.AppPortal.Models.Dtos;

public record AppDto(
    string Id,
    string Name,
    string? Description,
    string Url,
    string? IconUrl,
    List<string> RedirectUris,
    List<RoleDto> Roles
);
