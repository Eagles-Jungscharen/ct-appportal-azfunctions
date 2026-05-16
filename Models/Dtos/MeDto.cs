namespace EaglesJungscharen.Azure.AppPortal.Models.Dtos;

public record MeDto(
    string UserId,
    string DisplayName,
    bool IsAdmin,
    List<GroupDto> Groups
);
