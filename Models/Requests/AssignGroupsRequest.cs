namespace EaglesJungscharen.Azure.AppPortal.Models.Requests;

public record AssignGroupsRequest(
    List<string> GroupIds
);
