using EaglesJungscharen.Azure.AppPortal.Models.Dtos;

namespace EaglesJungscharen.Azure.AppPortal.Services;

public interface IChurchtoolIdpService
{
    Task<IEnumerable<ClientInfoDto>> GetClientsAsync();
    Task<ClientInfoDto> CreateClientAsync(string name, string owner, List<string> redirectUris);
    Task<ClientInfoDto?> UpdateClientAsync(string clientId, string? name, string? owner, List<string>? redirectUris);
    Task<bool> DeleteClientAsync(string clientId);
}
