namespace EaglesJungscharen.Azure.AppPortal.Models.Requests;

public record CreateClientRequest(
    string Name,
    List<string> RedirectUris
);
