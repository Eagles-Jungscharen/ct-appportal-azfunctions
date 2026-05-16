using System.Security.Claims;
using EaglesJungscharen.Azure.AppPortal.Models.Dtos;

namespace EaglesJungscharen.Azure.AppPortal.Services;

public interface IMeService
{
    Task<MeDto> GetMeDtoAsync(ClaimsPrincipal user, string userId);
}
