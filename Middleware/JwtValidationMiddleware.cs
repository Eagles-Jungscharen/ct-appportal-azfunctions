using System.Net;
using System.Text.Json;
using EaglesJungscharen.Azure.AppPortal.Models.Dtos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace EaglesJungscharen.Azure.AppPortal.Middleware;

public class JwtValidationMiddleware : IFunctionsWorkerMiddleware
{
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        // HTTP-Request aus dem FunctionContext lesen
        var requestData = await context.GetHttpRequestDataAsync();

        if (requestData is null)
        {
            // Kein HTTP-Request (z. B. Timer-Trigger) — Middleware überspringen
            await next(context);
            return;
        }

        // Authorization-Header prüfen
        if (!requestData.Headers.TryGetValues("Authorization", out var authValues))
        {
            await WriteUnauthorizedAsync(context, requestData, "Kein Autorisierungs-Header vorhanden.");
            return;
        }

        var authHeader = authValues.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            await WriteUnauthorizedAsync(context, requestData, "Ungültiges Autorisierungsformat. Erwartet: Bearer <token>.");
            return;
        }

        var token = authHeader["Bearer ".Length..].Trim();

        if (string.IsNullOrWhiteSpace(token))
        {
            await WriteUnauthorizedAsync(context, requestData, "Bearer-Token fehlt.");
            return;
        }

        // Token-Validierung über die ASP.NET Core Authentifizierungs-Pipeline
        // Die eigentliche Validierung (Signatur, Ablaufzeit, Issuer) erfolgt über
        // AddAuthentication().AddJwtBearer() in Program.cs — der HttpContext wird
        // dort via IHttpContextAccessor eingebunden.
        // Hier wird nur geprüft, ob der ClaimsPrincipal nach der Middleware-Pipeline gesetzt ist.
        // Für die tiefgreifende Validierung wird der ASP.NET Core-Middleware-Stack genutzt,
        // der via ConfigureFunctionsWebApplication() integriert ist.

        await next(context);

        // Nach der Ausführung: falls die ASP.NET Core Auth-Pipeline den Request als
        // nicht authentifiziert markiert hat, wird dies hier erkannt.
        var httpContext = context.GetHttpContext();
        if (httpContext is not null && !httpContext.User.Identity?.IsAuthenticated == true)
        {
            await WriteUnauthorizedAsync(context, requestData, "Token ungültig oder abgelaufen.");
        }
    }

    private static async Task WriteUnauthorizedAsync(
        FunctionContext context,
        HttpRequestData requestData,
        string message)
    {
        var response = requestData.CreateResponse(HttpStatusCode.Unauthorized);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");

        var error = new ErrorRecord(message, 1000);
        await response.WriteStringAsync(JsonSerializer.Serialize(error, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));

        // Response in den InvocationResult schreiben, damit Azure Functions sie zurückgibt
        var invocationResult = context.GetInvocationResult();
        invocationResult.Value = response;
    }
}
