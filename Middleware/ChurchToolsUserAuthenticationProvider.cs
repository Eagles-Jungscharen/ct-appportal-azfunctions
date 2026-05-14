using Microsoft.Extensions.Logging;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;

namespace EaglesJungscharen.Azure.AppPortal.Middleware;

public class ChurchToolsUserAuthenticationProvider(IUserTokenProvider tokenProvider, ILogger<ChurchToolsUserAuthenticationProvider> logger) : IAuthenticationProvider
{
    private readonly IUserTokenProvider _tokenProvider = tokenProvider;
    private readonly ILogger<ChurchToolsUserAuthenticationProvider> _logger = logger;

    public async Task AuthenticateRequestAsync(
        RequestInformation request,
        Dictionary<string, object>? context = null,
        CancellationToken cancellationToken = default)
    {
        var token = await _tokenProvider.GetTokenForCurrentUserAsync();
        _logger.LogInformation("Authenticating request to {Url} with token from IUserTokenProvider.", request.URI);
        _logger.LogDebug("Adding Authorization header with token: {Token}", token);
        request.Headers["Authorization"] =[$"Login {token}"];
    }
}