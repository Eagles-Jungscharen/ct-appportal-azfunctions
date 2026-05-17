using System.Security.Claims;
using EaglesJungscharen.Azure.AppPortal.Models.Dtos;
using EaglesJungscharen.Azure.AppPortal.Models.Requests;
using EaglesJungscharen.Azure.AppPortal.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace EaglesJungscharen.Azure.AppPortal.Functions;

public class AppManagementFunction
{
    private readonly IMeService _meService;
    private readonly IAppService _appService;
    private readonly ILogger<AppManagementFunction> _logger;

    public AppManagementFunction(
        IMeService meService,
        IAppService appService,
        ILogger<AppManagementFunction> logger)
    {
        _meService = meService;
        _appService = appService;
        _logger = logger;
    }

    [Function("AppManagement_GetApps")]
    public async Task<IActionResult> GetApps(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "appmanagement/apps")] HttpRequest req) =>
        await ExecuteAsAdminAsync(req, async (_, _) =>
        {
            var apps = await _appService.GetAllAppsAsync();
            _logger.LogInformation("{Count} App(s) für Admin geladen.", apps.Count);
            return new OkObjectResult(apps);
        });

    [Function("AppManagement_CreateApp")]
    public async Task<IActionResult> CreateApp(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "appmanagement/apps")] HttpRequest req) =>
        await ExecuteAsAdminAsync(req, async (req, _) =>
        {
            var request = await req.ReadFromJsonAsync<CreateAppRequest>();
            if (request is null || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Url))
                return new ObjectResult(new ErrorRecord("Ungültige Anfrage. Name und URL sind Pflichtfelder.", 1100))
                    { StatusCode = StatusCodes.Status400BadRequest };

            var app = await _appService.CreateAppAsync(request);
            _logger.LogInformation("App {AppId} '{AppName}' erstellt.", app.Id, app.Name);
            return new ObjectResult(app) { StatusCode = StatusCodes.Status201Created };
        });

    [Function("AppManagement_UpdateApp")]
    public async Task<IActionResult> UpdateApp(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "appmanagement/apps/{id}")] HttpRequest req,
        string id) =>
        await ExecuteAsAdminAsync(req, async (req, _) =>
        {
            var request = await req.ReadFromJsonAsync<UpdateAppRequest>();
            if (request is null || string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Url))
                return new ObjectResult(new ErrorRecord("Ungültige Anfrage. Name und URL sind Pflichtfelder.", 1100))
                    { StatusCode = StatusCodes.Status400BadRequest };

            var app = await _appService.UpdateAppAsync(id, request);
            if (app is null)
                return new ObjectResult(new ErrorRecord("Die Applikation wurde nicht gefunden.", 1101))
                    { StatusCode = StatusCodes.Status404NotFound };

            _logger.LogInformation("App {AppId} aktualisiert.", id);
            return new OkObjectResult(app);
        });

    [Function("AppManagement_DeleteApp")]
    public async Task<IActionResult> DeleteApp(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "appmanagement/apps/{id}")] HttpRequest req,
        string id) =>
        await ExecuteAsAdminAsync(req, async (_, _) =>
        {
            var deleted = await _appService.DeleteAppAsync(id);
            if (!deleted)
                return new ObjectResult(new ErrorRecord("Die Applikation wurde nicht gefunden.", 1101))
                    { StatusCode = StatusCodes.Status404NotFound };

            _logger.LogInformation("App {AppId} gelöscht.", id);
            return new NoContentResult();
        });

    [Function("AppManagement_AssignGroups")]
    public async Task<IActionResult> AssignGroups(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "appmanagement/apps/{id}/assignments")] HttpRequest req,
        string id) =>
        await ExecuteAsAdminAsync(req, async (req, _) =>
        {
            var app = await _appService.GetAppByIdAsync(id);
            if (app is null)
                return new ObjectResult(new ErrorRecord("Die Applikation wurde nicht gefunden.", 1101))
                    { StatusCode = StatusCodes.Status404NotFound };

            var request = await req.ReadFromJsonAsync<AssignGroupsRequest>();
            if (request is null)
                return new ObjectResult(new ErrorRecord("Ungültige Anfrage.", 1100))
                    { StatusCode = StatusCodes.Status400BadRequest };

            await _appService.AssignGroupsAsync(id, request.GroupIds);
            _logger.LogInformation("{Count} Gruppe(n) für App {AppId} gesetzt.", request.GroupIds.Count, id);
            return new OkResult();
        });

    // Auth-Guard: 401 wenn nicht authentifiziert, 403 wenn kein Admin, sonst Handler ausführen
    private async Task<IActionResult> ExecuteAsAdminAsync(
        HttpRequest req,
        Func<HttpRequest, MeDto, Task<IActionResult>> handler)
    {
        var userId = req.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? req.HttpContext.User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(userId))
            return new ObjectResult(new ErrorRecord("Nicht authentifiziert.", 1001))
                { StatusCode = StatusCodes.Status401Unauthorized };

        var meDto = await _meService.GetMeDtoAsync(req.HttpContext.User, userId);
        if (!meDto.IsAdmin)
            return new ObjectResult(new ErrorRecord("Zugriff verweigert. Admin-Berechtigung erforderlich.", 1002))
                { StatusCode = StatusCodes.Status403Forbidden };

        try
        {
            return await handler(req, meDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unerwarteter Fehler beim Ausführen eines Admin-Handlers.");
            return new ObjectResult(new ErrorRecord("Ein interner Fehler ist aufgetreten.", 1000))
                { StatusCode = StatusCodes.Status500InternalServerError };
        }
    }
}
