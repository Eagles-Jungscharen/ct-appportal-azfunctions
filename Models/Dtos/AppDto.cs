namespace EaglesJungscharen.Azure.AppPortal.Models.Dtos;

public record AppDto(
    string Id,
    string Name,
    string? Description,
    string Url,
    bool HasIcon,
    List<string> RedirectUris
);
