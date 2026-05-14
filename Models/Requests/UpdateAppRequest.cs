namespace EaglesJungscharen.Azure.AppPortal.Models.Requests;

public record UpdateAppRequest(
    string Name,
    string? Description,
    string Url,
    string? IconUrl,
    List<string> RedirectUris
);
