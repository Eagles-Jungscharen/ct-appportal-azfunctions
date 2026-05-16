using System.Security.Claims;
using EaglesJungscharen.Azure.AppPortal.Models.Dtos;
using EaglesJungscharen.Azure.AppPortal.Services;
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
    private readonly IMeService _meService;
    private readonly ILogger<MeFunction> _logger;

    public MeFunction(
        IMemoryCache cache,
        IMeService meService,
        ILogger<MeFunction> logger)
    {
        _cache = cache;
        _meService = meService;
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
        var meDto = await _meService.GetMeDtoAsync(req.HttpContext.User, userId);

        // Ergebnis in den Cache schreiben
        _cache.Set(cacheKey, meDto, CacheTtl);
        _logger.LogInformation("MeDto für Benutzer {UserId} in Cache gespeichert (TTL: {Ttl}).", userId, CacheTtl);

        return new OkObjectResult(meDto);
    }
}
