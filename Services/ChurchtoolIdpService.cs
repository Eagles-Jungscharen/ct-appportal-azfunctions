using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using EaglesJungscharen.Azure.AppPortal.Models.Dtos;
using Microsoft.Extensions.Logging;

namespace EaglesJungscharen.Azure.AppPortal.Services;

public class ChurchtoolIdpService(IHttpClientFactory httpClientFactory, ILogger<ChurchtoolIdpService> logger) : IChurchtoolIdpService
{
    // IDP-Deserialisierungsmodelle (spiegeln die Modelle des Churchtool IDP Backends wider)
    private sealed class IdpClientResponse
    {
        [JsonPropertyName("clientId")]
        public required string ClientId { get; set; }
        [JsonPropertyName("name")]
        public required string Name { get; set; }
        [JsonPropertyName("owner")]
        public required string Owner { get; set; }
        [JsonPropertyName("redirectUris")]
        public required List<string> RedirectUris { get; set; }
    }

    private sealed class IdpClientListResponse
    {
        [JsonPropertyName("clients")]
        public required List<IdpClientResponse> Clients { get; set; }
    }

    private sealed class IdpCreateClientRequest
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        [JsonPropertyName("owner")]
        public string? Owner { get; set; }
        [JsonPropertyName("redirectUris")]
        public List<string>? RedirectUris { get; set; }
    }

    private sealed class IdpUpdateClientRequest
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        [JsonPropertyName("owner")]
        public string? Owner { get; set; }
        [JsonPropertyName("redirectUris")]
        public List<string>? RedirectUris { get; set; }
    }

    private static ClientInfoDto ToDto(IdpClientResponse r) =>
        new(r.ClientId, r.Name, r.Owner, r.RedirectUris);

    public async Task<IEnumerable<ClientInfoDto>> GetClientsAsync()
    {
        var client = httpClientFactory.CreateClient("ChurchtoolIdp");
        var response = await client.GetAsync("api/clients");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdpClientListResponse>();
        logger.LogInformation("{Count} Client(s) vom IDP geladen.", result?.Clients.Count ?? 0);
        return result?.Clients.Select(ToDto) ?? [];
    }

    public async Task<ClientInfoDto> CreateClientAsync(string name, string owner, List<string> redirectUris)
    {
        var client = httpClientFactory.CreateClient("ChurchtoolIdp");
        var body = new IdpCreateClientRequest { Name = name, Owner = owner, RedirectUris = redirectUris };
        var response = await client.PostAsJsonAsync("api/clients", body);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdpClientResponse>();
        logger.LogInformation("Client '{Name}' beim IDP erstellt: {ClientId}", name, result!.ClientId);
        return ToDto(result);
    }

    public async Task<ClientInfoDto?> UpdateClientAsync(string clientId, string? name, string? owner, List<string>? redirectUris)
    {
        var client = httpClientFactory.CreateClient("ChurchtoolIdp");
        var body = new IdpUpdateClientRequest { Name = name, Owner = owner, RedirectUris = redirectUris };
        var response = await client.PutAsJsonAsync($"api/clients/{clientId}", body);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdpClientResponse>();
        logger.LogInformation("Client {ClientId} beim IDP aktualisiert.", clientId);
        return ToDto(result!);
    }

    public async Task<bool> DeleteClientAsync(string clientId)
    {
        var client = httpClientFactory.CreateClient("ChurchtoolIdp");
        var response = await client.DeleteAsync($"api/clients/{clientId}");
        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;
        response.EnsureSuccessStatusCode();
        logger.LogInformation("Client {ClientId} beim IDP gelöscht.", clientId);
        return true;
    }
}
