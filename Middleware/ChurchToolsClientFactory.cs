using Fegmm.ChurchTools;
using Microsoft.Kiota.Abstractions.Authentication;
using Microsoft.Kiota.Http.HttpClientLibrary;

namespace EaglesJungscharen.Azure.AppPortal.Middleware;

public sealed class ChurchToolsClientFactory
{
    private readonly HttpClient _httpClient;
    private readonly IAuthenticationProvider _authenticationProvider;

    public ChurchToolsClientFactory(
        HttpClient httpClient,
        IAuthenticationProvider authenticationProvider)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _authenticationProvider = authenticationProvider ?? throw new ArgumentNullException(nameof(authenticationProvider));
    }

    public ChurchToolsClient Create()
    {
        var adapter = new HttpClientRequestAdapter(
            _authenticationProvider,
            httpClient: _httpClient);

        return new ChurchToolsClient(adapter);
    }
}