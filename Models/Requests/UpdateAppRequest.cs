namespace EaglesJungscharen.Azure.AppPortal.Models.Requests;

public record UpdateAppRequest(
    string Name,
    string? Description,
    string Url,
    List<string> RedirectUris
);
