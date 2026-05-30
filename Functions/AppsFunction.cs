using System.Security.Claims;
using EaglesJungscharen.Azure.AppPortal.Models.Dtos;
using EaglesJungscharen.Azure.AppPortal.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace EaglesJungscharen.Azure.AppPortal.Functions;

public class AppsFunction
{
    private readonly IMeService _meService;
    private readonly IAppService _appService;
    private readonly IIconService _iconService;
    private readonly ILogger<AppsFunction> _logger;

    public AppsFunction(
        IMeService meService,
        IAppService appService,
        IIconService iconService,
        ILogger<AppsFunction> logger)
    {
        _meService = meService;
        _appService = appService;
        _iconService = iconService;
        _logger = logger;
    }

    [Function("Apps")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "apps")] HttpRequest req)
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

        // Benutzerinformationen (inkl. Gruppen) laden
        var meDto = await _meService.GetMeDtoAsync(req.HttpContext.User, userId);

        // Apps nach Gruppen des Benutzers filtern
        var groupIds = meDto.Groups.Select(g => g.Id);
        var apps = await _appService.GetAppsForUserAsync(groupIds);

        _logger.LogInformation("{Count} App(s) für Benutzer {UserId} geladen.", apps.Count, userId);

        return new OkObjectResult(apps);
    }

    [Function("Apps_GetIcon")]
    public async Task<IActionResult> GetIcon(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "apps/{id}/icon")] HttpRequest req, string id)
    {
        // Authentifizierung prüfen
        var userId = req.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? req.HttpContext.User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(userId))
            return new ObjectResult(new ErrorRecord("Nicht authentifiziert.", 1001))
            { StatusCode = StatusCodes.Status401Unauthorized };

        var result = await _iconService.GetIconAsync(id);
        if (result is null)
            return new NotFoundResult();

        return new FileStreamResult(result.Value.Stream, result.Value.ContentType);
    }
}
