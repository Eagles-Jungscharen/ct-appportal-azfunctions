using System.Net;
using System.Security.Claims;
using System.Text.Json;
using EaglesJungscharen.Azure.AppPortal.Models.Dtos;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using EaglesJungscharen.Azure.AppPortal.ChurchToolIDPServices.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;


namespace EaglesJungscharen.Azure.AppPortal.ChurchToolIDPServices.Middleware;

public class JwtValidationMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _oidcConfigManager;
    private readonly ILogger<JwtValidationMiddleware> _logger;

    public JwtValidationMiddleware(IOptions<ChurchToolIDPConfig> options, ILogger<JwtValidationMiddleware> logger)
    {
        var authority = options.Value.OidcAuthorityUrl;

        // OIDC Discovery-Dokument laden — Signing Keys werden automatisch gecacht und periodisch erneuert
        _oidcConfigManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{authority.TrimEnd('/')}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever());

        _logger = logger;
    }

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

        // OIDC-Konfiguration laden (Signing Keys werden gecacht und automatisch erneuert)
        var oidcConfig = await _oidcConfigManager.GetConfigurationAsync(CancellationToken.None);

        var validationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = true,
            ValidIssuer = oidcConfig.Issuer,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = oidcConfig.SigningKeys,
        };

        // Token direkt validieren — kein Umweg über die ASP.NET Core Auth-Pipeline
        var handler = new JsonWebTokenHandler();
        var result = await handler.ValidateTokenAsync(token, validationParameters);
        if (!result.IsValid)
        {
            _oidcConfigManager.RequestRefresh(); // Bei Validierungsfehlern könnte es an veralteten Keys liegen — OIDC-Konfiguration aktualisieren
            oidcConfig = await _oidcConfigManager.GetConfigurationAsync(CancellationToken.None);
            _logger.LogDebug("Token-Validierung fehlgeschlagen, versuche mit aktualisierten OIDC-Konfiguration. Fehler: {Reason}", result.Exception?.Message);
            result = await handler.ValidateTokenAsync(token, validationParameters);    
        }

        if (!result.IsValid)
        {
            _logger.LogDebug("Token-Validierung fehlgeschlagen: {Reason}", result.Exception?.Message);
            await WriteUnauthorizedAsync(context, requestData, "Token ungültig oder abgelaufen.");
            return;
        }

        // Validierten ClaimsPrincipal auf den HttpContext setzen
        var httpContext = context.GetHttpContext();
        if (httpContext is not null)
        {
            httpContext.User = new ClaimsPrincipal(result.ClaimsIdentity);
        }

        await next(context);
    }

    private static async Task WriteUnauthorizedAsync(
        FunctionContext context,
        Microsoft.Azure.Functions.Worker.Http.HttpRequestData requestData,
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
