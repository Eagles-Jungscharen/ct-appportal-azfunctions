namespace EaglesJungscharen.Azure.AppPortal.Models.Requests;

public record UpdateClientRequest(
    string? Name,
    string? Owner,
    List<string>? RedirectUris
);
