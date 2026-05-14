using System.Security.Claims;
using EaglesJungscharen.Azure.AppPortal.Middleware;
using EaglesJungscharen.Azure.AppPortal.Models.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace EaglesJungscharen.Azure.AppPortal.Functions;

public class MeFunction
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly IMemoryCache _cache;
    private readonly ChurchToolsClientFactory _clientFactory;
    private readonly ILogger<MeFunction> _logger;

    public MeFunction(
        IMemoryCache cache,
        ChurchToolsClientFactory clientFactory,
        ILogger<MeFunction> logger)
    {
        _cache = cache;
        _clientFactory = clientFactory;
        _logger = logger;
    }

    [Function("Me")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me")] HttpRequest req)
    {
        // Benutzer-ID aus dem JWT sub-Claim lesen
        var userId = req.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? req.HttpContext.User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(userId))
        {
            return new ObjectResult(new ErrorRecord("Nicht authentifiziert.", 1001))
            {
                StatusCode = StatusCodes.Status401Unauthorized
            };
        }

        var cacheKey = $"me_{userId}";

        // Cache prüfen — falls vorhanden, direkt zurückgeben
        if (_cache.TryGetValue(cacheKey, out MeDto? cached) && cached is not null)
        {
            _logger.LogInformation("MeDto für Benutzer {UserId} aus Cache geladen.", userId);
            return new OkObjectResult(cached);
        }

        // Benutzerinformationen laden und MeDto aufbauen
        var meDto = await BuildMeDtoAsync(req.HttpContext.User, userId);

        // Ergebnis in den Cache schreiben
        _cache.Set(cacheKey, meDto, CacheTtl);
        _logger.LogInformation("MeDto für Benutzer {UserId} in Cache gespeichert (TTL: {Ttl}).", userId, CacheTtl);

        return new OkObjectResult(meDto);
    }

    private async Task<MeDto> BuildMeDtoAsync(ClaimsPrincipal user, string userId)
    {
        // Anzeigename aus dem name-Claim lesen
        var firstName = user.FindFirstValue("firstname") ?? userId;
        var lastName = user.FindFirstValue("lastname") ?? "";
        var displayName = $"{firstName} {lastName}".Trim();
        // TODO: CHURCHTOOL_USERINFO_URL konfigurieren
        // Sobald der Endpoint bekannt ist, hier den HTTP-Call implementieren:
        //
        //   var client = _clientFactory.CreateClient("ChurchtoolIdp");
        //   var response = await client.GetAsync($"CHURCHTOOL_USERINFO_URL/{userId}");
        //   var userInfo = await response.Content.ReadFromJsonAsync<ChurchtoolUserInfoDto>();
        //   isAdmin = userInfo?.IsAdmin ?? false;
        //   groups = userInfo?.Groups ?? [];
        //
        // Vorerst Platzhalterwerte verwenden:
        var ctClient = _clientFactory.Create();
        var whoamiResponse = await ctClient.Whoami.GetAsWhoamiGetResponseAsync();
        var whoami = whoamiResponse?.Data;
        _logger.LogInformation("CHURCHTOOL WhoAmI: UserId={User Id}, DisplayName={DisplayName}", whoami?.Id, whoami != null ? whoami.FirstName + " " + whoami.LastName : "N/A");
        var isAdmin = false;
        var groups = new List<string>();

        await Task.CompletedTask; // Platzhalter — entfernen sobald echter HTTP-Call implementiert ist

        return new MeDto(userId, displayName, isAdmin, groups);
    }
}
