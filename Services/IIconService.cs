namespace EaglesJungscharen.Azure.AppPortal.Services;

public interface IIconService
{
    Task UploadIconAsync(string appId, string contentType, Stream data);
    Task<(Stream Stream, string ContentType)?> GetIconAsync(string appId);
    Task DeleteIconAsync(string appId);
}
