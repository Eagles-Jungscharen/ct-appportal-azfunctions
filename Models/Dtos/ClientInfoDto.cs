namespace EaglesJungscharen.Azure.AppPortal.Models.Dtos;

public record ClientInfoDto(
    string ClientId,
    string Name,
    string Owner,
    List<string> RedirectUris
);
