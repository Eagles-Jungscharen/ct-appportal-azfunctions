using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EaglesJungscharen.Azure.AppPortal.Middleware;

public class UserLoginTokens : ITableEntity
{
    public required string Id { get; set; }
    public required string LoginToken { get; set; }
    public required string ChurchToolsCookie { get; set; }
    public required DateTime Expires { get; set; }

    // ITableEntity implementation
    public string PartitionKey { get; set; } = default!;
    public string RowKey { get; set; } = default!;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}

public interface IUserTokenProvider
{
    Task<string> GetTokenForCurrentUserAsync();
}
public class AzureTableUserTokenProvider(IConfiguration configuration,
    IChurchToolReferenceContext churchToolReferenceContext, ILogger<AzureTableUserTokenProvider> logger) : IUserTokenProvider
{
    private readonly TableClient _tableClient = new TableClient(
            connectionString: configuration["CHURCHTOOL_IDP_STORAGE_CONNECTION_STRING"] ?? throw new InvalidOperationException("Storage connection string not configured"),
            tableName: "UserLoginTokensTable");
    private readonly IChurchToolReferenceContext _churchToolReferenceContext = churchToolReferenceContext;
    private readonly ILogger<AzureTableUserTokenProvider> _logger = logger;

    public async Task<string> GetTokenForCurrentUserAsync()
    {
        var churchToolIDPReferenceId = _churchToolReferenceContext.ChurchToolIDPReferenceId;
        _logger.LogInformation("Fetching login token for ChurchToolIDPReferenceId: {ReferenceId}", churchToolIDPReferenceId);

        var entity = await _tableClient.GetEntityAsync<UserLoginTokens>(
            "USER_TOKEN",
             churchToolIDPReferenceId);

        return entity.Value.LoginToken;
    }
}
