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
    private readonly IGroupService _groupService;
    private readonly IChurchtoolIdpService _churchtoolIdpService;
    private readonly IIconService _iconService;
    private readonly ILogger<AppManagementFunction> _logger;

    public AppManagementFunction(
        IMeService meService,
        IAppService appService,
        IGroupService groupService,
        IChurchtoolIdpService churchtoolIdpService,
        IIconService iconService,
        ILogger<AppManagementFunction> logger)
    {
        _meService = meService;
        _appService = appService;
        _groupService = groupService;
        _churchtoolIdpService = churchtoolIdpService;
        _iconService = iconService;
        _logger = logger;
    }

    [Function("AppManagement_GetGroups")]
    public async Task<IActionResult> GetGroups([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "appmanagement/groups")] HttpRequest req) =>
        await ExecuteAsAdminAsync(req, async (_, _) =>
        {
            var groups = await _groupService.GetGroupsAsync();
            _logger.LogInformation("{Count} Gruppe(n) für Admin geladen.", groups.Count);
            return new OkObjectResult(groups);
        });

    [Function("AppManagement_GetAssignments")]
    public async Task<IActionResult> GetAssignments([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "appmanagement/apps/{id}/assignments")] HttpRequest req, string id) =>
        await ExecuteAsAdminAsync(req, async (_, _) =>
        {
            var app = await _appService.GetAppByIdAsync(id);
            if (app is null)
                return new ObjectResult(new ErrorRecord("Die Applikation wurde nicht gefunden.", 1101))
                { StatusCode = StatusCodes.Status404NotFound };

            var groupIds = await _appService.GetAssignmentsAsync(id);
            _logger.LogInformation("{Count} Zuweisung(en) für App {AppId} geladen.", groupIds.Count, id);
            return new OkObjectResult(groupIds);
        });

    [Function("AppManagement_GetApps")]
    public async Task<IActionResult> GetApps([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "appmanagement/apps")] HttpRequest req) =>
        await ExecuteAsAdminAsync(req, async (_, _) =>
        {
            var apps = await _appService.GetAllAppsAsync();
            _logger.LogInformation("{Count} App(s) für Admin geladen.", apps.Count);
            return new OkObjectResult(apps);
        });

    [Function("AppManagement_CreateApp")]
    public async Task<IActionResult> CreateApp([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "appmanagement/apps")] HttpRequest req) =>
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
    public async Task<IActionResult> UpdateApp([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "appmanagement/apps/{id}")] HttpRequest req, string id) =>
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
    public async Task<IActionResult> DeleteApp([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "appmanagement/apps/{id}")] HttpRequest req, string id) =>
        await ExecuteAsAdminAsync(req, async (_, _) =>
        {
            var deleted = await _appService.DeleteAppAsync(id);
            if (!deleted)
                return new ObjectResult(new ErrorRecord("Die Applikation wurde nicht gefunden.", 1101))
                { StatusCode = StatusCodes.Status404NotFound };

            // Icon aus Blob Storage löschen (Fehler nicht propagieren)
            try { await _iconService.DeleteIconAsync(id); } catch { }

            _logger.LogInformation("App {AppId} gelöscht.", id);
            return new NoContentResult();
        });

    [Function("AppManagement_AssignGroups")]
    public async Task<IActionResult> AssignGroups([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "appmanagement/apps/{id}/assignments")] HttpRequest req, string id) =>
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
            return new NoContentResult();
        });

    [Function("AppManagement_UploadIcon")]
    public async Task<IActionResult> UploadIcon(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "appmanagement/apps/{id}/icon")] HttpRequest req, string id) =>
        await ExecuteAsAdminAsync(req, async (req, _) =>
        {
            // App-Existenz prüfen
            var app = await _appService.GetAppByIdAsync(id);
            if (app is null)
                return new ObjectResult(new ErrorRecord("Die Applikation wurde nicht gefunden.", 1101))
                { StatusCode = StatusCodes.Status404NotFound };

            // Datei aus multipart/form-data lesen
            if (!req.HasFormContentType)
                return new ObjectResult(new ErrorRecord("Keine Datei hochgeladen.", 1150))
                { StatusCode = StatusCodes.Status400BadRequest };

            var form = await req.ReadFormAsync();
            if (form.Files.Count == 0)
                return new ObjectResult(new ErrorRecord("Keine Datei hochgeladen.", 1150))
                { StatusCode = StatusCodes.Status400BadRequest };

            var file = form.Files[0];

            // Content-Type validieren
            var allowedTypes = new HashSet<string> { "image/png", "image/jpeg", "image/svg+xml", "image/webp" };
            if (!allowedTypes.Contains(file.ContentType))
                return new ObjectResult(new ErrorRecord("Ungültiges Dateiformat. Erlaubt: PNG, JPEG, SVG, WebP.", 1151))
                { StatusCode = StatusCodes.Status400BadRequest };

            // Grösse prüfen (max. 512 KB)
            if (file.Length > 512 * 1024)
                return new ObjectResult(new ErrorRecord("Die Datei ist zu gross. Maximale Grösse: 512 KB.", 1152))
                { StatusCode = StatusCodes.Status400BadRequest };

            using var stream = file.OpenReadStream();
            await _iconService.UploadIconAsync(id, file.ContentType, stream);
            await _appService.SetIconContentTypeAsync(id, file.ContentType);

            _logger.LogInformation("Icon für App {AppId} hochgeladen.", id);
            return new NoContentResult();
        });

    [Function("AppManagement_GetClients")]
    public async Task<IActionResult> GetClients([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "appmanagement/clients")] HttpRequest req) =>
        await ExecuteAsAdminAsync(req, async (_, _) =>
        {
            var clients = await _churchtoolIdpService.GetClientsAsync();
            _logger.LogInformation("{Count} Client(s) für Admin geladen.", clients.Count());
            return new OkObjectResult(clients);
        });

    [Function("AppManagement_CreateClient")]
    public async Task<IActionResult> CreateClient([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "appmanagement/clients")] HttpRequest req) =>
        await ExecuteAsAdminAsync(req, async (req, meDto) =>
        {
            var request = await req.ReadFromJsonAsync<CreateClientRequest>();
            if (request is null || string.IsNullOrWhiteSpace(request.Name))
                return new ObjectResult(new ErrorRecord("Ungültige Anfrage. Name ist ein Pflichtfeld.", 1300))
                { StatusCode = StatusCodes.Status400BadRequest };
            if (request.RedirectUris is null || request.RedirectUris.Count == 0)
                return new ObjectResult(new ErrorRecord("Mindestens eine Redirect-URI ist erforderlich.", 1301))
                { StatusCode = StatusCodes.Status400BadRequest };

            var client = await _churchtoolIdpService.CreateClientAsync(request.Name, meDto.UserId, request.RedirectUris);
            _logger.LogInformation("Client '{Name}' erstellt: {ClientId}", client.Name, client.ClientId);
            return new ObjectResult(client) { StatusCode = StatusCodes.Status201Created };
        });

    [Function("AppManagement_UpdateClient")]
    public async Task<IActionResult> UpdateClient([HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "appmanagement/clients/{clientId}")] HttpRequest req, string clientId) =>
        await ExecuteAsAdminAsync(req, async (req, _) =>
        {
            var request = await req.ReadFromJsonAsync<UpdateClientRequest>();
            if (request is null || (request.Name is null && request.Owner is null && request.RedirectUris is null))
                return new ObjectResult(new ErrorRecord("Mindestens ein Feld (Name, Owner oder RedirectUris) muss angegeben werden.", 1302))
                { StatusCode = StatusCodes.Status400BadRequest };

            var client = await _churchtoolIdpService.UpdateClientAsync(clientId, request.Name, request.Owner, request.RedirectUris);
            if (client is null)
                return new ObjectResult(new ErrorRecord("Der Client wurde nicht gefunden.", 1303))
                { StatusCode = StatusCodes.Status404NotFound };

            _logger.LogInformation("Client {ClientId} aktualisiert.", clientId);
            return new OkObjectResult(client);
        });

    [Function("AppManagement_DeleteClient")]
    public async Task<IActionResult> DeleteClient([HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "appmanagement/clients/{clientId}")] HttpRequest req, string clientId) =>
        await ExecuteAsAdminAsync(req, async (_, _) =>
        {
            var deleted = await _churchtoolIdpService.DeleteClientAsync(clientId);
            if (!deleted)
                return new ObjectResult(new ErrorRecord("Der Client wurde nicht gefunden.", 1303))
                { StatusCode = StatusCodes.Status404NotFound };

            _logger.LogInformation("Client {ClientId} gelöscht.", clientId);
            return new NoContentResult();
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
