using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace EaglesJungscharen.Azure.AppPortal.Services;

public class IconService(BlobServiceClient blobServiceClient) : IIconService
{
    private const string ContainerName = "app-icons";

    private BlobContainerClient GetContainer() =>
        blobServiceClient.GetBlobContainerClient(ContainerName);

    public async Task UploadIconAsync(string appId, string contentType, Stream data)
    {
        var container = GetContainer();
        // Container wird bei erstem Upload automatisch erstellt
        await container.CreateIfNotExistsAsync();

        var blob = container.GetBlobClient(appId);
        await blob.UploadAsync(data, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
        });
    }

    public async Task<(Stream Stream, string ContentType)?> GetIconAsync(string appId)
    {
        var container = GetContainer();
        var blob = container.GetBlobClient(appId);

        if (!await blob.ExistsAsync())
            return null;

        var response = await blob.DownloadStreamingAsync();
        return (response.Value.Content, response.Value.Details.ContentType);
    }

    public async Task DeleteIconAsync(string appId)
    {
        var container = GetContainer();
        var blob = container.GetBlobClient(appId);
        await blob.DeleteIfExistsAsync();
    }
}
