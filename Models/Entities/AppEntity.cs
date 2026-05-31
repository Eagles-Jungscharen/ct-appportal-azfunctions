namespace EaglesJungscharen.Azure.AppPortal.Models.Entities;

public class AppEntity
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string Url { get; set; }
    public string? IconContentType { get; set; }
    // Wird automatisch als JSON-Array-String serialisiert
    public List<string> RedirectUris { get; set; } = [];
}
