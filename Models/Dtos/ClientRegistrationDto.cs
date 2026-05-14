namespace EaglesJungscharen.Azure.AppPortal.Models.Dtos;

public record ClientRegistrationDto(
    string Name,
    string Owner,
    List<string> RedirectUris
);
